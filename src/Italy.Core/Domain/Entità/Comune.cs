namespace Italy.Core.Domain.Entità;

/// <summary>
/// Rappresenta un comune italiano secondo la nomenclatura ISTAT.
/// Il modello è immutabile: i dati storici si recuperano tramite <see cref="VariazioneStorica"/>.
/// </summary>
public sealed class Comune
{
    // ── Identificativi ──────────────────────────────────────────────────────
    /// <summary>Codice Catastale (Belfiore) - identificativo univoco immutabile. Es: "F205" per Milano.</summary>
    public string CodiceBelfiore { get; set; } = string.Empty;

    /// <summary>Codice ISTAT a 6 cifre. Es: "015146" per Milano.</summary>
    public string CodiceISTAT { get; set; } = string.Empty;

    // ── Anagrafica ──────────────────────────────────────────────────────────
    /// <summary>Denominazione ufficiale depositata in ANPR.</summary>
    public string DenominazioneUfficiale { get; set; } = string.Empty;

    /// <summary>Denominazione in lingua/dialetto locale (es. ladino, tedesco per Alto Adige).</summary>
    public string? DenominazioneAlternativa { get; set; }

    // ── Gerarchia Amministrativa ────────────────────────────────────────────
    public string SiglaProvincia { get; set; } = string.Empty;
    public string NomeProvincia { get; set; } = string.Empty;
    public string CodiceProvinciaISTAT { get; set; } = string.Empty;
    public string NomeRegione { get; set; } = string.Empty;
    public string CodiceRegioneISTAT { get; set; } = string.Empty;
    public RipartizioneGeografica Ripartizione { get; set; }

    // ── Stato Amministrativo ────────────────────────────────────────────────
    public bool IsCapoluogoProvincia { get; set; }
    public bool IsCittàMetropolitana { get; set; }
    public bool IsComuneMontano { get; set; }
    public bool IsAttivo { get; set; } = true;

    // ── Dati Storici ────────────────────────────────────────────────────────
    public DateTime DataIstituzione { get; set; }
    public DateTime? DataSoppressione { get; set; }

    /// <summary>Codice Belfiore del comune successore (in caso di fusione/soppressione).</summary>
    public string? CodiceSuccessore { get; set; }

    // ── CAP ─────────────────────────────────────────────────────────────────
    public string? CAPPrincipale { get; set; }

    // ── Geospaziale ─────────────────────────────────────────────────────────
    public double? Latitudine { get; set; }
    public double? Longitudine { get; set; }
    public double? AltitudineMetri { get; set; }
    public double? SuperficieKmq { get; set; }

    // ── Demografici ─────────────────────────────────────────────────────────
    public int? Popolazione { get; set; }
    public int? AnnoRilevazionePopolazione { get; set; }

    // ── Classificazioni ─────────────────────────────────────────────────────
    public ZonaSismica? ZonaSismica { get; set; }
    public ZonaClimatica? ZonaClimatica { get; set; }
    public ClasseAreeInterne? ClasseAreeInterne { get; set; }
    /// <summary>Zona altimetrica ISTAT: Pianura, Collina Interna/Litoranea, Montagna Interna/Litoranea.</summary>
    public ZonaAltimetrica? ZonaAltimetrica { get; set; }

    // ── NUTS / Eurostat ──────────────────────────────────────────────────────
    public string? CodiceNUTS3 { get; set; }
    public string? CodiceNUTS2 { get; set; }
    public string? CodiceNUTS1 { get; set; }

    // ── Santo Patrono ────────────────────────────────────────────────────────
    /// <summary>Nome del santo patrono locale. Null se non disponibile.</summary>
    public string? SantoPatrono { get; set; }
    /// <summary>Giorno della festa del patrono (1-31). Null se non disponibile.</summary>
    public int? PatronoGiorno { get; set; }
    /// <summary>Mese della festa del patrono (1-12). Null se non disponibile.</summary>
    public int? PatronoMese { get; set; }

    public override string ToString() =>
        $"{DenominazioneUfficiale} ({SiglaProvincia}) [{CodiceBelfiore}]";
}
