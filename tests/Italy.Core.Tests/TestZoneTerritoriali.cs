using Italy.Core.Domain.Entità;
using Xunit;

namespace Italy.Core.Tests;

public sealed class TestZoneTerritoriali
{
    private readonly Atlante _atlante = new();

    [Theory(DisplayName = "Zone territoriali presenti per comuni noti")]
    [InlineData("F205")]  // Milano
    [InlineData("H501")]  // Roma
    [InlineData("L736")]  // Torino
    public void OttieniZone_ComuneNoto_NonNull(string belfiore)
    {
        var z = _atlante.ZoneTerritoriali.OttieniZone(belfiore);
        Assert.NotNull(z);
        Assert.Equal(belfiore, z.CodiceBelfiore);
    }

    [Fact(DisplayName = "Zona sismica di Milano deve essere 3 o 4")]
    public void ZonaSismica_Milano_Zona3O4()
    {
        var z = _atlante.ZoneTerritoriali.OttieniZone("F205");
        Assert.NotNull(z);
        Assert.NotNull(z.ZonaSismica);
        Assert.True(z.ZonaSismica == ZonaSismica.Zona3 || z.ZonaSismica == ZonaSismica.Zona4,
            $"Milano zona sismica attesa 3 o 4, trovata {z.ZonaSismica}");
    }

    [Fact(DisplayName = "Coordinate WGS84 di Milano devono essere plausibili")]
    public void Coordinate_Milano_Plausibili()
    {
        var z = _atlante.ZoneTerritoriali.OttieniZone("F205");
        Assert.NotNull(z);
        Assert.NotNull(z.Latitudine);
        Assert.NotNull(z.Longitudine);
        // Milano: lat ~45.46, lng ~9.19
        Assert.InRange(z.Latitudine!.Value, 44.0, 47.0);
        Assert.InRange(z.Longitudine!.Value, 8.0, 11.0);
    }

    [Fact(DisplayName = "Comuni zona sismica 1 devono essere non vuoti")]
    public void ComuniPerZonaSismica_Zona1_NonVuoti()
    {
        var comuni = _atlante.ZoneTerritoriali.ComuniPerZonaSismica(1);
        Assert.NotEmpty(comuni);
    }

    [Fact(DisplayName = "Zona sismica non valida lancia ArgumentException")]
    public void ComuniPerZonaSismica_Invalida_Throws()
    {
        Assert.Throws<ArgumentException>(() => _atlante.ZoneTerritoriali.ComuniPerZonaSismica(5));
    }

    [Fact(DisplayName = "Zona climatica E: query non lancia eccezione")]
    public void ComuniPerZonaClimatica_ZonaE_NonLanciaEccezione()
    {
        // Il dato zona_climatica potrebbe mancare nel DB locale; verifichiamo che la query non fallisca
        var comuni = _atlante.ZoneTerritoriali.ComuniPerZonaClimatica("E");
        Assert.NotNull(comuni); // anche lista vuota è accettabile senza dato nel DB
    }

    [Fact(DisplayName = "Zona climatica non valida lancia ArgumentException")]
    public void ComuniPerZonaClimatica_Invalida_Throws()
    {
        Assert.Throws<ArgumentException>(() => _atlante.ZoneTerritoriali.ComuniPerZonaClimatica("Z"));
    }

    // ── Zona Altimetrica ──────────────────────────────────────────────────────

    [Fact(DisplayName = "Zona altimetrica: query per Pianura non lancia eccezione")]
    public void ComuniPerZonaAltimetrica_Pianura_NonLanciaEccezione()
    {
        var comuni = _atlante.ZoneTerritoriali.ComuniPerZonaAltimetrica(ZonaAltimetrica.Pianura);
        Assert.NotNull(comuni);
        // Accettabile anche lista vuota se il DB non ha ancora la colonna popolata
    }

    [Fact(DisplayName = "Zona altimetrica: query per MontagnaInterna non lancia eccezione")]
    public void ComuniPerZonaAltimetrica_Montagna_NonLanciaEccezione()
    {
        var comuni = _atlante.ZoneTerritoriali.ComuniPerZonaAltimetrica(ZonaAltimetrica.MontagnaInterna);
        Assert.NotNull(comuni);
    }

    [Fact(DisplayName = "Milano ha zona altimetrica Pianura (se il DB è aggiornato)")]
    public void ZonaAltimetrica_Milano_PianuaSePresente()
    {
        var z = _atlante.ZoneTerritoriali.OttieniZone("F205");
        Assert.NotNull(z);
        // Se la colonna è presente nel DB, Milano deve essere pianura
        if (z.ZonaAltimetrica.HasValue)
            Assert.Equal(ZonaAltimetrica.Pianura, z.ZonaAltimetrica.Value);
    }

    [Fact(DisplayName = "Milano ha zona altimetrica anche come proprietà del Comune")]
    public void ZonaAltimetrica_ComuneMilano_Coerente()
    {
        var comune = _atlante.Comuni.DaCodiceBelfiore("F205");
        Assert.NotNull(comune);
        // Se valorizzata, deve essere un valore dell'enum valido
        if (comune.ZonaAltimetrica.HasValue)
            Assert.True(Enum.IsDefined(typeof(ZonaAltimetrica), comune.ZonaAltimetrica.Value));
    }
}
