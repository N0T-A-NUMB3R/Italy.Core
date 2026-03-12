using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi per la gestione dei lavoratori frontalieri italiani.
///
/// Normativa di riferimento:
/// - Accordo Italia-Svizzera 1974 (aggiornato con D.Lgs. 209/2023)
/// - Comuni entro 20km dalla linea di confine svizzero (Lugano, Chiasso, Como, Varese, Verbania, Novara)
/// - Accordi con Francia (Art. 15 Conv. Italia-Francia 1989), Austria, Slovenia
///
/// I comuni frontalieri sono pre-calcolati a partire dalle coordinate ISTAT
/// e dai punti di confine ufficiali del Ministero degli Esteri.
/// </summary>
public sealed class ServiziFrontalieri
{
    private readonly IRepositoryComuni _repositoryComuni;
    private readonly ServiziGeo _geo;

    // ── Punti di confine internazionale (WGS84) ───────────────────────────────
    // Questi sono i centroidi delle zone di confine, non i punti esatti.
    // In produzione usare il boundary completo dal GeoPackage ISTAT.

    private static readonly CoordinateGeo[] _confiniSvizzera =
    [
        new() { Latitudine = 45.8295, Longitudine = 8.9988 },  // Luino
        new() { Latitudine = 45.8667, Longitudine = 9.0333 },  // Ponte Tresa
        new() { Latitudine = 45.8308, Longitudine = 9.0500 },  // Stabio area
        new() { Latitudine = 46.4667, Longitudine = 10.1500 }, // Traforo Stelvio
        new() { Latitudine = 45.9833, Longitudine = 7.8500 },  // Monte Rosa area
    ];

    private static readonly CoordinateGeo[] _confiniFrancia =
    [
        new() { Latitudine = 43.7672, Longitudine = 7.4167 },  // Ventimiglia
        new() { Latitudine = 44.1553, Longitudine = 7.0000 },  // Colle di Tenda
        new() { Latitudine = 45.9000, Longitudine = 7.0000 },  // Monte Bianco area
    ];

    private static readonly CoordinateGeo[] _confiniAustria =
    [
        new() { Latitudine = 46.8000, Longitudine = 12.2000 }, // Dobbiaco area
        new() { Latitudine = 46.5333, Longitudine = 13.5667 }, // Tarvisio
        new() { Latitudine = 47.0333, Longitudine = 10.8833 }, // Brennero
    ];

    private static readonly CoordinateGeo[] _confiniSlovenia =
    [
        new() { Latitudine = 45.6500, Longitudine = 13.7000 }, // Trieste area
        new() { Latitudine = 46.0833, Longitudine = 13.5000 }, // Gorizia
        new() { Latitudine = 46.3333, Longitudine = 13.7000 }, // Tarvisio-Slovenia
    ];

    private const double RaggioFrontaliero20Km = 20.0;
    private const double RaggioFrontaliero30Km = 30.0;

    public ServiziFrontalieri(IRepositoryComuni repositoryComuni, ServiziGeo geo)
    {
        _repositoryComuni = repositoryComuni;
        _geo = geo;
    }

    // ── API Pubblica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Verifica se un comune rientra nella zona frontaliera (entro 20km dal confine).
    /// Fondamentale per il calcolo corretto delle buste paga dei frontalieri.
    /// </summary>
    public InfoFrontalieri OttieniInfoFrontalieri(string codiceBelfiore)
    {
        var comune = _repositoryComuni.DaCodiceBelfiore(codiceBelfiore.ToUpperInvariant());
        if (comune == null)
            return new InfoFrontalieri
            {
                CodiceBelfiore = codiceBelfiore,
                IsComuneFrontaliero = false,
                Regime = RegimeFrontaliero.NonApplicabile
            };

        if (!comune.Latitudine.HasValue || !comune.Longitudine.HasValue)
            return new InfoFrontalieri
            {
                CodiceBelfiore = codiceBelfiore,
                NomeComune = comune.DenominazioneUfficiale,
                IsComuneFrontaliero = false,
                Regime = RegimeFrontaliero.NonApplicabile,
                NoteNormative = "Coordinate non disponibili per questo comune. Verifica manuale necessaria."
            };

        var punto = new CoordinateGeo
        {
            Latitudine = comune.Latitudine.Value,
            Longitudine = comune.Longitudine.Value
        };

        // Verifica distanza da ogni confine
        var (distSvizzera, confSvizzera) = DistanzaMinima(punto, _confiniSvizzera);
        var (distFrancia, confFrancia) = DistanzaMinima(punto, _confiniFrancia);
        var (distAustria, confAustria) = DistanzaMinima(punto, _confiniAustria);
        var (distSlovenia, confSlovenia) = DistanzaMinima(punto, _confiniSlovenia);

        // Trova il confine più vicino
        var (minDist, statoConfinante, regime, fasciaSwiss) = TrovaConfineMinimo(
            distSvizzera, distFrancia, distAustria, distSlovenia);

        var isFrontaliero = minDist <= RaggioFrontaliero20Km;

        return new InfoFrontalieri
        {
            CodiceBelfiore = codiceBelfiore,
            NomeComune = comune.DenominazioneUfficiale,
            IsComuneFrontaliero = isFrontaliero,
            DistanzaConfineKm = Math.Round(minDist, 1),
            StatoConfinante = isFrontaliero || minDist <= RaggioFrontaliero30Km ? statoConfinante : null,
            Regime = isFrontaliero ? regime : RegimeFrontaliero.NonApplicabile,
            FasciaSvizzera = regime == RegimeFrontaliero.AccordoSvizzera ? fasciaSwiss : null,
            DataDecorrenza = regime == RegimeFrontaliero.AccordoSvizzera
                ? new DateTime(2024, 1, 1)  // D.Lgs. 209/2023 in vigore dal 1° gen 2024
                : null,
            NoteNormative = CostruisciNoteNormative(regime, isFrontaliero, minDist, fasciaSwiss)
        };
    }

