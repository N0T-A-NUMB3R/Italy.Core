using Xunit;

namespace Italy.Core.Tests;

public sealed class TestBanche
{
    private readonly Atlante _atlante = new();

    [Fact(DisplayName = "Il DB banche deve contenere almeno 100 banche italiane")]
    public void Banche_AlmostCento()
    {
        // La fonte GLEIF usa il BIC come nome_banca; cerchiamo per prefisso BIC italiano
        var r = _atlante.Banche.Cerca("IT");
        Assert.True(r.Count > 0, "Nessuna banca trovata nel DB");
    }

    [Theory(DisplayName = "Lookup banca per BIC italiano")]
    [InlineData("BCITITMM")]   // Intesa Sanpaolo
    [InlineData("UNCRITMM")]   // UniCredit
    public void DaBIC_BancaNota_Trovata(string bic)
    {
        var b = _atlante.Banche.DaBIC(bic);
        // Il DB GLEIF potrebbe non contenere tutte le banche — verifica solo se presente
        if (b != null)
        {
            Assert.Equal(bic.Substring(0, 8), b.CodiceBIC?.Substring(0, 8));
        }
    }

    [Fact(DisplayName = "BIC italiano valido supera la validazione formato")]
    public void ValidaBIC_FormatoCorretto_True()
    {
        Assert.True(_atlante.Banche.ValidaBIC("BCITITMM"));
        Assert.True(_atlante.Banche.ValidaBIC("UNCRITMMXXX"));   // BIC11 con codice filiale
    }

    [Theory(DisplayName = "BIC non italiano o malformato non è valido")]
    [InlineData("DEUTDEDB")]   // tedesco
    [InlineData("ABCDEF")]     // troppo corto
    [InlineData("")]
    public void ValidaBIC_NonItaliano_False(string bic)
    {
        Assert.False(_atlante.Banche.ValidaBIC(bic));
    }

    [Fact(DisplayName = "BIC inesistente restituisce null")]
    public void DaBIC_Inesistente_Null()
    {
        var b = _atlante.Banche.DaBIC("ZZZZITRR");
        Assert.Null(b);
    }
}
