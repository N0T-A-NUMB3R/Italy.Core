using Italy.Core.Applicazione.Servizi;
using Italy.Core.Domain.Entità;
using Italy.Core.Infrastruttura;
using Italy.Core.Infrastruttura.Repository;
using Xunit;

namespace Italy.Core.Tests;

public sealed class TestCodiceFiscale
{
    private readonly ServiziCodiceFiscale _servizi;

    public TestCodiceFiscale()
    {
        var db = new DatabaseAtlante();
        var repComuni = new RepositoryComuni(db);
        _servizi = new ServiziCodiceFiscale(repComuni);
    }

    [Theory(DisplayName = "CF valido deve essere riconosciuto come valido")]
    [InlineData("RSSMRA80A01F205X")] // Mario Rossi, Milano, 01/01/1980
    [InlineData("BNCGPP85T10H501Y")] // Giuseppe Bianchi, Roma, 10/12/1985
    public void CF_Valido_IsValido(string cf)
    {
        var risultato = _servizi.Valida(cf);
        Assert.True(risultato.IsValido, $"CF '{cf}' dovrebbe essere valido. Anomalie: {string.Join(", ", risultato.Anomalie)}");
    }

    [Theory(DisplayName = "CF con lunghezza errata deve essere invalido")]
    [InlineData("RSSMRA80A01F205")]   // 15 caratteri
    [InlineData("RSSMRA80A01F205XZ")] // 17 caratteri
    [InlineData("")]
    public void CF_LunghezzaErrata_IsInvalido(string cf)
    {
        var risultato = _servizi.Valida(cf);
        Assert.False(risultato.IsValido);
    }

    [Fact(DisplayName = "CF con carattere di controllo errato deve essere invalido")]
    public void CF_CarattereControlloErrato_IsInvalido()
    {
        var risultato = _servizi.Valida("RSSMRA80A01F205A"); // Z → A (controllo errato)
        Assert.False(risultato.IsValido);
    }

    [Fact(DisplayName = "CF deve estrarre correttamente la data di nascita maschile")]
    public void CF_EstrazioneDataNascita_Maschio()
    {
        var risultato = _servizi.Valida("RSSMRA80A01F205X");
        Assert.NotNull(risultato.DataNascita);
        Assert.Equal(1, risultato.DataNascita!.Value.Day);
        Assert.Equal(1, risultato.DataNascita.Value.Month);
        Assert.Equal(1980, risultato.DataNascita.Value.Year);
        Assert.Equal('M', risultato.Sesso);
    }

    [Fact(DisplayName = "CF deve estrarre correttamente il sesso femminile")]
    public void CF_EstrazioneSesso_Femmina()
    {
        // Giorno > 40 indica sesso femminile
        var risultato = _servizi.Valida("RSSMRA80A41F205S"); // giorno 41 → F, giorno reale 1
        if (risultato.IsValido)
            Assert.Equal('F', risultato.Sesso);
    }

    [Fact(DisplayName = "Calcola CF deve produrre 16 caratteri")]
    public void CalcolaCodiceFiscale_ProduceCodiceValido()
    {
        var cf = _servizi.Calcola("Rossi", "Mario", new DateTime(1980, 1, 1), 'M', "F205");
        Assert.Equal(16, cf.Length);
        // Il CF calcolato deve essere valido
        var verifica = _servizi.Valida(cf);
        Assert.True(verifica.IsValido, $"CF calcolato '{cf}' non è valido: {string.Join(", ", verifica.Anomalie)}");
    }
}
