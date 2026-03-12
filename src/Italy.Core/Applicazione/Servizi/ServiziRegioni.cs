using Italy.Core.Infrastruttura;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>Dati di una provincia italiana.</summary>
public sealed record Provincia(
    string Sigla,
    string Nome,
    string NomeRegione,
    string CodiceISTAT,
    string CodiceNUTS3,
    int NumeroComuni);

/// <summary>Dati di una regione italiana.</summary>
public sealed record Regione(
    string Nome,
    string CodiceISTAT,
    string CodiceNUTS2,
    string CodiceNUTS1,
    int NumeroProvince,
    int NumeroComuni);

/// <summary>
/// Servizi per regioni e province italiane.
/// I dati sono derivati in tempo reale dalla tabella comuni.
/// </summary>
public sealed class ServiziRegioni
{
    private readonly DatabaseAtlante _database;

    public ServiziRegioni(DatabaseAtlante database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    // ── Province ─────────────────────────────────────────────────────────────

    /// <summary>Restituisce tutte le province italiane attive, ordinate per sigla.</summary>
    public IReadOnlyList<Provincia> TutteLeProvince()
    {
        return _database.Esegui(
            """
            SELECT sigla_provincia, nome_provincia, nome_regione,
                   codice_provincia, nuts3,
                   COUNT(*) AS num_comuni
            FROM comuni
            WHERE is_attivo = 1
            GROUP BY sigla_provincia
            ORDER BY sigla_provincia
            """,
            null,
            r =>
            {
                var ordNuts3 = r.GetOrdinal("nuts3");
                return new Provincia(
                    Sigla: r.GetString(r.GetOrdinal("sigla_provincia")),
                    Nome: r.GetString(r.GetOrdinal("nome_provincia")),
                    NomeRegione: r.GetString(r.GetOrdinal("nome_regione")),
                    CodiceISTAT: r.GetString(r.GetOrdinal("codice_provincia")),
                    CodiceNUTS3: r.IsDBNull(ordNuts3) ? "" : r.GetString(ordNuts3),
                    NumeroComuni: r.GetInt32(r.GetOrdinal("num_comuni")));
            });
    }

    /// <summary>Restituisce la provincia per sigla (es. "MI").</summary>
    public Provincia? DaSigla(string sigla)
    {
        if (string.IsNullOrWhiteSpace(sigla)) return null;
        return _database.Esegui(
            """
            SELECT sigla_provincia, nome_provincia, nome_regione,
                   codice_provincia, nuts3,
                   COUNT(*) AS num_comuni
            FROM comuni
            WHERE sigla_provincia = @s AND is_attivo = 1
            GROUP BY sigla_provincia
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@s", sigla.Trim().ToUpperInvariant()),
            r =>
            {
                var ordNuts3 = r.GetOrdinal("nuts3");
                return new Provincia(
                    Sigla: r.GetString(r.GetOrdinal("sigla_provincia")),
                    Nome: r.GetString(r.GetOrdinal("nome_provincia")),
                    NomeRegione: r.GetString(r.GetOrdinal("nome_regione")),
                    CodiceISTAT: r.GetString(r.GetOrdinal("codice_provincia")),
                    CodiceNUTS3: r.IsDBNull(ordNuts3) ? "" : r.GetString(ordNuts3),
                    NumeroComuni: r.GetInt32(r.GetOrdinal("num_comuni")));
            }).FirstOrDefault();
    }

    /// <summary>Restituisce le province di una regione.</summary>
    public IReadOnlyList<Provincia> DaRegione(string nomeRegione)
    {
        if (string.IsNullOrWhiteSpace(nomeRegione)) return Array.Empty<Provincia>();
        return _database.Esegui(
            """
            SELECT sigla_provincia, nome_provincia, nome_regione,
                   codice_provincia, nuts3,
                   COUNT(*) AS num_comuni
            FROM comuni
            WHERE nome_regione = @r AND is_attivo = 1
            GROUP BY sigla_provincia
            ORDER BY nome_provincia
            """,
            cmd => cmd.Parameters.AddWithValue("@r", nomeRegione.Trim()),
            r =>
            {
                var ordNuts3 = r.GetOrdinal("nuts3");
                return new Provincia(
                    Sigla: r.GetString(r.GetOrdinal("sigla_provincia")),
                    Nome: r.GetString(r.GetOrdinal("nome_provincia")),
                    NomeRegione: r.GetString(r.GetOrdinal("nome_regione")),
                    CodiceISTAT: r.GetString(r.GetOrdinal("codice_provincia")),
                    CodiceNUTS3: r.IsDBNull(ordNuts3) ? "" : r.GetString(ordNuts3),
                    NumeroComuni: r.GetInt32(r.GetOrdinal("num_comuni")));
            });
    }

    // ── Regioni ──────────────────────────────────────────────────────────────

    /// <summary>Restituisce tutte le regioni italiane, ordinate per nome.</summary>
    public IReadOnlyList<Regione> TutteLeRegioni()
    {
        return _database.Esegui(
            """
            SELECT nome_regione, codice_regione, nuts2, nuts1,
                   COUNT(DISTINCT sigla_provincia) AS num_province,
                   COUNT(*) AS num_comuni
            FROM comuni
            WHERE is_attivo = 1
            GROUP BY nome_regione
            ORDER BY nome_regione
            """,
            null,
            r =>
            {
                var ordNuts2 = r.GetOrdinal("nuts2");
                var ordNuts1 = r.GetOrdinal("nuts1");
                return new Regione(
                    Nome: r.GetString(r.GetOrdinal("nome_regione")),
                    CodiceISTAT: r.GetString(r.GetOrdinal("codice_regione")),
                    CodiceNUTS2: r.IsDBNull(ordNuts2) ? "" : r.GetString(ordNuts2),
                    CodiceNUTS1: r.IsDBNull(ordNuts1) ? "" : r.GetString(ordNuts1),
                    NumeroProvince: r.GetInt32(r.GetOrdinal("num_province")),
                    NumeroComuni: r.GetInt32(r.GetOrdinal("num_comuni")));
            });
    }

    /// <summary>Restituisce la regione per nome esatto.</summary>
    public Regione? DaNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return null;
        return _database.Esegui(
            """
            SELECT nome_regione, codice_regione, nuts2, nuts1,
                   COUNT(DISTINCT sigla_provincia) AS num_province,
                   COUNT(*) AS num_comuni
            FROM comuni
            WHERE nome_regione = @n AND is_attivo = 1
            GROUP BY nome_regione
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@n", nome.Trim()),
            r =>
            {
                var ordNuts2 = r.GetOrdinal("nuts2");
                var ordNuts1 = r.GetOrdinal("nuts1");
                return new Regione(
                    Nome: r.GetString(r.GetOrdinal("nome_regione")),
                    CodiceISTAT: r.GetString(r.GetOrdinal("codice_regione")),
                    CodiceNUTS2: r.IsDBNull(ordNuts2) ? "" : r.GetString(ordNuts2),
                    CodiceNUTS1: r.IsDBNull(ordNuts1) ? "" : r.GetString(ordNuts1),
                    NumeroProvince: r.GetInt32(r.GetOrdinal("num_province")),
                    NumeroComuni: r.GetInt32(r.GetOrdinal("num_comuni")));
            }).FirstOrDefault();
    }

    /// <summary>Restituisce la regione per codice NUTS2 (es. "ITC4" = Lombardia).</summary>
    public Regione? DaCodiceNUTS2(string nuts2)
    {
        if (string.IsNullOrWhiteSpace(nuts2)) return null;
        return _database.Esegui(
            """
            SELECT nome_regione, codice_regione, nuts2, nuts1,
                   COUNT(DISTINCT sigla_provincia) AS num_province,
                   COUNT(*) AS num_comuni
            FROM comuni
            WHERE nuts2 = @n AND is_attivo = 1
            GROUP BY nome_regione
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@n", nuts2.Trim().ToUpperInvariant()),
            r =>
            {
                var ordNuts2 = r.GetOrdinal("nuts2");
                var ordNuts1 = r.GetOrdinal("nuts1");
                return new Regione(
                    Nome: r.GetString(r.GetOrdinal("nome_regione")),
                    CodiceISTAT: r.GetString(r.GetOrdinal("codice_regione")),
                    CodiceNUTS2: r.IsDBNull(ordNuts2) ? "" : r.GetString(ordNuts2),
                    CodiceNUTS1: r.IsDBNull(ordNuts1) ? "" : r.GetString(ordNuts1),
                    NumeroProvince: r.GetInt32(r.GetOrdinal("num_province")),
                    NumeroComuni: r.GetInt32(r.GetOrdinal("num_comuni")));
            }).FirstOrDefault();
    }
}
