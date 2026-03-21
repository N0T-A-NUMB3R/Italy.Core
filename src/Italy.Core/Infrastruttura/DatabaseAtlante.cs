using Microsoft.Data.Sqlite;
using System.Reflection;
using System.IO;
using Italy.Core.Domain.Entità;

namespace Italy.Core.Infrastruttura;

/// <summary>
/// Gestisce l'accesso al database SQLite embedded "italy.db".
/// Il database viene estratto dalla risorsa embedded ad ogni avvio se non presente.
/// L'accesso è esclusivamente in modalità Read-Only per thread-safety garantita.
/// </summary>
public sealed class DatabaseAtlante : IDisposable
{
    private static readonly object _lock = new();
    private static string? _percorsoDb;
    private bool _rilasciata;

    // Stringa di connessione Read-Only per massima concorrenza
    public string StringaConnessione => $"Data Source={PercorsoDb};Mode=ReadOnly;Cache=Shared;";

    public string PercorsoDb
    {
        get
        {
            if (_percorsoDb != null) return _percorsoDb;
            lock (_lock)
            {
                if (_percorsoDb != null) return _percorsoDb;
                _percorsoDb = EstraiDatabase();
            }
            return _percorsoDb;
        }
    }

    /// <summary>Crea una nuova connessione read-only al database Atlante.</summary>
    public SqliteConnection ApriConnessione()
    {
        var connessione = new SqliteConnection(StringaConnessione);
        connessione.Open();
        return connessione;
    }

    /// <summary>Esegue una query e mappa i risultati tramite il delegato fornito.</summary>
    public IReadOnlyList<T> Esegui<T>(string sql, Action<SqliteCommand>? parametri, Func<SqliteDataReader, T> mappatura)
    {
        using var conn = ApriConnessione();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        parametri?.Invoke(cmd);

        var risultati = new List<T>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            risultati.Add(mappatura(reader));
        return risultati;
    }

    public T? EseguiScalare<T>(string sql, Action<SqliteCommand>? parametri = null)
    {
        using var conn = ApriConnessione();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        parametri?.Invoke(cmd);
        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value) return default;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    // ── Estrazione Risorsa Embedded ──────────────────────────────────────────

    private static string EstraiDatabase()
    {
        var assembly = typeof(DatabaseAtlante).Assembly;
        const string nomeRisorsa = "Italy.Core.data.italy.db";

        using var stream = assembly.GetManifestResourceStream(nomeRisorsa)
            ?? throw new DatabaseAtlanteException(
                $"Risorsa embedded '{nomeRisorsa}' non trovata. " +
                "Assicurarsi che italy.db sia incluso come EmbeddedResource nel progetto.");

        // Leggi tutti i byte per calcolare hash e dimensione
        var bytes = new byte[stream.Length > 0 ? stream.Length : 16 * 1024 * 1024];
        int letti = 0, n;
        while ((n = stream.Read(bytes, letti, bytes.Length - letti)) > 0) letti += n;
        var contenuto = bytes.AsSpan(0, letti);

        // Hash CRC32 semplice (XOR fold di GetHashCode su blocchi) per nome univoco
        byte[] hash;
        using (var md5 = System.Security.Cryptography.MD5.Create())
            hash = md5.ComputeHash(contenuto.ToArray());
        var hashHex = BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();

        var percorsoTemp = Path.Combine(
            Path.GetTempPath(),
            "ItalyCore",
            $"italy_{OttieniVersioneAssembly()}_{hashHex}.db");

        Directory.CreateDirectory(Path.GetDirectoryName(percorsoTemp)!);

        if (!File.Exists(percorsoTemp))
        {
            using var fileStream = File.Create(percorsoTemp);
            fileStream.Write(contenuto.ToArray(), 0, letti);
        }

        return percorsoTemp;
    }

    private static string OttieniVersioneAssembly() =>
        typeof(DatabaseAtlante).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    // ── Schema Creazione (per build pipeline) ───────────────────────────────

