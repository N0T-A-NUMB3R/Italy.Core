using System.Text.RegularExpressions;
using Italy.Core.Infrastruttura;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Dati identificativi di una banca italiana.
/// </summary>
public sealed record Banca(
    string CodiceABI,
    string NomeBanca,
    string? CodiceBIC,
    string? ComuneSede,
    string? ProvinciaSede);

/// <summary>
/// Servizi per la lookup e validazione di banche italiane.
///
/// Copre:
/// - Ricerca per codice ABI (5 cifre, es: "03069" = Intesa Sanpaolo)
/// - Ricerca per codice BIC/SWIFT (8 o 11 caratteri, es: "BCITITMM")
/// - Validazione formato BIC per l'Italia (prefisso paese "IT")
///
/// Fonte dati: Registro dei codici ABI/BIC — Banca d'Italia.
/// </summary>
public sealed class ServiziBanche
{
    // Pattern BIC italiano: 4 lettere istituto + "IT" + 2 alfanumerico + 3 alfanumerico opzionali
    private static readonly Regex _regexBIC =
        new(@"^[A-Z]{4}IT[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.Compiled);

    private readonly DatabaseAtlante _database;

    public ServiziBanche(DatabaseAtlante database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    // ── Lookup per BIC ────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce la banca dato il codice BIC/SWIFT.
    /// Se il BIC è di 11 caratteri lo normalizza a 8 (elimina il codice filiale).
    /// Es: DaBIC("BCITITMMXXX") → stessa banca di DaBIC("BCITITMM")
    /// </summary>
    public Banca? DaBIC(string bic)
    {
        if (string.IsNullOrWhiteSpace(bic)) return null;

        var bicNormalizzato = NormalizzaBIC(bic.Trim().ToUpperInvariant());

        var risultati = _database.Esegui(
            """
            SELECT codice_abi, nome_banca, codice_bic, comune_sede, provincia_sede
            FROM banche
            WHERE UPPER(SUBSTR(codice_bic, 1, 8)) = @bic
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@bic", bicNormalizzato),
            MappaBanca);

        return risultati.FirstOrDefault();
    }

    // ── Lookup per ABI ────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce la banca dato il codice ABI (5 cifre).
    /// Es: DaABI("03069") → { NomeBanca: "Intesa Sanpaolo", ... }
    /// </summary>
    public Banca? DaABI(string abi)
    {
        if (string.IsNullOrWhiteSpace(abi)) return null;

        var risultati = _database.Esegui(
            """
            SELECT codice_abi, nome_banca, codice_bic, comune_sede, provincia_sede
            FROM banche
            WHERE codice_abi = @abi
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@abi", abi.Trim()),
            MappaBanca);

        return risultati.FirstOrDefault();
    }

    // ── Ricerca per Nome ─────────────────────────────────────────────────────

    /// <summary>
    /// Cerca banche per nome (LIKE %nome%, case-insensitive).
    /// Es: Cerca("Intesa") → tutte le banche con "Intesa" nel nome.
    /// </summary>
    public IReadOnlyList<Banca> Cerca(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return Array.Empty<Banca>();

        return _database.Esegui(
            """
            SELECT codice_abi, nome_banca, codice_bic, comune_sede, provincia_sede
            FROM banche
            WHERE nome_banca LIKE @q
              AND is_attivo = 1
            ORDER BY nome_banca
            LIMIT 50
            """,
            cmd => cmd.Parameters.AddWithValue("@q", $"%{nome.Trim()}%"),
            MappaBanca);
    }

    // ── Validazione BIC ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifica che il BIC rispetti il formato italiano:
    /// 4 lettere (istituto) + "IT" + 2 alfanumerici + 3 alfanumerici opzionali (filiale).
    /// Accetta sia la forma a 8 caratteri (BIC8) sia quella a 11 (BIC11 con codice filiale).
    /// Es: ValidaBIC("BCITITMM") → true
    ///     ValidaBIC("BCITITMMXXX") → true
    ///     ValidaBIC("BCITDEFF") → false (DE = Germania, non IT)
    /// </summary>
    public bool ValidaBIC(string bic)
    {
        if (string.IsNullOrWhiteSpace(bic)) return false;
        var normalizzato = bic.Trim().ToUpperInvariant();
        return _regexBIC.IsMatch(normalizzato);
    }

    // ── Helper Privati ────────────────────────────────────────────────────────

    /// <summary>
    /// Normalizza un BIC a 8 caratteri rimuovendo il codice filiale (ultimi 3 char).
    /// Se il BIC è già di 8 caratteri lo restituisce invariato.
    /// </summary>
    private static string NormalizzaBIC(string bic)
    {
        return bic.Length == 11 ? bic[..8] : bic;
    }

    private static Banca MappaBanca(Microsoft.Data.Sqlite.SqliteDataReader r)
    {
        var ordBic = r.GetOrdinal("codice_bic");
        var ordComune = r.GetOrdinal("comune_sede");
        var ordProv = r.GetOrdinal("provincia_sede");
        return new Banca(
            CodiceABI: r.GetString(r.GetOrdinal("codice_abi")),
            NomeBanca: r.GetString(r.GetOrdinal("nome_banca")),
            CodiceBIC: r.IsDBNull(ordBic) ? null : r.GetString(ordBic),
            ComuneSede: r.IsDBNull(ordComune) ? null : r.GetString(ordComune),
            ProvinciaSede: r.IsDBNull(ordProv) ? null : r.GetString(ordProv));
    }
}
