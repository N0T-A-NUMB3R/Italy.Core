using Italy.Core.Applicazione.Servizi;
using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;
using Italy.Core.Infrastruttura;
using Italy.Core.Infrastruttura.Repository;

namespace Italy.Core;

/// <summary>
/// Punto di ingresso principale della libreria Italy.Core.
/// Fornisce accesso a tutti i servizi geografici, amministrativi e fiscali italiani.
///
/// Utilizzo base:
/// <code>
/// var atlante = new Atlante();
/// var comuni  = atlante.Comuni.Cerca("Mialno");           // → Milano (fuzzy)
/// var succ    = atlante.Comuni.OttieniSuccessore("C619"); // → Corigliano-Rossano
/// var r       = atlante.Comuni.RisolviCodiceISTATStorico("076020");
/// var addr    = atlante.Parser.Analizza("Via Roma 10, 17025 Loano (SV)");
/// var report  = atlante.Bonifica.ElaboraBatch(records);
/// var front   = atlante.Frontalieri.OttieniInfoFrontalieri("A327");
/// var ipa     = atlante.PA.OttieniCodiceIPA("F205");
/// </code>
/// </summary>
public sealed class Atlante : IDisposable
{
    private readonly DatabaseAtlante _database;
    private bool _rilasciato;

    // ── Servizi Principali ───────────────────────────────────────────────────

    /// <summary>Ricerca e navigazione comuni. Include risoluzione storica e codice ISTAT vecchio.</summary>
    public ServiziComuni Comuni { get; }

    /// <summary>CAP singoli, multi-CAP, ricerca inversa, storici.</summary>
    public ServiziCAP CAP { get; }

    // ── Servizi Fiscali ──────────────────────────────────────────────────────

    /// <summary>Codice Fiscale persone fisiche: validazione, calcolo, lookup comune nascita.</summary>
    public ServiziCodiceFiscale Fiscale { get; }

    /// <summary>Codice Fiscale persone giuridiche (società, enti, PA).</summary>
    public ServiziCodiceFiscalePG FiscalePG { get; }

    /// <summary>Partita IVA, IBAN, Targa automobilistica.</summary>
    public ServiziValidazione Validazione { get; }

    // ── Servizi Geografici ───────────────────────────────────────────────────

    /// <summary>Coordinate, distanze Haversine, comuni nel raggio.</summary>
    public ServiziGeo Geo { get; }

    /// <summary>Prefissi telefonici fissi, mobili, identificazione operatore.</summary>
    public ServiziTelefonia Telefonia { get; }

    // ── Servizi Temporali ────────────────────────────────────────────────────

    /// <summary>Festività nazionali, Santi Patroni, Pasqua, giorni lavorativi.</summary>
    public ServiziFestività Calendario { get; }

    // ── Servizi Pulizia Dati ─────────────────────────────────────────────────

    /// <summary>
    /// Parser indirizzi italiani formato libero.
    /// "P.ZZA GARIBALDI 5/A - SAVONA 17100" → {Toponimo, NomeVia, Civico, CAP, Comune, ...}
    /// </summary>
    public ServiziParserIndirizzi Parser { get; }

    /// <summary>
    /// Bonifica database legacy: comuni rinominati, CAP errati, sigle obsolete, CF incoerenti.
    /// Supporta elaborazione batch con report.
    /// </summary>
    public ServiziBonificaDati Bonifica { get; }

    // ── Servizi Lavoratori ───────────────────────────────────────────────────

    /// <summary>
    /// Verifica zona frontaliera (Svizzera/Francia/Austria/Slovenia entro 20km).
    /// Regime D.Lgs. 209/2023 per frontalieri svizzeri.
    /// </summary>
    public ServiziFrontalieri Frontalieri { get; }

    // ── Servizi Pubblica Amministrazione ─────────────────────────────────────

    /// <summary>
    /// Codici IPA/SdI per fatturazione elettronica B2G.
    /// ASL, Comunità Montane, ATO, sedi INPS/INAIL di competenza.
    /// </summary>
    public ServiziPA PA { get; }

    // ── Time Machine ─────────────────────────────────────────────────────────

    /// <summary>
    /// Risoluzione storica deterministica.
    /// "Esisteva 'Corigliano' il 1/1/1980?" → sì.
    /// "Qual era il Belfiore valido per CF nati a Corigliano nel 1980?" → C619.
    /// </summary>
    public ServiziTimeMachine TimeMachine { get; }

    // ── Validazione Cross-Modulo ──────────────────────────────────────────────

    /// <summary>
    /// Validazioni che combinano più fonti: CF vs Comune, IBAN vs Territorio,
    /// CAP + Comune + Provincia triple-check.
    /// </summary>
    public ServiziValidazioneCross ValidazioneCross { get; }

    // ── Confronto Indirizzi ───────────────────────────────────────────────────

    /// <summary>
    /// Confronto intelligente tra due indirizzi italiani con scoring multi-dimensionale.
    /// Pre-normalizza tramite il parser DB, poi calcola score su via, civico, CAP e città.
    /// </summary>
    public ServiziConfrontoIndirizzi Confronto { get; }

    // ── Nuovi Servizi ─────────────────────────────────────────────────────────

    /// <summary>
    /// Codici ATECO 2007 (agg. 2022): lookup, ricerca, gerarchia Sezione→Classe.
    /// </summary>
    public ServiziAteco ATECO { get; }

    /// <summary>
    /// Banche italiane: lookup per BIC/ABI, ricerca per nome, validazione BIC.
    /// Fonte: GLEIF BIC-LEI mapping.
    /// </summary>
    public ServiziBanche Banche { get; }

