namespace Italy.Core.Domain.Entità;

/// <summary>
/// Registra una variazione amministrativa di un comune dal 1861 ad oggi.
/// </summary>
public sealed class VariazioneStorica
{
    public int Id { get; set; }
    public string CodiceBelfiore { get; set; } = string.Empty;
    public TipoVariazione Tipo { get; set; }
    public DateTime DataVariazione { get; set; }
    public string? DenominazionePrecedente { get; set; }
    public string? DenominazioneSuccessiva { get; set; }
    public string? ProvinciaPrecedente { get; set; }
    public string? ProvinciaSuccessiva { get; set; }

    /// <summary>Codici Belfiore dei comuni di origine (es. in caso di fusione).</summary>
    public IReadOnlyList<string> CodiciOrigine { get; set; } = Array.Empty<string>();

    /// <summary>Codici Belfiore dei comuni risultanti dalla variazione.</summary>
    public IReadOnlyList<string> CodiciDestinazione { get; set; } = Array.Empty<string>();

    /// <summary>Riferimento normativo (es. "D.L. 123/2018").</summary>
    public string? RiferimentoNormativo { get; set; }

    public string? Note { get; set; }

    public override string ToString() =>
        $"{Tipo} del {DataVariazione:yyyy-MM-dd}: [{string.Join(", ", CodiciOrigine)}] → [{string.Join(", ", CodiciDestinazione)}]";
}
