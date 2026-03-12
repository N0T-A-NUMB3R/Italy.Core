using Italy.Core.Domain.Entità;
using Italy.Core.Infrastruttura;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi geografici avanzati: distanza tra comuni, lookup NUTS, comuni nel raggio.
/// Richiede le coordinate WGS84 presenti nel database (da GeoNames).
/// </summary>
public sealed class ServiziGeoDistanza
{
    private readonly DatabaseAtlante _database;

    public ServiziGeoDistanza(DatabaseAtlante database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    // ── Distanza ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Calcola la distanza in km tra due comuni tramite formula Haversine (WGS84).
    /// Restituisce null se uno dei due comuni non ha coordinate nel DB.
    /// Es: DistanzaKm("F205", "H501") → ~479 km (Milano → Roma)
    /// </summary>
    public double? DistanzaKm(string codiceBelfioreA, string codiceBelfioreB)
    {
        var coordA = OttieniCoordinate(codiceBelfioreA);
        var coordB = OttieniCoordinate(codiceBelfioreB);
        if (coordA == null || coordB == null) return null;
        return Haversine(coordA.Value.Lat, coordA.Value.Lng, coordB.Value.Lat, coordB.Value.Lng);
    }

    /// <summary>
    /// Restituisce i comuni entro il raggio specificato (km) dal comune centrale.
    /// Ordinati per distanza crescente. Esclude il comune centrale stesso.
    /// Es: ComuniNelRaggio("F205", 20) → comuni entro 20 km da Milano
    /// </summary>
    public IReadOnlyList<(string CodiceBelfiore, string Denominazione, double DistanzaKm)>
        ComuniNelRaggio(string codiceBelfiore, double raggioKm)
    {
        if (raggioKm <= 0) throw new ArgumentException("Il raggio deve essere > 0.", nameof(raggioKm));

        var centro = OttieniCoordinate(codiceBelfiore);
        if (centro == null) return Array.Empty<(string, string, double)>();

        // Approssimazione bounding box per pre-filtrare (1° lat ≈ 111 km)
        var deltaLat = raggioKm / 111.0;
        var deltaLng = raggioKm / (111.0 * Math.Cos(centro.Value.Lat * Math.PI / 180.0));

        var candidati = _database.Esegui(
            """
            SELECT codice_belfiore, denominazione, latitudine, longitudine
            FROM comuni
            WHERE is_attivo = 1
              AND codice_belfiore != @cb
              AND latitudine IS NOT NULL
              AND longitudine IS NOT NULL
              AND latitudine  BETWEEN @latMin AND @latMax
              AND longitudine BETWEEN @lngMin AND @lngMax
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant());
                cmd.Parameters.AddWithValue("@latMin", centro.Value.Lat - deltaLat);
                cmd.Parameters.AddWithValue("@latMax", centro.Value.Lat + deltaLat);
                cmd.Parameters.AddWithValue("@lngMin", centro.Value.Lng - deltaLng);
                cmd.Parameters.AddWithValue("@lngMax", centro.Value.Lng + deltaLng);
            },
            r => (
                CodiceBelfiore: r.GetString(0),
                Denominazione: r.GetString(1),
                Lat: r.GetDouble(2),
                Lng: r.GetDouble(3)));

        return candidati
            .Select(c => (
                c.CodiceBelfiore,
                c.Denominazione,
                DistanzaKm: Haversine(centro.Value.Lat, centro.Value.Lng, c.Lat, c.Lng)))
            .Where(c => c.DistanzaKm <= raggioKm)
            .OrderBy(c => c.DistanzaKm)
            .ToList();
    }

    // ── NUTS ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce i codici NUTS (1, 2, 3) di un comune.
    /// Es: OttieniNUTS("F205") → { NUTS1: "ITC", NUTS2: "ITC4", NUTS3: "ITC4C" }
    /// </summary>
    public (string? NUTS1, string? NUTS2, string? NUTS3)? OttieniNUTS(string codiceBelfiore)
    {
        if (string.IsNullOrWhiteSpace(codiceBelfiore)) return null;

        var risultati = _database.Esegui(
            "SELECT nuts1, nuts2, nuts3 FROM comuni WHERE codice_belfiore = @cb LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.Trim().ToUpperInvariant()),
            r =>
            {
                var o1 = r.GetOrdinal("nuts1");
                var o2 = r.GetOrdinal("nuts2");
                var o3 = r.GetOrdinal("nuts3");
                return (
                    NUTS1: r.IsDBNull(o1) ? null : r.GetString(o1),
                    NUTS2: r.IsDBNull(o2) ? null : r.GetString(o2),
                    NUTS3: r.IsDBNull(o3) ? null : r.GetString(o3));
            });

        return risultati.Count > 0 ? risultati[0] : null;
    }

    /// <summary>
    /// Restituisce tutti i comuni appartenenti a un codice NUTS3 (provincia europea).
    /// Es: ComuniPerNUTS3("ITC4C") → comuni della provincia di Milano
    /// </summary>
    public IReadOnlyList<string> ComuniPerNUTS3(string nuts3)
    {
        if (string.IsNullOrWhiteSpace(nuts3)) return Array.Empty<string>();
        return _database.Esegui(
            "SELECT codice_belfiore FROM comuni WHERE nuts3 = @n AND is_attivo = 1 ORDER BY denominazione",
            cmd => cmd.Parameters.AddWithValue("@n", nuts3.Trim().ToUpperInvariant()),
            r => r.GetString(0));
    }

    /// <summary>
    /// Restituisce tutti i comuni appartenenti a un codice NUTS2 (regione europea).
    /// Es: ComuniPerNUTS2("ITC4") → comuni della Lombardia
    /// </summary>
    public IReadOnlyList<string> ComuniPerNUTS2(string nuts2)
    {
        if (string.IsNullOrWhiteSpace(nuts2)) return Array.Empty<string>();
        return _database.Esegui(
            "SELECT codice_belfiore FROM comuni WHERE nuts2 = @n AND is_attivo = 1 ORDER BY denominazione",
            cmd => cmd.Parameters.AddWithValue("@n", nuts2.Trim().ToUpperInvariant()),
            r => r.GetString(0));
    }

    // ── Helper Privati ────────────────────────────────────────────────────────

    private (double Lat, double Lng)? OttieniCoordinate(string codiceBelfiore)
    {
        var risultati = _database.Esegui(
            "SELECT latitudine, longitudine FROM comuni WHERE codice_belfiore = @cb AND latitudine IS NOT NULL LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.Trim().ToUpperInvariant()),
            r => (Lat: r.GetDouble(0), Lng: r.GetDouble(1)));
        return risultati.Count > 0 ? risultati[0] : null;
    }

    /// <summary>Formula Haversine: distanza in km tra due punti WGS84.</summary>
    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // raggio terrestre in km
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 2);
    }
}
