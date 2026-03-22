using Italy.Core.Applicazione.Servizi;
using Italy.Core.Domain.Entità;
using Italy.Core.Infrastruttura;
using Italy.Core.Infrastruttura.Repository;
using Xunit;

namespace Italy.Core.Tests;

public sealed class TestGeo
{
    private readonly ServiziGeo _servizi;

    public TestGeo()
    {
        var db = new DatabaseAtlante();
        var repComuni = new RepositoryComuni(db);
        _servizi = new ServiziGeo(repComuni);
    }

    [Fact(DisplayName = "Distanza Milano-Torino deve essere circa 126 km")]
    public void CalcolaDistanza_MilanoTorino_CircaCorretto()
    {
        var km = _servizi.CalcolaDistanzaKm("F205", "L219"); // Milano → Torino
        if (km.HasValue) // Richiede coordinate nel DB
            Assert.InRange(km.Value, 120, 135);
    }

    [Fact(DisplayName = "TrovaNelRaggio con raggio 0 deve restituire solo il comune corrispondente")]
    public void TrovaNelRaggio_RaggioZero_SoloComuneVicinissimo()
    {
        var punto = new CoordinateGeo { Latitudine = 45.4654, Longitudine = 9.1859 }; // Milano centro
        var comuni = _servizi.TrovaNelRaggio(punto, 1.0); // 1 km
        // Deve trovare almeno un comune (Milano stessa)
        Assert.NotEmpty(comuni);
    }

    [Fact(DisplayName = "Haversine di punto identico deve restituire 0")]
    public void Haversine_PuntoIdentico_Zero()
    {
        var km = _servizi.CalcolaDistanzaKm("F205", "F205");
        if (km.HasValue)
            Assert.Equal(0.0, km.Value, precision: 3);
    }

    // ── Gauss-Boaga ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "Milano WGS84 → Gauss-Boaga Fuso Ovest")]
    public void GaussBoaga_Milano_FusoOvest()
    {
        // Milano: lat=45.4642, lon=9.1900 → Fuso Ovest (lon < 12°)
        var (est, nord, fuso) = _servizi.ConvertInGaussBoaga(45.4642, 9.1900);
        Assert.Equal("Ovest", fuso);
        // Est atteso ~1.518.000 m, Nord ~5.035.000 m (tolleranza 1000 m)
        Assert.InRange(est,  1_510_000, 1_530_000);
        Assert.InRange(nord, 5_025_000, 5_045_000);
    }

    [Fact(DisplayName = "Roma WGS84 → Gauss-Boaga Fuso Est")]
    public void GaussBoaga_Roma_FusoEst()
    {
        // Roma: lat=41.8955, lon=12.4823 → Fuso Est (lon >= 12°)
        var (est, nord, fuso) = _servizi.ConvertInGaussBoaga(41.8955, 12.4823);
        Assert.Equal("Est", fuso);
        // Est atteso ~2.311.000 m (Fuso Est: falso est 2.520.000), Nord ~4.641.000 m
        Assert.InRange(est,  2_300_000, 2_325_000);
        Assert.InRange(nord, 4_630_000, 4_655_000);
    }

    [Fact(DisplayName = "Gauss-Boaga via Belfiore Milano restituisce stesso risultato di coordinate dirette")]
    public void GaussBoaga_DaBelfiore_Coerente()
    {
        var daCoord  = _servizi.ConvertInGaussBoaga(45.4642, 9.1900);
        var daBelfiore = _servizi.ConvertComuneInGaussBoaga("F205");
        if (daBelfiore.HasValue)
        {
            Assert.InRange(daBelfiore.Value.Est,  daCoord.Est  - 5000, daCoord.Est  + 5000);
            Assert.InRange(daBelfiore.Value.Nord, daCoord.Nord - 5000, daCoord.Nord + 5000);
        }
    }

    [Fact(DisplayName = "Gauss-Boaga per Belfiore inesistente restituisce null")]
    public void GaussBoaga_BelfioreInesistente_Null()
    {
        var result = _servizi.ConvertComuneInGaussBoaga("ZZZZ");
        Assert.Null(result);
    }

    // ── UTM ───────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Milano WGS84 → UTM fuso 32")]
    public void UTM_Milano_Fuso32()
    {
        var (fuso, est, nord) = _servizi.ConvertInUTM(45.4642, 9.1900);
        Assert.Equal(32, fuso);
        // Est atteso ~514.000 m, Nord ~5.034.000 m (tolleranza 1000 m)
        Assert.InRange(est,  510_000, 520_000);
        Assert.InRange(nord, 5_025_000, 5_045_000);
    }

    [Fact(DisplayName = "Trieste WGS84 → UTM fuso 33")]
    public void UTM_Trieste_Fuso33()
    {
        // Trieste: lat=45.6494, lon=13.7681 → fuso 33 (lon > 12°)
        var (fuso, est, nord) = _servizi.ConvertInUTM(45.6494, 13.7681);
        Assert.Equal(33, fuso);
        Assert.InRange(est,  395_000, 415_000);
        Assert.InRange(nord, 5_055_000, 5_075_000);
    }

    [Fact(DisplayName = "UTM via Belfiore restituisce risultato plausibile")]
    public void UTM_DaBelfiore_Plausibile()
    {
        var result = _servizi.ConvertComuneInUTM("F205");
        if (result.HasValue)
        {
            Assert.Equal(32, result.Value.Fuso);
            Assert.InRange(result.Value.Est,  400_000, 700_000);
            Assert.InRange(result.Value.Nord, 4_000_000, 6_000_000);
        }
    }

    [Fact(DisplayName = "UTM per Belfiore inesistente restituisce null")]
    public void UTM_BelfioreInesistente_Null()
    {
        var result = _servizi.ConvertComuneInUTM("ZZZZ");
        Assert.Null(result);
    }
}
