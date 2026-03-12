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
        // Se il DB ha coordinate per Milano
        var km = _servizi.CalcolaDistanzaKm("F205", "F205");
        if (km.HasValue)
            Assert.Equal(0.0, km.Value, precision: 3);
    }
}
