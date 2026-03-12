namespace Italy.Core.Domain.Entità;

/// <summary>
/// Informazioni sul regime frontaliero di un comune italiano.
/// Fondamentale per il corretto calcolo delle buste paga dei lavoratori frontalieri
/// secondo gli Accordi bilaterali Italia-Svizzera (aggiornati 2023) e altri confini.
/// </summary>
public sealed class InfoFrontalieri
{
    public string CodiceBelfiore { get; init; } = string.Empty;
    public string NomeComune { get; init; } = string.Empty;
    public bool IsComuneFrontaliero { get; init; }

    /// <summary>Distanza dal confine internazionale più vicino in km (linea d'aria).</summary>
    public double? DistanzaConfineKm { get; init; }

    /// <summary>Stato confinante più vicino (es. "Svizzera", "Francia", "Austria", "Slovenia").</summary>
    public string? StatoConfinante { get; init; }

    /// <summary>
    /// Regime fiscale applicabile per i frontalieri.
    /// Cambia in base agli accordi bilaterali vigenti.
    /// </summary>
    public RegimeFrontaliero Regime { get; init; }

    /// <summary>
    /// Fascia di appartenenza per l'accordo Italia-Svizzera 2023.
    /// Solo per comuni al confine svizzero.
    /// </summary>
    public FasciaFrontalieraSvizzera? FasciaSvizzera { get; init; }

    /// <summary>Data di entrata in vigore del regime attuale.</summary>
    public DateTime? DataDecorrenza { get; init; }

    public string? NoteNormative { get; init; }
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
    public string CodiceBelfiore { get; init; } = string.Empty;

    // ── Sanitario ────────────────────────────────────────────────────────────
    public string? CodiceASL { get; init; }
    public string? NomeASL { get; init; }
    public string? CodiceAziendaOspedaliera { get; init; }

    // ── Montano ──────────────────────────────────────────────────────────────
    public string? ComuneMontanaCodice { get; init; }
    public string? ComuneMontanaNome { get; init; }
    public bool IsComuneMontano { get; init; }

    // ── Unioni e Consorzi ────────────────────────────────────────────────────
    public string? UnioneComuni { get; init; }
    public string? ConsorzioServizi { get; init; }

    // ── Ambiti Territoriali Ottimali ─────────────────────────────────────────
    public string? ATOAcqua { get; init; }
    public string? ATORifiuti { get; init; }
    public string? ATOEnergia { get; init; }

    // ── Distretto Scolastico ─────────────────────────────────────────────────
    public string? DistrettoScolastico { get; init; }
    public string? UfficioScolasticoRegionale { get; init; }

    // ── Giustizia ───────────────────────────────────────────────────────────
    public string? TribunaleCompetente { get; init; }
    public string? PreturaCodice { get; init; }
}

/// <summary>
/// Codici IPA (Indice della Pubblica Amministrazione) per la fatturazione elettronica.
/// </summary>
public sealed class CodiceIPA
{
    public string CodiceBelfiore { get; init; } = string.Empty;
    public string NomeEnte { get; init; } = string.Empty;

    /// <summary>Codice IPA univoco (es. "c_f205" per Comune di Milano).</summary>
    public string CodiceIPAUnivoco { get; init; } = string.Empty;

    /// <summary>Codice Ufficio SdI per la fatturazione elettronica B2G.</summary>
    public string CodiceSdI { get; init; } = string.Empty;

    public string? PEC { get; init; }
    public string? CodiceIVA { get; init; }
    public string? CodiceFiscaleEnte { get; init; }
    public TipoEnteIPA TipoEnte { get; init; }
    public DateTime? DataAggiornamento { get; init; }
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
