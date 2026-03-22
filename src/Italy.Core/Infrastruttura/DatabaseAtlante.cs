using Microsoft.Data.Sqlite;
using System.Reflection;
using System.IO;
using Italy.Core.Domain.Entità;

namespace Italy.Core.Infrastruttura;

/// <summary>
/// Gestisce l'accesso al database SQLite embedded "italy.db".
/// Strategia di caricamento a cascata:
///   1. Estrae il DB su disco in %TEMP%\ItalyCore\ (percorso stabile, riutilizzabile tra avvii)
///   2. Se la scrittura su disco fallisce (permessi, container read-only, disco pieno),
///      carica il DB direttamente in memoria tramite SQLite Backup API (fallback in-memory).
/// L'accesso è esclusivamente in modalità Read-Only (o shared-memory) per thread-safety.
/// </summary>
public sealed class DatabaseAtlante : IDisposable
{
    private static readonly object _lock = new();
    private static string? _percorsoDb;           // percorso file su disco (null se in-memory)
    private static SqliteConnection? _inMemoryConn; // connessione persistente se in-memory
    private bool _rilasciata;

    /// <summary>True se il DB è stato caricato in memoria (fallback).</summary>
    public static bool IsInMemory => _inMemoryConn != null;

    // Stringa di connessione: file read-only oppure memoria condivisa
    public string StringaConnessione =>
        _inMemoryConn != null
            ? "Data Source=italante_mem;Mode=Memory;Cache=Shared;"
            : $"Data Source={_percorsoDb};Mode=ReadOnly;Cache=Shared;";

    private void EnsureInitialized()
    {
        if (_percorsoDb != null || _inMemoryConn != null) return;
        lock (_lock)
        {
            if (_percorsoDb != null || _inMemoryConn != null) return;
            InizializzaDatabase();
        }
    }

    /// <summary>Crea una nuova connessione al database Atlante.</summary>
    public SqliteConnection ApriConnessione()
    {
        EnsureInitialized();
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

    // ── Inizializzazione con fallback in-memory ──────────────────────────────

    private static void InizializzaDatabase()
    {
        var assembly = typeof(DatabaseAtlante).Assembly;
        const string nomeRisorsa = "Italy.Core.data.italy.db";

        using var stream = assembly.GetManifestResourceStream(nomeRisorsa)
            ?? throw new DatabaseAtlanteException(
                $"Risorsa embedded '{nomeRisorsa}' non trovata. " +
                "Assicurarsi che italy.db sia incluso come EmbeddedResource nel progetto.");

        // Leggi tutti i byte
        var bytes = new byte[stream.Length > 0 ? stream.Length : 20 * 1024 * 1024];
        int letti = 0, n;
        while ((n = stream.Read(bytes, letti, bytes.Length - letti)) > 0) letti += n;
        var contenuto = bytes.AsSpan(0, letti).ToArray();

        // Hash per nome file univoco per versione
        byte[] hash;
        using (var md5 = System.Security.Cryptography.MD5.Create())
            hash = md5.ComputeHash(contenuto);
        var hashHex = BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();

        var percorsoTemp = Path.Combine(
            Path.GetTempPath(),
            "ItalyCore",
            $"italy_{OttieniVersioneAssembly()}_{hashHex}.db");

        // Strategia 1: scrittura su disco
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(percorsoTemp)!);
            if (!File.Exists(percorsoTemp))
                File.WriteAllBytes(percorsoTemp, contenuto);
            _percorsoDb = percorsoTemp;
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Disco non scrivibile — fallback in-memory
        }

        // Strategia 2: DB in memoria condivisa (SQLite Backup API)
        // La connessione viene mantenuta aperta per tutta la vita dell'app:
        // SQLite distrugge i DB :memory: alla chiusura dell'ultima connessione.
        var connMem = new SqliteConnection("Data Source=italante_mem;Mode=Memory;Cache=Shared;");
        connMem.Open();

        // Carica i byte nel DB in-memory tramite un file temporaneo virtuale
        // SQLite non supporta LoadExtension da byte[], ma supporta backup da altra connessione
        var tmpPath = Path.Combine(Path.GetTempPath(), $"italycore_init_{Guid.NewGuid():N}.db");
        try
        {
            File.WriteAllBytes(tmpPath, contenuto);
            using var connFile = new SqliteConnection($"Data Source={tmpPath};Mode=ReadOnly;");
            connFile.Open();
            connFile.BackupDatabase(connMem);
        }
        finally
        {
            try { File.Delete(tmpPath); } catch { /* ignora */ }
        }

        _inMemoryConn = connMem;
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
            // La connessione in-memory è statica (condivisa tra istanze) — non la chiudiamo
            // finché il processo è in vita, altrimenti SQLite distrugge il DB.
        }
    }
}
