namespace Italy.Core.Domain.Entità;

public sealed class PrefissoTelefonico
{
    public string Prefisso { get; init; } = string.Empty;
    public TipoPrefisso Tipo { get; init; }
    public string? AreaGeografica { get; init; }
    public IReadOnlyList<string> CodiciISTAT { get; init; } = Array.Empty<string>();
    public bool IsAttivo { get; init; } = true;
}

public sealed class OperatoreMobile
{
    public string Prefisso { get; init; } = string.Empty;
    public string NomeOperatore { get; init; } = string.Empty;
    public TecnologiaRete Tecnologia { get; init; }
    public bool IsAttivo { get; init; } = true;
}

public sealed class RisultatoNumeroTelefonico
{
    public bool IsValido { get; init; }
    public string? NumeroNormalizzatoE164 { get; init; }
    public TipoPrefisso? Tipo { get; init; }
    public string? AreaGeografica { get; init; }
    public string? NomeOperatore { get; init; }
    public string? Prefisso { get; init; }
    public IReadOnlyList<string> Anomalie { get; init; } = Array.Empty<string>();
}
