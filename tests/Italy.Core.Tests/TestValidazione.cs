using Italy.Core.Applicazione.Servizi;
using Xunit;

namespace Italy.Core.Tests;

public sealed class TestValidazione
{
    private readonly ServiziValidazione _servizi = new();

    // ── Partita IVA ─────────────────────────────────────────────────────────

    [Theory(DisplayName = "Partita IVA valida deve essere accettata")]
    [InlineData("06655971007")]  // Comune di Roma
    [InlineData("10773060016")]  // UniCredit SpA
    public void PartitaIVA_Valida_IsAccettata(string piva)
    {
        var r = _servizi.ValidaPartitaIVA(piva);
        Assert.True(r.IsValida, $"P.IVA '{piva}' dovrebbe essere valida. Anomalie: {string.Join(", ", r.Anomalie)}");
    }

    [Theory(DisplayName = "Partita IVA non valida deve essere rifiutata")]
    [InlineData("12345678901")]   // checksum errato
    [InlineData("1234567890")]    // 10 cifre
    [InlineData("ABCDEFGHIJK")]   // non numerica
    [InlineData("")]
    public void PartitaIVA_NonValida_IsRifiutata(string piva)
    {
        var r = _servizi.ValidaPartitaIVA(piva);
        Assert.False(r.IsValida);
    }

    // ── IBAN ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "IBAN italiano valido deve essere accettato")]
    public void IBAN_Valido_IsAccettato()
    {
        var r = _servizi.ValidaIBAN("IT60X0542811101000000123456");
        Assert.True(r.IsValido, $"IBAN dovrebbe essere valido. Anomalie: {string.Join(", ", r.Anomalie)}");
        Assert.NotNull(r.IbanFormattato);
    }

    [Theory(DisplayName = "IBAN non valido deve essere rifiutato")]
    [InlineData("IT00X0542811101000000123456")] // check digits errati
    [InlineData("GB29NWBK60161331926819")]       // IBAN non italiano
    [InlineData("IT601234")]                      // troppo corto
    [InlineData("")]
    public void IBAN_NonValido_IsRifiutato(string iban)
    {
        var r = _servizi.ValidaIBAN(iban);
        Assert.False(r.IsValido);
    }

    // ── Targa ───────────────────────────────────────────────────────────────

    [Theory(DisplayName = "Targa formato attuale deve essere valida")]
    [InlineData("AB123CD")]
    [InlineData("ZZ999ZZ")]
    public void Targa_FormatoAttuale_IsValida(string targa)
    {
        var r = _servizi.ValidaTarga(targa);
        Assert.True(r.IsValida);
        Assert.Equal(Domain.Entità.FormatoTarga.Attuale, r.Formato);
    }

    [Theory(DisplayName = "Targhe speciali devono essere riconosciute")]
    [InlineData("CD123AB")]
    [InlineData("SCV001")]
    public void Targa_Speciale_IsRiconosciuta(string targa)
    {
        var r = _servizi.ValidaTarga(targa);
        Assert.True(r.IsValida);
    }

    [Theory(DisplayName = "Targa invalida deve essere rifiutata")]
    [InlineData("12345AB")]  // inizia con cifre
    [InlineData("A")]
    [InlineData("")]
    public void Targa_Invalida_IsRifiutata(string targa)
    {
        var r = _servizi.ValidaTarga(targa);
        Assert.False(r.IsValida);
    }
}
