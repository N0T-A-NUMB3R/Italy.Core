using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;
using Italy.Core.Infrastruttura;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi per la Pubblica Amministrazione italiana.
///
/// Funzionalità:
/// - Lookup codici IPA (Indice PA) per fatturazione elettronica B2G
/// - Codici SdI (Sistema di Interscambio) per invio fatture alla PA
/// - Aggregazioni sovracomunali (ASL, Comunità Montane, ATO)
/// - Codici INPS/INAIL per sede di competenza territoriale
///
/// Fonte dati IPA: https://indicepa.gov.it (API pubblica, aggiornamento giornaliero)
/// Il database Atlante include uno snapshot mensile dell'IndicePA.
/// </summary>
public sealed class ServiziPA
{
    private readonly IRepositoryComuni _repositoryComuni;
    private readonly DatabaseAtlante? _database;

    public ServiziPA(IRepositoryComuni repositoryComuni, DatabaseAtlante? database = null)
    {
        _repositoryComuni = repositoryComuni;
        _database = database;
    }

    // ── IPA / SdI ────────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce il codice IPA e SdI del comune per la fatturazione elettronica B2G.
    ///
    /// Esempio:
    /// <code>
    /// var ipa = atlante.PA.OttieniCodiceIPA("F205");
    /// // → { CodiceIPAUnivoco: "c_f205", CodiceSdI: "UFOVS8", NomeEnte: "Comune di Milano" }
    /// </code>
    /// </summary>
    public CodiceIPA? OttieniCodiceIPA(string codiceBelfiore)
    {
        if (_database == null) return GeneraCodiceIPADaConvenzione(codiceBelfiore);

        var risultati = _database.Esegui(
            "SELECT * FROM codici_ipa WHERE codice_belfiore = @cb LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()),
            r => new CodiceIPA
            {
                CodiceBelfiore = r.GetString(r.GetOrdinal("codice_belfiore")),
                NomeEnte = r.GetString(r.GetOrdinal("nome_ente")),
                CodiceIPAUnivoco = r.GetString(r.GetOrdinal("codice_ipa")),
                CodiceSdI = r.GetString(r.GetOrdinal("codice_sdi")),
                PEC = r.IsDBNull(r.GetOrdinal("pec")) ? null : r.GetString(r.GetOrdinal("pec")),
                CodiceFiscaleEnte = r.IsDBNull(r.GetOrdinal("cf_ente")) ? null : r.GetString(r.GetOrdinal("cf_ente")),
                TipoEnte = (TipoEnteIPA)Enum.Parse(typeof(TipoEnteIPA), r.GetString(r.GetOrdinal("tipo_ente")))
            });

        return risultati.FirstOrDefault() ?? GeneraCodiceIPADaConvenzione(codiceBelfiore);
    }

    /// <summary>
    /// Cerca un ente PA per nome (fuzzy) e restituisce i codici IPA/SdI.
    /// </summary>
    public IReadOnlyList<CodiceIPA> CercaEntePA(string nomeEnte)
    {
        if (_database == null) return Array.Empty<CodiceIPA>();

        return _database.Esegui(
            """
            SELECT * FROM codici_ipa
            WHERE nome_ente LIKE @q
            ORDER BY tipo_ente, nome_ente
            LIMIT 20
            """,
            cmd => cmd.Parameters.AddWithValue("@q", $"%{nomeEnte.Trim()}%"),
            r => new CodiceIPA
            {
                CodiceBelfiore = r.GetString(r.GetOrdinal("codice_belfiore")),
                NomeEnte = r.GetString(r.GetOrdinal("nome_ente")),
                CodiceIPAUnivoco = r.GetString(r.GetOrdinal("codice_ipa")),
                CodiceSdI = r.GetString(r.GetOrdinal("codice_sdi")),
            });
    }

    // ── Aggregazioni Sovracomunali ────────────────────────────────────────────

