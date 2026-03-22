namespace Italy.Core.Domain.Entità;

/// <summary>
/// Indirizzo italiano scomposto nei suoi componenti canonici.
/// </summary>
public sealed class IndirizzoItaliano
{
    // ── Componenti via ────────────────────────────────────────────────────────
    /// <summary>Tipo via normalizzato (VIA, VIALE, PIAZZA, CORSO, ecc.).</summary>
    public string? Toponimo { get; set; }

    /// <summary>Nome della via (senza il tipo).</summary>
    public string? NomeVia { get; set; }

    /// <summary>Numero civico (es. "10", "10/A", "10 bis").</summary>
    public string? Civico { get; set; }

    // ── Località ─────────────────────────────────────────────────────────────
    public string? CAP { get; set; }
    public string? NomeComune { get; set; }
    public string? SiglaProvincia { get; set; }
    public string? NomeProvincia { get; set; }

    // ── Risoluzione DB ────────────────────────────────────────────────────────
    /// <summary>Comune risolto nel database (null se non trovato).</summary>
    public Comune? ComuneRisolto { get; set; }

    // ── Qualità ───────────────────────────────────────────────────────────────
    public bool IsCompleto { get; set; }
    public double ScoreQualità { get; set; }  // 0.0 → 1.0
    public IReadOnlyList<string> Anomalie { get; set; } = Array.Empty<string>();

    /// <summary>Indirizzo ricostruito in formato canonico ANPR.</summary>
    public string FormatoANPR =>
        string.Join(", ", new[]
        {
            Toponimo != null && NomeVia != null ? $"{Toponimo} {NomeVia}" : NomeVia,
            Civico,
            CAP != null && NomeComune != null ? $"{CAP} {NomeComune}" : NomeComune,
            SiglaProvincia != null ? $"({SiglaProvincia})" : null,
        }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public override string ToString() => FormatoANPR;
}

/// <summary>
/// Risultato della bonifica di un record anagrafico con indirizzi potenzialmente errati.
/// </summary>
public sealed class RisultatoBonifica
{
    public bool RichiedeCorrezione { get; set; }
    public string? CampoProblematico { get; set; }
    public string? ValoreOriginale { get; set; }
    public string? ValoreSuggerito { get; set; }
    public double ConfidenzaSuggerimento { get; set; }  // 0.0 → 1.0
    public string Motivazione { get; set; } = string.Empty;
    public TipoBonifica Tipo { get; set; }
}

public enum TipoBonifica
{
    NessunaCorrezione,
    ComuneRinominato,
    ComuneFuso,
    SiglaProvinciaAggiornata,
    CAPAggiornato,
    CAPNonCorrispondeAlComune,
    ComuneNonTrovato,
    ProvinciaInconsistente,
    CodiceFiscaleIncoerente,
}

/// <summary>
/// Report completo di bonifica per un batch di record.
/// </summary>
public sealed class ReportBonificaBatch
{
    public int TotaleRecord { get; set; }
    public int RecordConAnomalie { get; set; }
    public int RecordPuliti { get; set; }
    public double PercentualePulizia => TotaleRecord == 0 ? 100.0 : (double)RecordPuliti / TotaleRecord * 100.0;
    public IReadOnlyList<RecordBonifica> Risultati { get; set; } = Array.Empty<RecordBonifica>();
}

public sealed class RecordBonifica
{
    public int IndiceRecord { get; set; }
    public object? DatoOriginale { get; set; }
    public IReadOnlyList<RisultatoBonifica> Correzioni { get; set; } = Array.Empty<RisultatoBonifica>();
    public bool HasAnomalie => Correzioni.Any(c => c.RichiedeCorrezione);
}
