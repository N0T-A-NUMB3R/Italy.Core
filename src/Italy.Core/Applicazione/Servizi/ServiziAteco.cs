using Italy.Core.Infrastruttura;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Codice ATECO con descrizione e posizione nella gerarchia.
/// </summary>
public sealed record CodiceAteco(
    string Codice,
    string Descrizione,
    string Livello,
    string? CodicePadre);

/// <summary>
/// Servizi per la classificazione ATECO 2007 delle attività economiche.
///
/// Gerarchia: Sezione → Divisione → Gruppo → Classe
/// Es: "C" (Sezione) → "10" (Divisione) → "10.1" (Gruppo) → "10.11" (Classe)
///
/// Fonte: ISTAT - Classificazione delle attività economiche ATECO 2007
/// aggiornamento 2022 (integrazione con Nace Rev. 2.1).
/// </summary>
public sealed class ServiziAteco
{
    private readonly DatabaseAtlante _database;

    public ServiziAteco(DatabaseAtlante database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    // ── Lookup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce un codice ATECO dato il codice esatto.
    /// Es: DaCodice("10.11") → { Codice: "10.11", Descrizione: "Produzione di carne...", Livello: "Classe", CodicePadre: "10.1" }
    /// </summary>
    public CodiceAteco? DaCodice(string codice)
    {
        var risultati = _database.Esegui(
            "SELECT codice, descrizione, livello, codice_padre FROM ateco WHERE codice = @c LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@c", codice.Trim()),
            MappaCodiceAteco);

        return risultati.FirstOrDefault();
    }

    // ── Ricerca ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Cerca codici ATECO per testo nella descrizione (case-insensitive, LIKE %testo%).
    /// </summary>
    public IReadOnlyList<CodiceAteco> Cerca(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return Array.Empty<CodiceAteco>();

        return _database.Esegui(
            """
            SELECT codice, descrizione, livello, codice_padre
            FROM ateco
            WHERE descrizione LIKE @q
            ORDER BY livello, codice
            LIMIT 50
            """,
            cmd => cmd.Parameters.AddWithValue("@q", $"%{testo.Trim()}%"),
            MappaCodiceAteco);
    }

    // ── Navigazione Gerarchica ────────────────────────────────────────────────

    /// <summary>
    /// Restituisce i figli diretti di un codice padre.
    /// Es: SottoCategorie("10") → Gruppi 10.1, 10.2, 10.3, ...
    /// </summary>
    public IReadOnlyList<CodiceAteco> SottoCategorie(string codicePadre)
    {
        if (string.IsNullOrWhiteSpace(codicePadre)) return Array.Empty<CodiceAteco>();

        return _database.Esegui(
            """
            SELECT codice, descrizione, livello, codice_padre
            FROM ateco
            WHERE codice_padre = @p
            ORDER BY codice
            """,
            cmd => cmd.Parameters.AddWithValue("@p", codicePadre.Trim()),
            MappaCodiceAteco);
    }

    /// <summary>
    /// Restituisce tutte le Sezioni ATECO (livello radice, es: A, B, C, ..., U).
    /// </summary>
    public IReadOnlyList<CodiceAteco> Sezioni()
    {
        return _database.Esegui(
            """
            SELECT codice, descrizione, livello, codice_padre
            FROM ateco
            WHERE livello = 'Sezione'
            ORDER BY codice
            """,
            null,
            MappaCodiceAteco);
    }

    // ── Descrizione Completa ─────────────────────────────────────────────────

    /// <summary>
    /// Restituisce la catena gerarchica completa come stringa.
    /// Es: DescrizioneCompleta("10.11") → "C > 10 > 10.1 > 10.11"
    /// Restituisce null se il codice non esiste.
    /// </summary>
    public string? DescrizioneCompleta(string codice)
    {
        var corrente = DaCodice(codice);
        if (corrente == null) return null;

        var catena = new List<string> { corrente.Codice };
        var visitati = new HashSet<string>(StringComparer.Ordinal) { corrente.Codice };

        while (corrente.CodicePadre != null)
        {
            if (visitati.Contains(corrente.CodicePadre)) break; // protezione da cicli
            var padre = DaCodice(corrente.CodicePadre);
            if (padre == null) break;

            visitati.Add(padre.Codice);
            catena.Insert(0, padre.Codice);
            corrente = padre;
        }

        return string.Join(" > ", catena);
    }

    // ── Helper Privati ────────────────────────────────────────────────────────

    private static CodiceAteco MappaCodiceAteco(Microsoft.Data.Sqlite.SqliteDataReader r)
    {
        var ordPadre = r.GetOrdinal("codice_padre");
        return new CodiceAteco(
            Codice: r.GetString(r.GetOrdinal("codice")),
            Descrizione: r.GetString(r.GetOrdinal("descrizione")),
            Livello: r.GetString(r.GetOrdinal("livello")),
            CodicePadre: r.IsDBNull(ordPadre) ? null : r.GetString(ordPadre));
    }
}
