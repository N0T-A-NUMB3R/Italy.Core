namespace Italy.Core.Domain.Entità;

/// <summary>
/// Rappresenta una festività nazionale, locale o contrattuale italiana.
/// </summary>
public sealed class Festività
{
    public string Nome { get; set; } = string.Empty;
    public DateTime Data { get; set; }
    public TipoFestività Tipo { get; set; }

    /// <summary>Null per festività nazionali. Codice Belfiore per festività locali (Santo Patrono).</summary>
    public string? CodiceBelfiore { get; set; }

    /// <summary>Identificativo CCNL per festività contrattuali (es. "ABI", "METALMECCANICI").</summary>
    public string? CodiceCCNL { get; set; }

    public bool IsNazionale => Tipo == TipoFestività.Nazionale;

    public override string ToString() => $"{Data:dd/MM} - {Nome} ({Tipo})";
}

/// <summary>
/// Fuso orario storico italiano con anomalie belliche e variazioni legislativa.
/// </summary>
public sealed class InfoFusoOrario
{
    public DateTime Dal { get; set; }
    public DateTime? Al { get; set; }
    public TimeSpan OffsetUTC { get; set; }
    public bool IsOrarioLegale { get; set; }
    public string? Nota { get; set; }
}