    /// <summary>
    /// Restituisce tutti i comuni italiani nella fascia frontaliera con un dato stato.
    /// </summary>
    public IReadOnlyList<InfoFrontalieri> TuttiComuniFrontalieri(string? statoConfinante = null)
    {
        // In produzione: query SQL con filtro su colonna pre-calcolata is_frontaliero
        // Per ora calcola in-memory sui comuni con coordinate
        return _repositoryComuni.TuttiAttivi()
            .Where(c => c.Latitudine.HasValue && c.Longitudine.HasValue)
            .AsParallel()
            .Select(c => OttieniInfoFrontalieri(c.CodiceBelfiore))
            .Where(info => info.IsComuneFrontaliero &&
                           (statoConfinante == null ||
                            info.StatoConfinante?.Equals(statoConfinante, StringComparison.OrdinalIgnoreCase) == true))
            .OrderBy(info => info.DistanzaConfineKm)
            .ToList();
    }

    // ── Algoritmi Privati ────────────────────────────────────────────────────

    private static (double Distanza, CoordinateGeo PuntoConfine) DistanzaMinima(
        CoordinateGeo punto, CoordinateGeo[] confini)
    {
        var min = double.MaxValue;
        CoordinateGeo? puntoMin = null;

        foreach (var c in confini)
        {
            var d = Haversine(punto, c);
            if (d < min)
            {
                min = d;
                puntoMin = c;
            }
        }

        return (min, puntoMin ?? confini[0]);
    }

    private static (double MinDist, string Stato, RegimeFrontaliero Regime, FasciaFrontalieraSvizzera? Fascia)
        TrovaConfineMinimo(double sv, double fr, double at, double sl)
    {
        if (sv <= fr && sv <= at && sv <= sl)
        {
            var fascia = sv <= 20 ? FasciaFrontalieraSvizzera.ZonaFrontaliera
                       : sv <= 30 ? FasciaFrontalieraSvizzera.ZonaTransizione
                       : (FasciaFrontalieraSvizzera?)null;
            return (sv, "Svizzera", RegimeFrontaliero.AccordoSvizzera, fascia);
        }
        if (fr <= at && fr <= sl)
            return (fr, "Francia", RegimeFrontaliero.AccordoFrancia, null);
        if (at <= sl)
            return (at, "Austria", RegimeFrontaliero.AccordoAustria, null);
        return (sl, "Slovenia", RegimeFrontaliero.AccordoSlovenia, null);
    }

    private static string? CostruisciNoteNormative(
        RegimeFrontaliero regime, bool isFrontaliero, double distanza,
        FasciaFrontalieraSvizzera? fascia)
    {
        if (!isFrontaliero && distanza > 30)
            return null;

        return regime switch
        {
            RegimeFrontaliero.AccordoSvizzera when fascia == FasciaFrontalieraSvizzera.ZonaFrontaliera =>
                "Zona frontaliera svizzera (entro 20km). Regime D.Lgs. 209/2023: " +
                "tassazione concorrente IT/CH. Obbligo dichiarazione modello frontalieri.",
            RegimeFrontaliero.AccordoSvizzera when fascia == FasciaFrontalieraSvizzera.ZonaTransizione =>
                "Zona di transizione svizzera (20-30km). Verifica applicabilità D.Lgs. 209/2023.",
            RegimeFrontaliero.AccordoFrancia =>
                "Zona frontaliera Francia. Art. 15 Convenzione Italia-Francia 1989. " +
                "Tassazione esclusiva nel paese di residenza se lavoro oltre confine.",
            RegimeFrontaliero.AccordoAustria =>
                "Zona frontaliera Austria. Convenzione Italia-Austria 1981.",
            RegimeFrontaliero.AccordoSlovenia =>
                "Zona frontaliera Slovenia. Accordo di stabilizzazione e di associazione.",
            _ => null
        };
    }

    private static double Haversine(CoordinateGeo p1, CoordinateGeo p2)
    {
        const double r = 6371.0;
        var dLat = (p2.Latitudine - p1.Latitudine) * Math.PI / 180;
        var dLon = (p2.Longitudine - p1.Longitudine) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(p1.Latitudine * Math.PI / 180)
              * Math.Cos(p2.Latitudine * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
