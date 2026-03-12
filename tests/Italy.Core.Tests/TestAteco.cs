using Italy.Core.Applicazione.Servizi;
using Xunit;

namespace Italy.Core.Tests;

public sealed class TestAteco
{
    private readonly Atlante _atlante = new();

    [Fact(DisplayName = "Sezioni ATECO: devono esistere almeno 20 sezioni")]
    public void Sezioni_AtLeast20()
    {
        var sezioni = _atlante.ATECO.Sezioni();
        Assert.True(sezioni.Count >= 20, $"Attese ≥20 sezioni, trovate {sezioni.Count}");
    }

    [Theory(DisplayName = "Lookup ATECO per codice diretto")]
    [InlineData("A")]
    [InlineData("C")]
    [InlineData("01")]
    public void DaCodice_CodicePrincipale_Trovato(string codice)
    {
        var r = _atlante.ATECO.DaCodice(codice);
        Assert.NotNull(r);
        Assert.Equal(codice, r.Codice);
        Assert.False(string.IsNullOrWhiteSpace(r.Descrizione));
    }

    [Fact(DisplayName = "Codice ATECO inesistente restituisce null")]
    public void DaCodice_CodiceInesistente_Null()
    {
        var r = _atlante.ATECO.DaCodice("ZZZZZ");
        Assert.Null(r);
    }

    [Fact(DisplayName = "Ricerca ATECO per testo restituisce risultati")]
    public void Cerca_Testo_TrovaRisultati()
    {
        var r = _atlante.ATECO.Cerca("agricoltura");
        Assert.NotEmpty(r);
        Assert.All(r, item => Assert.False(string.IsNullOrWhiteSpace(item.Codice)));
    }

    [Fact(DisplayName = "Sottocategorie di una divisione non sono vuote")]
    public void SottoCategorie_Divisione_NonVuote()
    {
        var figli = _atlante.ATECO.SottoCategorie("01");
        Assert.NotEmpty(figli);
    }

    [Fact(DisplayName = "DescrizioneCompleta costruisce la catena gerarchica")]
    public void DescrizioneCompleta_Classe_ContieneSeparatori()
    {
        // Classe 01.11 — Coltivazione di cereali
        var desc = _atlante.ATECO.DescrizioneCompleta("01.11");
        Assert.NotNull(desc);
        Assert.Contains(">", desc);
    }
}
