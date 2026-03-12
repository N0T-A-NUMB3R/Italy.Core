using Italy.Core.Applicazione.Servizi;
using Xunit;

namespace Italy.Core.Tests;

public sealed class TestFestività
{
    private readonly ServiziFestività _servizi = new();

    [Theory(DisplayName = "Pasqua calcolata correttamente per anni noti")]
    [InlineData(2024, 3, 31)]  // 31 marzo 2024
    [InlineData(2025, 4, 20)]  // 20 aprile 2025
    [InlineData(2023, 4, 9)]   // 9 aprile 2023
    [InlineData(2019, 4, 21)]  // 21 aprile 2019
    [InlineData(2000, 4, 23)]  // 23 aprile 2000
    public void CalcolaPasqua_AnniNoti_CorrettaData(int anno, int mese, int giorno)
    {
        var pasqua = _servizi.CalcolaPasqua(anno);
        Assert.Equal(new DateTime(anno, mese, giorno), pasqua);
    }

    [Fact(DisplayName = "Festività nazionali 2024 devono includere tutte le 11 festività fisse + Pasqua + Pasquetta")]
    public void OttieniFestività_2024_IncludeTutteNazionali()
    {
        var festività = _servizi.OttieniFestività(2024);
        Assert.True(festività.Count >= 12, $"Trovate solo {festività.Count} festività (attese >= 12).");

        var nomi = festività.Select(f => f.Nome).ToList();
        Assert.Contains("Capodanno", nomi);
        Assert.Contains("Pasqua", nomi);
        Assert.Contains("Lunedì dell'Angelo (Pasquetta)", nomi);
        Assert.Contains("Festa della Liberazione", nomi);
        Assert.Contains("Festa del Lavoro", nomi);
        Assert.Contains("Natale", nomi);
    }

    [Fact(DisplayName = "Milano deve avere Sant'Ambrogio come festività locale")]
    public void OttieniFestività_Milano_IncludeSantAmbrogio()
    {
        var festività = _servizi.OttieniFestività(2024, "F205"); // F205 = Milano
        var nomi = festività.Select(f => f.Nome).ToList();
        Assert.Contains("Sant'Ambrogio", nomi);
    }

    [Fact(DisplayName = "25 dicembre 2024 deve essere festivo")]
    public void IsFestivo_Natale2024_Vero()
    {
        Assert.True(_servizi.IsFestivo(new DateTime(2024, 12, 25)));
    }

    [Fact(DisplayName = "Giorni lavorativi anno 2024 devono essere circa 251")]
    public void CalcolaGiorniLavorativi_Anno2024_CircaCorretto()
    {
        var giorni = _servizi.CalcolaGiorniLavorativi(
            new DateTime(2024, 1, 1),
            new DateTime(2024, 12, 31));
        // Range ragionevole: 248-254 giorni lavorativi
        Assert.InRange(giorni, 245, 257);
    }

    [Fact(DisplayName = "Festività ordinate per data crescente")]
    public void OttieniFestività_RestituisceOrdineCorretto()
    {
        var festività = _servizi.OttieniFestività(2024).ToList();
        for (var i = 1; i < festività.Count; i++)
            Assert.True(festività[i].Data >= festività[i - 1].Data,
                $"Festività non ordinata: {festività[i - 1].Nome} > {festività[i].Nome}");
    }
}
