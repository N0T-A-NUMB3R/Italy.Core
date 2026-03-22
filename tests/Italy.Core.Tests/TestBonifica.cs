using Italy.Core.Applicazione.Servizi;
using Italy.Core.Domain.Entità;
using Italy.Core.Infrastruttura;
using Italy.Core.Infrastruttura.Repository;
using Xunit;

namespace Italy.Core.Tests;

public sealed class TestBonifica
{
    private readonly ServiziBonificaDati _bonifica;

    public TestBonifica()
    {
        var db = new DatabaseAtlante();
        var repComuni = new RepositoryComuni(db);
        var repCAP = new RepositoryCAP(db);
        var cf = new ServiziCodiceFiscale(repComuni);
        var parser = new ServiziParserIndirizzi(repComuni, repCAP);
        _bonifica = new ServiziBonificaDati(repComuni, repCAP, cf, parser);
    }

    [Fact(DisplayName = "Province soppresse devono essere segnalate")]
    public void VerificaSiglaProvincia_ProvinceSoppresse_SegnalaCorrezione()
    {
        var province = new[] { "CI", "OG", "OT", "VS" };
        foreach (var p in province)
        {
            var r = _bonifica.VerificaSiglaProvincia(p);
            Assert.True(r.RichiedeCorrezione, $"Provincia '{p}' dovrebbe essere segnalata come soppressa.");
            Assert.Equal(TipoBonifica.SiglaProvinciaAggiornata, r.Tipo);
            Assert.NotNull(r.ValoreSuggerito);
        }
    }

    [Fact(DisplayName = "Provincia attiva non deve richiedere correzione")]
    public void VerificaSiglaProvincia_ProvinciaAttiva_NessunaCorrezione()
    {
        var r = _bonifica.VerificaSiglaProvincia("MI");
        Assert.False(r.RichiedeCorrezione);
    }

    [Fact(DisplayName = "AnalizzaIndirizzo con CAP e comune coerenti non segnala anomalie")]
    public void AnalizzaIndirizzo_Coerente_NessunaAnomalia()
    {
        // Questo test richiede DB popolato
        var correzioni = _bonifica.AnalizzaIndirizzo("Loano", "17025", "SV");
        var errori = correzioni.Where(c => c.Tipo == TipoBonifica.CAPNonCorrispondeAlComune).ToList();
        Assert.Empty(errori);
    }

    [Fact(DisplayName = "ElaboraBatch restituisce report con totale corretto")]
    public void ElaboraBatch_RestituisceReportCorretto()
    {
        var records = new List<RecordDaBonificare>
        {
            new() { NomeComune = "Milano", CAP = "20121", SiglaProvincia = "MI" },
            new() { NomeComune = "Roma", CAP = "00100", SiglaProvincia = "RM" },
        };

        var report = _bonifica.ElaboraBatch(records);
        Assert.Equal(2, report.TotaleRecord);
        Assert.True(report.PercentualePulizia >= 0 && report.PercentualePulizia <= 100);
    }
}
