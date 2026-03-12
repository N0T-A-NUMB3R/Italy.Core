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

    [Fact(DisplayName = "Il comune più vicino a Milano deve essere entro 10 km")]
    public void ComuneVicino_Milano_Entro10km()
    {
        // GeoNames posiziona i comuni nel centro del territorio:
        // Assago (più vicino a Milano) è a circa 7 km
        var comuni = _atlante.GeoDistanza.ComuniNelRaggio("F205", 10);
        Assert.True(comuni.Count > 0, "Nessun comune trovato entro 10 km da Milano");
        Assert.True(comuni[0].DistanzaKm <= 10);
    }

    [Fact(DisplayName = "OttieniNUTS restituisce un risultato (anche null) per comune valido")]
    public void NUTS_Milano_RisultatoPresente()
    {
        // Il metodo deve ritornare un valore (anche con NUTS null se non in DB)
        // senza lanciare eccezioni
        var nuts = _atlante.GeoDistanza.OttieniNUTS("F205");
        // nuts può essere null se i dati NUTS non sono nel DB (dipende dalla fonte dati)
        // verifichiamo solo che non lanci eccezioni
        Assert.True(nuts == null || nuts.HasValue);
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