    /// <summary>
    /// Zone territoriali per comune: zona sismica (1-4), zona climatica (A-F),
    /// coordinate WGS84. Fonte: Protezione Civile + DPR 412/93.
    /// </summary>
    public ServiziZoneTerritoriali ZoneTerritoriali { get; }

    /// <summary>
    /// Regioni e province italiane: elenco, lookup per nome/sigla/NUTS2,
    /// conteggio comuni e province per regione.
    /// </summary>
    public ServiziRegioni Regioni { get; }

    /// <summary>
    /// Distanza Haversine tra comuni, comuni nel raggio, codici NUTS EU.
    /// Richiede coordinate WGS84 nel database (da GeoNames).
    /// </summary>
    public ServiziGeoDistanza GeoDistanza { get; }

    /// <summary>
    /// Farmacie attive e impianti carburante per comune/provincia.
    /// Fonte: Ministero della Salute + MIMIT.
    /// </summary>
    public ServiziTerritorioServizi TerritoriServizi { get; }

    // ── Costruttori ──────────────────────────────────────────────────────────

    /// <summary>
    /// Crea Atlante con database embedded di default.
    /// Il database viene estratto lazy dalla risorsa embedded al primo utilizzo.
    /// </summary>
    public Atlante()
    {
        _database = new DatabaseAtlante();

        var repComuni = new RepositoryComuni(_database);
        var repCAP = new RepositoryCAP(_database);
        var repTelefonia = new RepositoryTelefonia(_database);
        var geo = new ServiziGeo(repComuni);
        var cf = new ServiziCodiceFiscale(repComuni);
        var parser = new ServiziParserIndirizzi(repComuni, repCAP);

        Comuni = new ServiziComuni(repComuni);
        CAP = new ServiziCAP(repCAP);
        Fiscale = cf;
        FiscalePG = new ServiziCodiceFiscalePG();
        Validazione = new ServiziValidazione();
        Geo = geo;
        Telefonia = new ServiziTelefonia(repTelefonia);
        Calendario = new ServiziFestività(repComuni);
        Parser = parser;
        Bonifica = new ServiziBonificaDati(repComuni, repCAP, cf, parser);
        Frontalieri = new ServiziFrontalieri(repComuni, geo);
        PA = new ServiziPA(repComuni, _database);
        TimeMachine = new ServiziTimeMachine(repComuni);
        ValidazioneCross = new ServiziValidazioneCross(repComuni, repCAP, cf, Validazione);
        Confronto = new ServiziConfrontoIndirizzi(parser);
        ATECO = new ServiziAteco(_database);
        Banche = new ServiziBanche(_database);
        ZoneTerritoriali = new ServiziZoneTerritoriali(_database);
        Regioni = new ServiziRegioni(_database);
        GeoDistanza = new ServiziGeoDistanza(_database);
        TerritoriServizi = new ServiziTerritorioServizi(_database);
    }

    /// <summary>Costruttore per Dependency Injection.</summary>
    public Atlante(
        IRepositoryComuni repositoryComuni,
        IRepositoryCAP repositoryCAP,
        IProviderTelefonia providerTelefonia,
        IProviderFestività? providerFestività = null,
        DatabaseAtlante? database = null)
    {
        _database = database ?? new DatabaseAtlante();
        var geo = new ServiziGeo(repositoryComuni);
        var cf = new ServiziCodiceFiscale(repositoryComuni);
        var parser = new ServiziParserIndirizzi(repositoryComuni, repositoryCAP);

        Comuni = new ServiziComuni(repositoryComuni);
        CAP = new ServiziCAP(repositoryCAP);
        Fiscale = cf;
        FiscalePG = new ServiziCodiceFiscalePG();
        Validazione = new ServiziValidazione();
        Geo = geo;
        Telefonia = new ServiziTelefonia(providerTelefonia);
        Calendario = providerFestività as ServiziFestività ?? new ServiziFestività(repositoryComuni);
        Parser = parser;
        Bonifica = new ServiziBonificaDati(repositoryComuni, repositoryCAP, cf, parser);
        Frontalieri = new ServiziFrontalieri(repositoryComuni, geo);
        PA = new ServiziPA(repositoryComuni, _database);
        TimeMachine = new ServiziTimeMachine(repositoryComuni);
        ValidazioneCross = new ServiziValidazioneCross(repositoryComuni, repositoryCAP, cf, Validazione);
        Confronto = new ServiziConfrontoIndirizzi(parser);
        ATECO = new ServiziAteco(_database);
        Banche = new ServiziBanche(_database);
        ZoneTerritoriali = new ServiziZoneTerritoriali(_database);
        Regioni = new ServiziRegioni(_database);
        GeoDistanza = new ServiziGeoDistanza(_database);
        TerritoriServizi = new ServiziTerritorioServizi(_database);
    }

    // ── Metadati Database ─────────────────────────────────────────────────────

    /// <summary>Versione dati nel DB (formato "YYYY-MM").</summary>
    public string? VersioneDati =>
        _database.EseguiScalare<string?>("SELECT valore FROM meta WHERE chiave = 'versione_dati'");

    /// <summary>Data ultimo aggiornamento ISTAT.</summary>
    public DateTime? DataUltimoAggiornamento
    {
        get
        {
            var s = _database.EseguiScalare<string?>(
                "SELECT valore FROM meta WHERE chiave = 'data_aggiornamento'");
            return DateTime.TryParse(s, out var d) ? d : null;
        }
    }

    public void Dispose()
    {
        if (!_rilasciato)
        {
            _database.Dispose();
            _rilasciato = true;
        }
    }
}
