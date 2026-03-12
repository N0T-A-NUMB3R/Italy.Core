namespace Italy.Core.Domain.Entità;

/// <summary>
/// Informazioni sul regime frontaliero di un comune italiano.
/// Fondamentale per il corretto calcolo delle buste paga dei lavoratori frontalieri
/// secondo gli Accordi bilaterali Italia-Svizzera (aggiornati 2023) e altri confini.
/// </summary>
public sealed class InfoFrontalieri
{
    public string CodiceBelfiore { get; set; } = string.Empty;
    public string NomeComune { get; set; } = string.Empty;
    public bool IsComuneFrontaliero { get; set; }

    /// <summary>Distanza dal confine internazionale più vicino in km (linea d'aria).</summary>
    public double? DistanzaConfineKm { get; set; }

    /// <summary>Stato confinante più vicino (es. "Svizzera", "Francia", "Austria", "Slovenia").</summary>
    public string? StatoConfinante { get; set; }

    /// <summary>
    /// Regime fiscale applicabile per i frontalieri.
    /// Cambia in base agli accordi bilaterali vigenti.
    /// </summary>
    public RegimeFrontaliero Regime { get; set; }

    /// <summary>
    /// Fascia di appartenenza per l'accordo Italia-Svizzera 2023.
    /// Solo per comuni al confine svizzero.
    /// </summary>
    public FasciaFrontalieraSvizzera? FasciaSvizzera { get; set; }

    /// <summary>Data di entrata in vigore del regime attuale.</summary>
    public DateTime? DataDecorrenza { get; set; }

    public string? NoteNormative { get; set; }
}

public enum RegimeFrontaliero
{
    /// <summary>Comune non frontaliero.</summary>
    NonApplicabile,

    /// <summary>Accordo bilaterale Italia-Svizzera (Lugano 1974, aggiornato 2023).</summary>
    AccordoSvizzera,

    /// <summary>Accordo bilaterale Italia-Francia.</summary>
    AccordoFrancia,

    /// <summary>Accordo bilaterale Italia-Austria.</summary>
    AccordoAustria,

    /// <summary>Accordo bilaterale Italia-Slovenia.</summary>
    AccordoSlovenia,
}

/// <summary>
/// Fascia per l'accordo Italia-Svizzera 2023 (D.Lgs. 209/2023).
/// Determina il trattamento fiscale e previdenziale.
/// </summary>
public enum FasciaFrontalieraSvizzera
{
    /// <summary>Comuni entro 20km dal confine — regime agevolato pieno.</summary>
    ZonaFrontaliera,

    /// <summary>Comuni tra 20km e 30km — regime transitorio.</summary>
    ZonaTransizione,
}

/// <summary>
/// Informazioni sulle aggregazioni sovracomunali di un comune.
/// </summary>
public sealed class AggregazioniSovracomunali
{
    public string CodiceBelfiore { get; set; } = string.Empty;

    // ── Sanitario ────────────────────────────────────────────────────────────
    public string? CodiceASL { get; set; }
    public string? NomeASL { get; set; }
    public string? CodiceAziendaOspedaliera { get; set; }

    // ── Montano ──────────────────────────────────────────────────────────────
    public string? ComuneMontanaCodice { get; set; }
    public string? ComuneMontanaNome { get; set; }
    public bool IsComuneMontano { get; set; }

    // ── Unioni e Consorzi ────────────────────────────────────────────────────
    public string? UnioneComuni { get; set; }
    public string? ConsorzioServizi { get; set; }

    // ── Ambiti Territoriali Ottimali ─────────────────────────────────────────
    public string? ATOAcqua { get; set; }
    public string? ATORifiuti { get; set; }
    public string? ATOEnergia { get; set; }

    // ── Distretto Scolastico ─────────────────────────────────────────────────
    public string? DistrettoScolastico { get; set; }
    public string? UfficioScolasticoRegionale { get; set; }

    // ── Giustizia ───────────────────────────────────────────────────────────
    public string? TribunaleCompetente { get; set; }
    public string? PreturaCodice { get; set; }
}

/// <summary>
/// Codici IPA (Indice della Pubblica Amministrazione) per la fatturazione elettronica.
/// </summary>
public sealed class CodiceIPA
{
    public string CodiceBelfiore { get; set; } = string.Empty;
    public string NomeEnte { get; set; } = string.Empty;

    /// <summary>Codice IPA univoco (es. "c_f205" per Comune di Milano).</summary>
    public string CodiceIPAUnivoco { get; set; } = string.Empty;

    /// <summary>Codice Ufficio SdI per la fatturazione elettronica B2G.</summary>
    public string CodiceSdI { get; set; } = string.Empty;

    public string? PEC { get; set; }
    public string? CodiceIVA { get; set; }
    public string? CodiceFiscaleEnte { get; set; }
    public TipoEnteIPA TipoEnte { get; set; }
    public DateTime? DataAggiornamento { get; set; }
}

public enum TipoEnteIPA
{
    ComuneCapoluogo,
    Comune,
    Provincia,
    RegioneAutonoma,
    Regione,
    CittaMetropolitana,
    UnioneComunity,
    AziendaSanitaria,
    Altro
}
