using Italy.Core;
using Italy.Core.Domain.Entità;
using Xunit;

namespace Italy.Core.Tests;

/// <summary>
/// Test di integrità storica — OBBLIGATORIO in ogni build.
/// Verifica la coerenza tra comuni soppressi e successori.
/// </summary>
public sealed class TestIntegritàStorica
{
    // Nota: questi test richiedono il database italy.db popolato.
    // In CI, il database viene generato dallo script build_atlante.py prima dei test.

    [Fact(DisplayName = "Ogni comune soppresso con successore deve puntare a un comune attivo")]
    public void ComuniSoppressi_ConSuccessore_DeveEsistereEssereAttivo()
    {
        using var atlante = new Atlante();

        var comuniSoppressi = atlante.Comuni
            .TuttiInclusiSoppressi()
            .Where(c => !c.IsAttivo && c.CodiceSuccessore != null)
            .ToList();

        var errori = new List<string>();
        foreach (var comune in comuniSoppressi)
        {
            var successore = atlante.Comuni.TrovaDaCodiceBelfiore(comune.CodiceSuccessore!);
            if (successore == null)
                errori.Add($"{comune.CodiceBelfiore} ({comune.DenominazioneUfficiale}): successore '{comune.CodiceSuccessore}' non trovato.");
            else if (!successore.IsAttivo)
                errori.Add($"{comune.CodiceBelfiore} ({comune.DenominazioneUfficiale}): successore '{comune.CodiceSuccessore}' ({successore.DenominazioneUfficiale}) non è attivo.");
        }

        Assert.True(errori.Count == 0,
            $"Trovate {errori.Count} anomalie di integrità storica:\n{string.Join("\n", errori)}");
    }

    [Fact(DisplayName = "Nessun comune attivo deve avere data di soppressione")]
    public void ComuniAttivi_NonDevonAvereDataSoppressione()
    {
        using var atlante = new Atlante();

        var anomalie = atlante.Comuni
            .TuttiAttivi()
            .Where(c => c.DataSoppressione.HasValue)
            .Select(c => $"{c.CodiceBelfiore} ({c.DenominazioneUfficiale}): attivo ma con DataSoppressione = {c.DataSoppressione:yyyy-MM-dd}")
            .ToList();

        Assert.True(anomalie.Count == 0,
            $"Trovati {anomalie.Count} comuni attivi con data soppressione:\n{string.Join("\n", anomalie)}");
    }

    [Fact(DisplayName = "Il totale comuni attivi deve essere plausibile (tra 7000 e 8000)")]
    public void TotaleComuni_DevEsserePlausibile()
    {
        using var atlante = new Atlante();
        var totale = atlante.Comuni.ContaTotale();
        Assert.InRange(totale, 7_000, 8_500);
    }

    [Fact(DisplayName = "Ogni comune deve avere Codice Belfiore e ISTAT non vuoti")]
    public void TuttiComuni_CodiciNonVuoti()
    {
        using var atlante = new Atlante();
        var anomalie = atlante.Comuni
            .TuttiAttivi()
            .Where(c => string.IsNullOrWhiteSpace(c.CodiceBelfiore) || string.IsNullOrWhiteSpace(c.CodiceISTAT))
            .Select(c => $"Comune con codice mancante: '{c.DenominazioneUfficiale}'")
            .ToList();

        Assert.True(anomalie.Count == 0,
            $"Trovati {anomalie.Count} comuni con codici mancanti:\n{string.Join("\n", anomalie)}");
    }

    [Fact(DisplayName = "Il database Atlante deve contenere metadati versione")]
    public void Database_DevContenereMetadatiVersione()
    {
        using var atlante = new Atlante();
        Assert.NotNull(atlante.VersioneDati);
        Assert.NotEmpty(atlante.VersioneDati);
    }
}
