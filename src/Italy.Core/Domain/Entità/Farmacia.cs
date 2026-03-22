namespace Italy.Core.Domain.Entità;

/// <summary>
/// Farmacia attiva sul territorio italiano.
/// Fonte: Ministero della Salute — aggiornamento settimanale.
/// </summary>
public sealed class Farmacia
{
    public int CodFarmacia { get; set; }
    public string Denominazione { get; set; } = string.Empty;
    public string? Indirizzo { get; set; }
    public string? CAP { get; set; }
    public string Comune { get; set; } = string.Empty;
    public string? Frazione { get; set; }
    public string SiglaProvincia { get; set; } = string.Empty;
    public string? Provincia { get; set; }
    public string? Regione { get; set; }
    public string? CodComuneISTAT { get; set; }
    public string? Tipologia { get; set; }
    public double? Latitudine { get; set; }
    public double? Longitudine { get; set; }

    public override string ToString() =>
        $"{Denominazione} — {Comune} ({SiglaProvincia})";
}
