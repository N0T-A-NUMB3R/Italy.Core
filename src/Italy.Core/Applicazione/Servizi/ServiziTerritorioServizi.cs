using Italy.Core.Domain.Entità;
using Italy.Core.Infrastruttura;
using Microsoft.Data.Sqlite;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi per farmacie e impianti carburante sul territorio italiano.
///
/// Fonti:
/// - Farmacie: Ministero della Salute (aggiornamento settimanale, ~20.750 farmacie attive)
/// - Impianti Carburante: MIMIT (aggiornamento mensile, ~23.574 impianti)
/// </summary>
public sealed class ServiziTerritorioServizi
{
    private readonly DatabaseAtlante _database;

    public ServiziTerritorioServizi(DatabaseAtlante database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    // ── Farmacie ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce le farmacie attive nel comune dato (per codice ISTAT a 6 cifre).
    /// </summary>
    public IReadOnlyList<Farmacia> FarmacieDaCodiceISTAT(string codiceISTAT)
    {
        if (string.IsNullOrWhiteSpace(codiceISTAT)) return Array.Empty<Farmacia>();

        return _database.Esegui(
            """
            SELECT cod_farmacia, denominazione, indirizzo, cap, comune, frazione,
                   sigla_provincia, provincia, regione, cod_comune_istat, tipologia,
                   latitudine, longitudine
            FROM farmacie
            WHERE cod_comune_istat = @istat
            ORDER BY denominazione
            """,
            cmd => cmd.Parameters.AddWithValue("@istat", codiceISTAT.Trim()),
            MappaFarmacia);
    }

    /// <summary>
    /// Restituisce le farmacie nella provincia data (sigla, es. "MI").
    /// </summary>
    public IReadOnlyList<Farmacia> FarmacieDaProvincia(string siglaProvincia)
    {
        if (string.IsNullOrWhiteSpace(siglaProvincia)) return Array.Empty<Farmacia>();

        return _database.Esegui(
            """
            SELECT cod_farmacia, denominazione, indirizzo, cap, comune, frazione,
                   sigla_provincia, provincia, regione, cod_comune_istat, tipologia,
                   latitudine, longitudine
            FROM farmacie
            WHERE sigla_provincia = @prov
            ORDER BY comune, denominazione
            """,
            cmd => cmd.Parameters.AddWithValue("@prov", siglaProvincia.Trim().ToUpperInvariant()),
            MappaFarmacia);
    }

    // ── Impianti Carburante ───────────────────────────────────────────────────

    /// <summary>
    /// Restituisce gli impianti carburante nel comune e provincia dati.
    /// Il matching avviene per nome comune (case-insensitive) e sigla provincia.
    /// </summary>
    public IReadOnlyList<ImpiantoCarburante> ImpiantiDaComune(string comune, string siglaProvincia)
    {
        if (string.IsNullOrWhiteSpace(comune)) return Array.Empty<ImpiantoCarburante>();

        return _database.Esegui(
            """
            SELECT id_impianto, gestore, bandiera, tipo_impianto, nome_impianto,
                   indirizzo, comune, sigla_provincia, latitudine, longitudine
            FROM impianti_carburante
            WHERE UPPER(comune) = UPPER(@comune)
              AND sigla_provincia = @prov
            ORDER BY bandiera, nome_impianto
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@comune", comune.Trim());
                cmd.Parameters.AddWithValue("@prov", siglaProvincia.Trim().ToUpperInvariant());
            },
            MappaImpianto);
    }

    /// <summary>
    /// Restituisce tutti gli impianti carburante nella provincia data.
    /// </summary>
    public IReadOnlyList<ImpiantoCarburante> ImpiantiDaProvincia(string siglaProvincia)
    {
        if (string.IsNullOrWhiteSpace(siglaProvincia)) return Array.Empty<ImpiantoCarburante>();

        return _database.Esegui(
            """
            SELECT id_impianto, gestore, bandiera, tipo_impianto, nome_impianto,
                   indirizzo, comune, sigla_provincia, latitudine, longitudine
            FROM impianti_carburante
            WHERE sigla_provincia = @prov
            ORDER BY comune, bandiera
            """,
            cmd => cmd.Parameters.AddWithValue("@prov", siglaProvincia.Trim().ToUpperInvariant()),
            MappaImpianto);
    }

    // ── Mapper privati ────────────────────────────────────────────────────────

    private static Farmacia MappaFarmacia(SqliteDataReader r)
    {
        var oCod   = r.GetOrdinal("cod_farmacia");
        var oDen   = r.GetOrdinal("denominazione");
        var oCom   = r.GetOrdinal("comune");
        var oProv  = r.GetOrdinal("sigla_provincia");
        return new()
        {
            CodFarmacia    = r.IsDBNull(oCod)  ? 0  : r.GetInt32(oCod),
            Denominazione  = r.IsDBNull(oDen)  ? "" : r.GetString(oDen),
            Indirizzo      = Leggi(r, "indirizzo"),
            CAP            = Leggi(r, "cap"),
            Comune         = r.IsDBNull(oCom)  ? "" : r.GetString(oCom),
            Frazione       = Leggi(r, "frazione"),
            SiglaProvincia = r.IsDBNull(oProv) ? "" : r.GetString(oProv),
            Provincia      = Leggi(r, "provincia"),
            Regione        = Leggi(r, "regione"),
            CodComuneISTAT = Leggi(r, "cod_comune_istat"),
            Tipologia      = Leggi(r, "tipologia"),
            Latitudine     = LeggiDouble(r, "latitudine"),
            Longitudine    = LeggiDouble(r, "longitudine"),
        };
    }

    private static ImpiantoCarburante MappaImpianto(SqliteDataReader r)
    {
        var oId   = r.GetOrdinal("id_impianto");
        var oCom  = r.GetOrdinal("comune");
        var oProv = r.GetOrdinal("sigla_provincia");
        return new()
        {
            IdImpianto     = r.IsDBNull(oId)   ? 0  : r.GetInt32(oId),
            Gestore        = Leggi(r, "gestore"),
            Bandiera       = Leggi(r, "bandiera"),
            TipoImpianto   = Leggi(r, "tipo_impianto"),
            NomeImpianto   = Leggi(r, "nome_impianto"),
            Indirizzo      = Leggi(r, "indirizzo"),
            Comune         = r.IsDBNull(oCom)  ? "" : r.GetString(oCom),
            SiglaProvincia = r.IsDBNull(oProv) ? "" : r.GetString(oProv),
            Latitudine     = LeggiDouble(r, "latitudine"),
            Longitudine    = LeggiDouble(r, "longitudine"),
        };
    }

    private static string? Leggi(SqliteDataReader r, string col)
    {
        try
        {
            var o = r.GetOrdinal(col);
            return r.IsDBNull(o) ? null : r.GetString(o);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static double? LeggiDouble(SqliteDataReader r, string col)
    {
        try
        {
            var o = r.GetOrdinal(col);
            return r.IsDBNull(o) ? null : r.GetDouble(o);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
