namespace Italy.Core.Domain.Entità;

/// <summary>
/// Impianto di distribuzione carburanti attivo.
/// Fonte: MIMIT (Ministero delle Imprese e del Made in Italy) — aggiornamento mensile.
/// </summary>
public sealed class ImpiantoCarburante
{
    public int IdImpianto { get; set; }
    public string? Gestore { get; set; }
    public string? Bandiera { get; set; }
    public string? TipoImpianto { get; set; }
    public string? NomeImpianto { get; set; }
    public string? Indirizzo { get; set; }
    public string Comune { get; set; } = string.Empty;
    public string SiglaProvincia { get; set; } = string.Empty;
    public double? Latitudine { get; set; }
    public double? Longitudine { get; set; }

    public override string ToString() =>
        $"{Bandiera ?? Gestore} — {NomeImpianto ?? Indirizzo} ({Comune}, {SiglaProvincia})";
}