    /// <summary>Crea lo schema del database Atlante (usato dallo script Python di build).</summary>
    public static string SchemaSQL => """
        PRAGMA journal_mode=WAL;
        PRAGMA foreign_keys=ON;

        CREATE TABLE IF NOT EXISTS comuni (
            codice_belfiore     TEXT PRIMARY KEY,
            codice_istat        TEXT NOT NULL UNIQUE,
            denominazione       TEXT NOT NULL,
            denominazione_alt   TEXT,
            sigla_provincia     TEXT NOT NULL,
            nome_provincia      TEXT NOT NULL,
            codice_provincia    TEXT NOT NULL,
            nome_regione        TEXT NOT NULL,
            codice_regione      TEXT NOT NULL,
            ripartizione        INTEGER NOT NULL,
            is_capoluogo        INTEGER NOT NULL DEFAULT 0,
            is_citta_metro      INTEGER NOT NULL DEFAULT 0,
            is_montano          INTEGER NOT NULL DEFAULT 0,
            is_attivo           INTEGER NOT NULL DEFAULT 1,
            data_istituzione    TEXT,
            data_soppressione   TEXT,
            codice_successore   TEXT,
            cap_principale      TEXT,
            latitudine          REAL,
            longitudine         REAL,
            altitudine          REAL,
            superficie_kmq      REAL,
            popolazione         INTEGER,
            anno_rilevazione    INTEGER,
            zona_sismica        INTEGER,
            zona_climatica      TEXT,
            classe_aree_interne TEXT,
            nuts3               TEXT,
            nuts2               TEXT,
            nuts1               TEXT,
            FOREIGN KEY (codice_successore) REFERENCES comuni(codice_belfiore)
        );

        CREATE TABLE IF NOT EXISTS variazioni_storiche (
            id                  INTEGER PRIMARY KEY AUTOINCREMENT,
            codice_belfiore     TEXT NOT NULL,
            tipo_variazione     TEXT NOT NULL,
            data_variazione     TEXT NOT NULL,
            denominazione_prec  TEXT,
            denominazione_succ  TEXT,
            provincia_prec      TEXT,
            provincia_succ      TEXT,
            codici_origine      TEXT,   -- JSON array
            codici_destinazione TEXT,   -- JSON array
            riferimento_norm    TEXT,
            note                TEXT
        );

        CREATE TABLE IF NOT EXISTS cap (
            id                  INTEGER PRIMARY KEY AUTOINCREMENT,
            cap                 TEXT NOT NULL,
            codice_belfiore     TEXT NOT NULL,
            codice_istat        TEXT NOT NULL,
            descrizione_zona    TEXT,
            data_attivazione    TEXT,
            data_disattivazione TEXT
        );

        CREATE TABLE IF NOT EXISTS prefissi_telefonici (
            prefisso            TEXT PRIMARY KEY,
            tipo                TEXT NOT NULL,
            area_geografica     TEXT,
            codici_istat        TEXT,   -- JSON array
            is_attivo           INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS operatori_mobili (
            prefisso            TEXT PRIMARY KEY,
            nome_operatore      TEXT NOT NULL,
            tecnologia          TEXT NOT NULL,
            is_attivo           INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS dati_demografici (
            codice_belfiore     TEXT NOT NULL,
            anno                INTEGER NOT NULL,
            popolazione         INTEGER,
            maschi              INTEGER,
            femmine             INTEGER,
            superficie_kmq      REAL,
            PRIMARY KEY (codice_belfiore, anno)
        );

        CREATE TABLE IF NOT EXISTS meta (
            chiave              TEXT PRIMARY KEY,
            valore              TEXT NOT NULL
        );

        -- Indici per performance
        CREATE INDEX IF NOT EXISTS idx_comuni_denominazione ON comuni(denominazione);
        CREATE INDEX IF NOT EXISTS idx_comuni_provincia     ON comuni(sigla_provincia);
        CREATE INDEX IF NOT EXISTS idx_comuni_regione       ON comuni(nome_regione);
        CREATE INDEX IF NOT EXISTS idx_comuni_attivo        ON comuni(is_attivo);
        CREATE INDEX IF NOT EXISTS idx_cap_cap              ON cap(cap);
        CREATE INDEX IF NOT EXISTS idx_cap_belfiore         ON cap(codice_belfiore);
        CREATE INDEX IF NOT EXISTS idx_variazioni_belfiore  ON variazioni_storiche(codice_belfiore);
        CREATE INDEX IF NOT EXISTS idx_variazioni_data      ON variazioni_storiche(data_variazione);

        -- FTS5 per fuzzy search avanzata
        CREATE VIRTUAL TABLE IF NOT EXISTS comuni_fts USING fts5(
            codice_belfiore UNINDEXED,
            denominazione,
            denominazione_alt,
            content='comuni',
            content_rowid='rowid'
        );
        """;

    public void Dispose()
    {
        if (!_rilasciata)
        {
            _rilasciata = true;
        }
    }
}
