namespace Italy.Core.Domain.Entità;

/// <summary>
/// Rappresenta un comune italiano secondo la nomenclatura ISTAT.
/// Il modello è immutabile: i dati storici si recuperano tramite <see cref="VariazioneStorica"/>.
/// </summary>
public sealed class Comune
{
    // ── Identificativi ──────────────────────────────────────────────────────
    /// <summary>Codice Catastale (Belfiore) - identificativo univoco immutabile. Es: "F205" per Milano.</summary>
    public string CodiceBelfiore { get; init; } = string.Empty;

    /// <summary>Codice ISTAT a 6 cifre. Es: "015146" per Milano.</summary>
    public string CodiceISTAT { get; init; } = string.Empty;

    // ── Anagrafica ──────────────────────────────────────────────────────────
    /// <summary>Denominazione ufficiale depositata in ANPR.</summary>
    public string DenominazioneUfficiale { get; init; } = string.Empty;

    /// <summary>Denominazione in lingua/dialetto locale (es. ladino, tedesco per Alto Adige).</summary>
    public string? DenominazioneAlternativa { get; init; }

    // ── Gerarchia Amministrativa ────────────────────────────────────────────
    public string SiglaProvincia { get; init; } = string.Empty;
    public string NomeProvincia { get; init; } = string.Empty;
    public string CodiceProvinciaISTAT { get; init; } = string.Empty;
    public string NomeRegione { get; init; } = string.Empty;
    public string CodiceRegioneISTAT { get; init; } = string.Empty;
    public RipartizioneGeografica Ripartizione { get; init; }

    // ── Stato Amministrativo ────────────────────────────────────────────────
    public bool IsCapoluogoProvincia { get; init; }
    public bool IsCittàMetropolitana { get; init; }
    public bool IsComuneMontano { get; init; }
    public bool IsAttivo { get; init; } = true;

    // ── Dati Storici ────────────────────────────────────────────────────────
    public DateTime DataIstituzione { get; init; }
    public DateTime? DataSoppressione { get; init; }

    /// <summary>Codice Belfiore del comune successore (in caso di fusione/soppressione).</summary>
    public string? CodiceSuccessore { get; init; }

    // ── CAP ─────────────────────────────────────────────────────────────────
    public string? CAPPrincipale { get; init; }

    // ── Geospaziale ─────────────────────────────────────────────────────────
    public double? Latitudine { get; init; }
    public double? Longitudine { get; init; }
    public double? AltitudineMetri { get; init; }
    public double? SuperficieKmq { get; init; }

    // ── Demografici ─────────────────────────────────────────────────────────
    public int? Popolazione { get; init; }
    public int? AnnoRilevazionePopolazione { get; init; }

    // ── Classificazioni ─────────────────────────────────────────────────────
    public ZonaSismica? ZonaSismica { get; init; }
    public ZonaClimatica? ZonaClimatica { get; init; }
    public ClasseAreeInterne? ClasseAreeInterne { get; init; }

    // ── NUTS / Eurostat ──────────────────────────────────────────────────────
    public string? CodiceNUTS3 { get; init; }
    public string? CodiceNUTS2 { get; init; }
    public string? CodiceNUTS1 { get; init; }

    public override string ToString() =>
        $"{DenominazioneUfficiale} ({SiglaProvincia}) [{CodiceBelfiore}]";
}
