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
        throw new NotImplementedException("OttieniGeoJSON richiede dati ISTAT GeoPackage non inclusi nella versione base.");
    }

    // ── Proiezioni Cartografiche ───────────────────────────────────────────────

    /// <summary>
    /// Converte coordinate WGS84 in Gauss-Boaga (sistema di riferimento catasto italiano, Roma40).
    /// Fuso Ovest: longitudine 6°–12° E (EPSG:3003). Fuso Est: longitudine 12°–18° E (EPSG:3004).
    /// </summary>
    /// <param name="latitudine">Latitudine WGS84 in gradi decimali.</param>
    /// <param name="longitudine">Longitudine WGS84 in gradi decimali.</param>
    /// <returns>Coordinate Gauss-Boaga (Est, Nord) e fuso (Ovest/Est).</returns>
    public (double Est, double Nord, string Fuso) ConvertInGaussBoaga(double latitudine, double longitudine)
    {
        // Parametri ellissoide Internazionale 1924 (Hayford) — datum Roma40
        const double a = 6378388.0;       // semiasse maggiore
        const double f = 1.0 / 297.0;     // schiacciamento
        const double b = a * (1 - f);
        const double e2 = 1 - (b * b) / (a * a);
        const double k0 = 0.9996;         // fattore di scala

        // Meridiano centrale: Fuso Ovest = 9°E (Monte Mario), Fuso Est = 15°E
        double lambda0Deg = longitudine < 12.0 ? 9.0 : 15.0;
        string fuso = longitudine < 12.0 ? "Ovest" : "Est";
        double falsoEst = longitudine < 12.0 ? 1_500_000.0 : 2_520_000.0;

        double latRad = latitudine * Math.PI / 180.0;
        double lonRad = longitudine * Math.PI / 180.0;
        double lambda0Rad = lambda0Deg * Math.PI / 180.0;

        double e = Math.Sqrt(e2);
        double N = a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad));
        double T = Math.Tan(latRad) * Math.Tan(latRad);
        double C = e2 / (1 - e2) * Math.Cos(latRad) * Math.Cos(latRad);
        double A = Math.Cos(latRad) * (lonRad - lambda0Rad);

        double e4 = e2 * e2; double e6 = e4 * e2;
        double M = a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latRad
                      - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latRad)
                      + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latRad)
                      - 35 * e6 / 3072 * Math.Sin(6 * latRad));

        double est  = falsoEst + k0 * N * (A + (1 - T + C) * A * A * A / 6
                    + (5 - 18 * T + T * T + 72 * C) * A * A * A * A * A / 120);
        double nord = k0 * (M + N * Math.Tan(latRad) * (A * A / 2
                    + (5 - T + 9 * C + 4 * C * C) * A * A * A * A / 24
                    + (61 - 58 * T + T * T + 600 * C) * A * A * A * A * A * A / 720));

        return (Math.Round(est, 3), Math.Round(nord, 3), fuso);
    }

    /// <summary>
    /// Converte le coordinate WGS84 di un comune (centroide) in Gauss-Boaga.
    /// </summary>
    /// <param name="codiceBelfiore">Codice catastale del comune (es. "F205" per Milano).</param>
    /// <returns>Coordinate Gauss-Boaga, o null se il comune non ha coordinate.</returns>
    public (double Est, double Nord, string Fuso)? ConvertComuneInGaussBoaga(string codiceBelfiore)
    {
        var coord = OttieniCoordinate(codiceBelfiore);
        if (coord == null) return null;
        return ConvertInGaussBoaga(coord.Latitudine, coord.Longitudine);
    }

    /// <summary>
    /// Converte coordinate WGS84 in UTM (Universal Transverse Mercator).
    /// Seleziona automaticamente il fuso UTM corretto (32N per Italia occidentale, 33N per orientale).
    /// </summary>
    /// <param name="latitudine">Latitudine WGS84 in gradi decimali.</param>
    /// <param name="longitudine">Longitudine WGS84 in gradi decimali.</param>
    /// <returns>Coordinate UTM (fuso EPSG, Est, Nord) — es. fuso 32 = EPSG:32632.</returns>
    public (int Fuso, double Est, double Nord) ConvertInUTM(double latitudine, double longitudine)
    {
        // WGS84
        const double a = 6378137.0;
        const double f = 1.0 / 298.257223563;
        const double b = a * (1 - f);
        const double e2 = 1 - (b * b) / (a * a);
        const double k0 = 0.9996;
        const double falsoEst = 500_000.0;

        int fuso = (int)Math.Floor((longitudine + 180.0) / 6.0) + 1;
        double lambda0Rad = ((fuso - 1) * 6.0 - 180.0 + 3.0) * Math.PI / 180.0;

        double latRad = latitudine * Math.PI / 180.0;
        double lonRad = longitudine * Math.PI / 180.0;

        double e = Math.Sqrt(e2);
        double N = a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad));
        double T = Math.Tan(latRad) * Math.Tan(latRad);
        double C = e2 / (1 - e2) * Math.Cos(latRad) * Math.Cos(latRad);
        double A = Math.Cos(latRad) * (lonRad - lambda0Rad);

        double e4 = e2 * e2; double e6 = e4 * e2;
        double M = a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latRad
                      - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latRad)
                      + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latRad)
                      - 35 * e6 / 3072 * Math.Sin(6 * latRad));

        double est  = falsoEst + k0 * N * (A + (1 - T + C) * A * A * A / 6
                    + (5 - 18 * T + T * T + 72 * C - 58 * (e2 / (1 - e2)))
                      * A * A * A * A * A / 120);
        double nord = k0 * (M + N * Math.Tan(latRad) * (A * A / 2
                    + (5 - T + 9 * C + 4 * C * C) * A * A * A * A / 24
                    + (61 - 58 * T + T * T + 600 * C - 330 * (e2 / (1 - e2)))
                      * A * A * A * A * A * A / 720));

        // Emisfero nord: nessun falso nord. Sud: falso nord = 10.000.000.
        if (latitudine < 0) nord += 10_000_000.0;

        return (fuso, Math.Round(est, 3), Math.Round(nord, 3));
    }

    /// <summary>
    /// Converte le coordinate WGS84 di un comune (centroide) in UTM.
    /// </summary>
    /// <param name="codiceBelfiore">Codice catastale del comune.</param>
    /// <returns>Coordinate UTM, o null se il comune non ha coordinate.</returns>
    public (int Fuso, double Est, double Nord)? ConvertComuneInUTM(string codiceBelfiore)
    {
        var coord = OttieniCoordinate(codiceBelfiore);
        if (coord == null) return null;
        return ConvertInUTM(coord.Latitudine, coord.Longitudine);
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
