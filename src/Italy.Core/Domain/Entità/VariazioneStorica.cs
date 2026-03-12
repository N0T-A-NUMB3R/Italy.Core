namespace Italy.Core.Domain.Entità;

/// <summary>
/// Registra una variazione amministrativa di un comune dal 1861 ad oggi.
/// </summary>
public sealed class VariazioneStorica
{
    public int Id { get; init; }
    public string CodiceBelfiore { get; init; } = string.Empty;
    public TipoVariazione Tipo { get; init; }
    public DateTime DataVariazione { get; init; }
    public string? DenominazionePrecedente { get; init; }
    public string? DenominazioneSuccessiva { get; init; }
    public string? ProvinciaPrecedente { get; init; }
    public string? ProvinciaSuccessiva { get; init; }

    /// <summary>Codici Belfiore dei comuni di origine (es. in caso di fusione).</summary>
    public IReadOnlyList<string> CodiciOrigine { get; init; } = Array.Empty<string>();

    /// <summary>Codici Belfiore dei comuni risultanti dalla variazione.</summary>
    public IReadOnlyList<string> CodiciDestinazione { get; init; } = Array.Empty<string>();

    /// <summary>Riferimento normativo (es. "D.L. 123/2018").</summary>
    public string? RiferimentoNormativo { get; init; }

    public string? Note { get; init; }

    public override string ToString() =>
        $"{Tipo} del {DataVariazione:yyyy-MM-dd}: [{string.Join(", ", CodiciOrigine)}] → [{string.Join(", ", CodiciDestinazione)}]";
}
