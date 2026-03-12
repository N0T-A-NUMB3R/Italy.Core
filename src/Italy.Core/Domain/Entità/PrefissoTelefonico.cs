namespace Italy.Core.Domain.Entità;

public sealed class PrefissoTelefonico
{
    public string Prefisso { get; set; } = string.Empty;
    public TipoPrefisso Tipo { get; set; }
    public string? AreaGeografica { get; set; }
    public IReadOnlyList<string> CodiciISTAT { get; set; } = Array.Empty<string>();
    public bool IsAttivo { get; set; } = true;
}

public sealed class OperatoreMobile
{
    public string Prefisso { get; set; } = string.Empty;
    public string NomeOperatore { get; set; } = string.Empty;
    public TecnologiaRete Tecnologia { get; set; }
    public bool IsAttivo { get; set; } = true;
}

public sealed class RisultatoNumeroTelefonico
{
    public bool IsValido { get; set; }
    public string? NumeroNormalizzatoE164 { get; set; }
    public TipoPrefisso? Tipo { get; set; }
    public string? AreaGeografica { get; set; }
    public string? NomeOperatore { get; set; }
    public string? Prefisso { get; set; }
    public IReadOnlyList<string> Anomalie { get; set; } = Array.Empty<string>();
}
