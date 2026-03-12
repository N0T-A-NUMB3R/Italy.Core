namespace Italy.Core.Domain.Entità;

/// <summary>
/// Rappresenta una zona CAP (Codice di Avviamento Postale) italiana.
/// Un CAP può coprire più comuni e un comune può avere più CAP.
/// </summary>
public sealed class ZonaCAP
{
    public string CAP { get; init; } = string.Empty;

    /// <summary>Codici ISTAT dei comuni coperti da questo CAP.</summary>
    public IReadOnlyList<string> CodiciISTAT { get; init; } = Array.Empty<string>();

    /// <summary>Codici Belfiore dei comuni coperti da questo CAP.</summary>
    public IReadOnlyList<string> CodiciBelfiore { get; init; } = Array.Empty<string>();

    /// <summary>Descrizione della zona (es. "Milano Centro", "Roma Prati").</summary>
    public string? DescrizioneZona { get; init; }

    public DateTime? DataAttivazione { get; init; }
    public DateTime? DataDisattivazione { get; init; }

    public bool IsAttivo => DataDisattivazione == null || DataDisattivazione > DateTime.Today;

    public override string ToString() => $"CAP {CAP} - {DescrizioneZona ?? string.Join(", ", CodiciISTAT)}";
}
