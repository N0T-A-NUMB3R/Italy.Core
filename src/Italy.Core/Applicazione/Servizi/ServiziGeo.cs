using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi geospaziali per comuni italiani.
/// Utilizza la formula di Haversine per il calcolo delle distanze.
/// </summary>
public sealed class ServiziGeo : IProviderGeografico
{
    private const double RaggioTerraKm = 6371.0;

    private readonly IRepositoryComuni _repository;

    public ServiziGeo(IRepositoryComuni repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public CoordinateGeo? OttieniCoordinate(string codiceBelfiore)
    {
        var comune = _repository.DaCodiceBelfiore(codiceBelfiore.ToUpperInvariant());
        if (comune?.Latitudine == null || comune.Longitudine == null)
            return null;
        return new CoordinateGeo
        {
            Latitudine = comune.Latitudine.Value,
            Longitudine = comune.Longitudine.Value,
            Altitudine = comune.AltitudineMetri
        };
    }

    /// <summary>
    /// Calcola la distanza in km tra due comuni (linea d'aria, formula Haversine).
    /// Es: CalcolaDistanzaKm("F205", "L219") → ~126 km (Milano-Torino)
    /// </summary>
    public double? CalcolaDistanzaKm(string codiceBelfiore1, string codiceBelfiore2)
    {
        var c1 = OttieniCoordinate(codiceBelfiore1);
        var c2 = OttieniCoordinate(codiceBelfiore2);
        if (c1 == null || c2 == null) return null;
        return Haversine(c1, c2);
    }

    /// <summary>
    /// Trova tutti i comuni entro un raggio specificato da un punto geografico.
    /// </summary>
    public IReadOnlyList<Comune> TrovaNelRaggio(CoordinateGeo punto, double raggioKm)
    {
        return _repository.TuttiAttivi()
            .Where(c => c.Latitudine.HasValue && c.Longitudine.HasValue)
            .Select(c => new
            {
                Comune = c,
                Distanza = Haversine(punto, new CoordinateGeo
                {
                    Latitudine = c.Latitudine!.Value,
                    Longitudine = c.Longitudine!.Value
                })
            })
            .Where(x => x.Distanza <= raggioKm)
            .OrderBy(x => x.Distanza)
            .Select(x => x.Comune)
            .ToList();
    }

    /// <summary>Restituisce il GeoJSON del confine comunale (se disponibile nel DB).</summary>
    public string? OttieniGeoJSON(string codiceBelfiore)
    {
        // Il GeoJSON viene caricato dalla tabella `confini` nel database Atlante
        // Implementazione nella classe Repository concreta
        throw new NotImplementedException("OttieniGeoJSON richiede dati ISTAT GeoPackage non inclusi nella versione base.");
    }

    private static double Haversine(CoordinateGeo p1, CoordinateGeo p2)
    {
        var dLat = GradiARadianti(p2.Latitudine - p1.Latitudine);
        var dLon = GradiARadianti(p2.Longitudine - p1.Longitudine);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(GradiARadianti(p1.Latitudine))
              * Math.Cos(GradiARadianti(p2.Latitudine))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return RaggioTerraKm * c;
    }

    private static double GradiARadianti(double gradi) => gradi * Math.PI / 180.0;
}
