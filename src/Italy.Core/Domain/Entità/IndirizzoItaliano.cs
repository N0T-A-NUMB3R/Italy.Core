namespace Italy.Core.Domain.Entità;

/// <summary>
/// Indirizzo italiano scomposto nei suoi componenti canonici.
/// </summary>
public sealed class IndirizzoItaliano
{
    // ── Componenti via ────────────────────────────────────────────────────────
    /// <summary>Tipo via normalizzato (VIA, VIALE, PIAZZA, CORSO, ecc.).</summary>
    public string? Toponimo { get; init; }

    /// <summary>Nome della via (senza il tipo).</summary>
    public string? NomeVia { get; init; }

    /// <summary>Numero civico (es. "10", "10/A", "10 bis").</summary>
    public string? Civico { get; init; }

    // ── Località ─────────────────────────────────────────────────────────────
    public string? CAP { get; init; }
    public string? NomeComune { get; init; }
    public string? SiglaProvincia { get; init; }
    public string? NomeProvincia { get; init; }

    // ── Risoluzione DB ────────────────────────────────────────────────────────
    /// <summary>Comune risolto nel database (null se non trovato).</summary>
    public Comune? ComuneRisolto { get; init; }

    // ── Qualità ───────────────────────────────────────────────────────────────
    public bool IsCompleto { get; init; }
    public double ScoreQualità { get; init; }  // 0.0 → 1.0
    public IReadOnlyList<string> Anomalie { get; init; } = Array.Empty<string>();

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
    public bool RequiereCorrezione { get; init; }
    public string? CampoProblematico { get; init; }
    public string? ValoreOriginale { get; init; }
    public string? ValoreSuggerito { get; init; }
    public double ConfidenzaSuggerimento { get; init; }  // 0.0 → 1.0
    public string Motivazione { get; init; } = string.Empty;
    public TipoBonifica Tipo { get; init; }
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
    public int TotaleRecord { get; init; }
    public int RecordConAnomalie { get; init; }
    public int RecordPuliti { get; init; }
    public double PercentualePulizia => TotaleRecord == 0 ? 100.0 : (double)RecordPuliti / TotaleRecord * 100.0;
    public IReadOnlyList<RecordBonifica> Risultati { get; init; } = Array.Empty<RecordBonifica>();
}

public sealed class RecordBonifica
{
    public int IndiceRecord { get; init; }
    public object? DatoOriginale { get; init; }
    public IReadOnlyList<RisultatoBonifica> Correzioni { get; init; } = Array.Empty<RisultatoBonifica>();
    public bool HasAnomalie => Correzioni.Any(c => c.RequiereCorrezione);
}
