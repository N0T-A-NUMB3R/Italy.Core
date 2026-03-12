namespace Italy.Core.Domain.Entità;

/// <summary>
/// Rappresenta una festività nazionale, locale o contrattuale italiana.
/// </summary>
public sealed class Festività
{
    public string Nome { get; init; } = string.Empty;
    public DateTime Data { get; init; }
    public TipoFestività Tipo { get; init; }

    /// <summary>Null per festività nazionali. Codice Belfiore per festività locali (Santo Patrono).</summary>
    public string? CodiceBelfiore { get; init; }

    /// <summary>Identificativo CCNL per festività contrattuali (es. "ABI", "METALMECCANICI").</summary>
    public string? CodiceCCNL { get; init; }

    public bool IsNazionale => Tipo == TipoFestività.Nazionale;

    public override string ToString() => $"{Data:dd/MM} - {Nome} ({Tipo})";
}

/// <summary>
/// Fuso orario storico italiano con anomalie belliche e variazioni legislativa.
/// </summary>
public sealed class InfoFusoOrario
{
    public DateTime Dal { get; init; }
    public DateTime? Al { get; init; }
    public TimeSpan OffsetUTC { get; init; }
    public bool IsOrarioLegale { get; init; }
    public string? Nota { get; init; }
}