    /// <summary>
    /// Restituisce le aggregazioni sovracomunali di un comune:
    /// ASL, Comunità Montana, ATO (Acqua, Rifiuti), Distretto Scolastico, Tribunale.
    /// </summary>
    public AggregazioniSovracomunali? OttieniAggregazioni(string codiceBelfiore)
    {
        if (_database == null)
            return CostruisciAggregazioniDaConvenzione(codiceBelfiore);

        var risultati = _database.Esegui(
            "SELECT * FROM aggregazioni_sovracomunali WHERE codice_belfiore = @cb LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()),
            r => new AggregazioniSovracomunali
            {
                CodiceBelfiore = r.GetString(r.GetOrdinal("codice_belfiore")),
                CodiceASL = LeggiNullabile(r, "codice_asl"),
                NomeASL = LeggiNullabile(r, "nome_asl"),
                ComuneMontanaNome = LeggiNullabile(r, "comunita_montana"),
                IsComuneMontano = r.GetInt32(r.GetOrdinal("is_montano")) == 1,
                UnioneComuni = LeggiNullabile(r, "unione_comuni"),
                ATOAcqua = LeggiNullabile(r, "ato_acqua"),
                ATORifiuti = LeggiNullabile(r, "ato_rifiuti"),
                DistrettoScolastico = LeggiNullabile(r, "distretto_scolastico"),
                TribunaleCompetente = LeggiNullabile(r, "tribunale"),
                CCIAA = LeggiNullabile(r, "cciaa"),
            });

        return risultati.FirstOrDefault() ?? CostruisciAggregazioniDaConvenzione(codiceBelfiore);
    }

    /// <summary>
    /// Restituisce l'ASL competente per un comune.
    /// </summary>
    public (string? CodiceASL, string? NomeASL) OttieniASL(string codiceBelfiore)
    {
        var agg = OttieniAggregazioni(codiceBelfiore);
        return (agg?.CodiceASL, agg?.NomeASL);
    }

    // ── Codici INPS/INAIL ────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce il codice sede INPS di competenza per un comune.
    /// Utile per la compilazione di DM10, UNIEMENS, CUD, ecc.
    /// </summary>
    public string? OttieniSedeINPS(string codiceBelfiore)
    {
        if (_database == null) return null;
        return _database.EseguiScalare<string?>(
            "SELECT codice_sede_inps FROM aggregazioni_sovracomunali WHERE codice_belfiore = @cb",
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()));
    }

    /// <summary>
    /// Restituisce il codice sede INAIL di competenza.
    /// </summary>
    public string? OttieniSedeINAIL(string codiceBelfiore)
    {
        if (_database == null) return null;
        return _database.EseguiScalare<string?>(
            "SELECT codice_sede_inail FROM aggregazioni_sovracomunali WHERE codice_belfiore = @cb",
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()));
    }

    // ── Helper Privati ────────────────────────────────────────────────────────

    /// <summary>
    /// Genera un codice IPA deterministico dalla convenzione di naming IPA
    /// quando il DB non ha il record esatto.
    /// Convenzione: "c_" + codice_belfiore_lowercase (per i comuni).
    /// </summary>
    private CodiceIPA? GeneraCodiceIPADaConvenzione(string codiceBelfiore)
    {
        var comune = _repositoryComuni.DaCodiceBelfiore(codiceBelfiore.ToUpperInvariant());
        if (comune == null) return null;

        var codiceConvenzionale = $"c_{codiceBelfiore.ToLowerInvariant()}";
        return new CodiceIPA
        {
            CodiceBelfiore = codiceBelfiore,
            NomeEnte = $"Comune di {comune.DenominazioneUfficiale}",
            CodiceIPAUnivoco = codiceConvenzionale,
            CodiceSdI = "0000000",  // Il codice SdI reale richiede il DB IPA aggiornato
            TipoEnte = comune.IsCapoluogoProvincia ? TipoEnteIPA.ComuneCapoluogo : TipoEnteIPA.Comune,
        };
    }

    private AggregazioniSovracomunali CostruisciAggregazioniDaConvenzione(string codiceBelfiore)
    {
        var comune = _repositoryComuni.DaCodiceBelfiore(codiceBelfiore.ToUpperInvariant());
        return new AggregazioniSovracomunali
        {
            CodiceBelfiore = codiceBelfiore,
            IsComuneMontano = comune?.IsComuneMontano ?? false
        };
    }

    private static string? LeggiNullabile(Microsoft.Data.Sqlite.SqliteDataReader r, string col)
    {
        var ord = r.GetOrdinal(col);
        return r.IsDBNull(ord) ? null : r.GetString(ord);
    }
}
