using Xunit;

namespace Italy.Core.Tests;

public sealed class TestRegioni
{
    private readonly Atlante _atlante = new();

    [Fact(DisplayName = "Le regioni italiane devono essere 20")]
    public void Regioni_Sono20()
    {
        var regioni = _atlante.Regioni.TutteLeRegioni();
        Assert.Equal(20, regioni.Count);
    }

    [Fact(DisplayName = "La Lombardia esiste e ha più di 1000 comuni")]
    public void Lombardia_HaOltre1000Comuni()
    {
        var r = _atlante.Regioni.DaNome("Lombardia");
        Assert.NotNull(r);
        Assert.True(r.NumeroComuni > 1000, $"Attesi >1000 comuni, trovati {r.NumeroComuni}");
    }

    [Fact(DisplayName = "Le province italiane devono essere almeno 100")]
    public void Province_SonoAlmeno100()
    {
        var province = _atlante.Regioni.TutteLeProvince();
        Assert.True(province.Count >= 100, $"Attese >=100 province, trovate {province.Count}");
    }

    [Fact(DisplayName = "La provincia di Milano (MI) esiste")]
    public void Provincia_Milano_Esiste()
    {
        var p = _atlante.Regioni.DaSigla("MI");
        Assert.NotNull(p);
        Assert.Equal("MI", p.Sigla);
        Assert.Equal("Lombardia", p.NomeRegione);
    }

    [Fact(DisplayName = "La Lombardia ha almeno 10 province")]
    public void Lombardia_HaAlmeno10Province()
    {
        var province = _atlante.Regioni.DaRegione("Lombardia");
        Assert.True(province.Count >= 10, $"Attese >=10 province, trovate {province.Count}");
    }
}
