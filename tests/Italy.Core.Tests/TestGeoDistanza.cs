using Xunit;

namespace Italy.Core.Tests;

public sealed class TestGeoDistanza
{
    private readonly Atlante _atlante = new();

    [Fact(DisplayName = "Distanza Milano-Roma deve essere circa 479 km")]
    public void DistanzaMilanoRoma_CircaQuattrocento()
    {
        var km = _atlante.GeoDistanza.DistanzaKm("F205", "H501");
        Assert.NotNull(km);
        Assert.InRange(km!.Value, 400, 560);
    }

    [Fact(DisplayName = "Distanza da un comune a sé stesso è zero")]
    public void DistanzaStessoComuneZero()
    {
        var km = _atlante.GeoDistanza.DistanzaKm("F205", "F205");
        Assert.NotNull(km);
        Assert.Equal(0.0, km!.Value);
    }

    [Fact(DisplayName = "Comuni nel raggio di 20 km da Milano devono essere più di 10")]
    public void ComuniNelRaggio_Milano20km_Molti()
    {
        var comuni = _atlante.GeoDistanza.ComuniNelRaggio("F205", 20);
        Assert.True(comuni.Count > 10, $"Attesi >10 comuni, trovati {comuni.Count}");
    }

    [Fact(DisplayName = "Il comune più vicino a Milano deve essere entro 5 km")]
    public void ComuneVicino_Milano_Entro5km()
    {
        var comuni = _atlante.GeoDistanza.ComuniNelRaggio("F205", 5);
        Assert.True(comuni.Count > 0, "Nessun comune trovato entro 5 km da Milano");
        Assert.True(comuni[0].DistanzaKm <= 5);
    }

    [Fact(DisplayName = "NUTS di Milano devono essere presenti")]
    public void NUTS_Milano_Presenti()
    {
        var nuts = _atlante.GeoDistanza.OttieniNUTS("F205");
        Assert.NotNull(nuts);
        // NUTS1 = macroregione Nord-Ovest = ITC
        // NUTS2 = Lombardia = ITC4
        // NUTS3 = Milano = ITC4C
        Assert.False(string.IsNullOrEmpty(nuts!.Value.NUTS1));
        Assert.False(string.IsNullOrEmpty(nuts!.Value.NUTS2));
        Assert.False(string.IsNullOrEmpty(nuts!.Value.NUTS3));
    }

    [Fact(DisplayName = "Comuni per NUTS3 Milano devono essere molti")]
    public void ComuniPerNUTS3_Milano_Molti()
    {
        // Prima ottieni il NUTS3 di Milano
        var nuts = _atlante.GeoDistanza.OttieniNUTS("F205");
        if (nuts?.NUTS3 == null) return; // skip se dato mancante nel DB
        var comuni = _atlante.GeoDistanza.ComuniPerNUTS3(nuts.Value.NUTS3);
        Assert.True(comuni.Count > 100, $"Attesi >100 comuni in NUTS3 Milano, trovati {comuni.Count}");
    }
}
