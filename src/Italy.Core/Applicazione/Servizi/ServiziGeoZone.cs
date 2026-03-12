using Italy.Core.Domain.Entità;
using Italy.Core.Infrastruttura;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi per le zone territoriali dei comuni italiani:
/// classificazione sismica (PCM 3274/2003) e climatica (DPR 412/1993).
///
/// Complementa <see cref="ServiziGeo"/> senza modificarne il sorgente.
/// Richiede il database Atlante per l'accesso ai dati.
/// </summary>
public sealed class ServiziZoneTerritoriali
{
    private readonly DatabaseAtlante _database;

    public ServiziZoneTerritoriali(DatabaseAtlante database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    // ── Lookup Zone per Comune ───────────────────────────────────────────────

    /// <summary>
    /// Restituisce le zone territoriali (sismica, climatica, coordinate) per un comune.
    /// Es: OttieniZone("F205") → { ZonaSismica: Zona3, ZonaClimatica: E, Latitudine: 45.46, Longitudine: 9.19 }
    /// Restituisce null se il codice Belfiore non è trovato.
    /// </summary>
    public InfoZoneTerritoriali? OttieniZone(string codiceBelfiore)
    {
        if (string.IsNullOrWhiteSpace(codiceBelfiore)) return null;

        var risultati = _database.Esegui(
            """
            SELECT codice_belfiore, zona_sismica, zona_climatica, latitudine, longitudine
            FROM comuni
            WHERE codice_belfiore = @cb
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.Trim().ToUpperInvariant()),
            MappaInfoZone);

        return risultati.FirstOrDefault();
    }

    // ── Filtro per Zona Sismica ──────────────────────────────────────────────

    /// <summary>
    /// Restituisce i codici Belfiore di tutti i comuni in una determinata zona sismica.
    /// Es: ComuniPerZonaSismica(1) → comuni ad alta sismicità.
    /// </summary>
    public IReadOnlyList<string> ComuniPerZonaSismica(int zona)
    {
        if (zona < 1 || zona > 4)
            throw new ArgumentException($"Zona sismica non valida: {zona}. Valori ammessi: 1-4.", nameof(zona));

        return _database.Esegui(
            """
            SELECT codice_belfiore
            FROM comuni
            WHERE zona_sismica = @z
              AND is_attivo = 1
            ORDER BY codice_belfiore
            """,
            cmd => cmd.Parameters.AddWithValue("@z", zona),
            r => r.GetString(0));
    }

    // ── Filtro per Zona Climatica ─────────────────────────────────────────────

    /// <summary>
    /// Restituisce i codici Belfiore di tutti i comuni in una determinata zona climatica.
    /// Es: ComuniPerZonaClimatica("E") → comuni con 2101–3000 gradi-giorno.
    /// </summary>
    public IReadOnlyList<string> ComuniPerZonaClimatica(string zona)
    {
        if (string.IsNullOrWhiteSpace(zona)) return Array.Empty<string>();

        var zonaUpper = zona.Trim().ToUpperInvariant();
        if (zonaUpper.Length != 1 || zonaUpper[0] < 'A' || zonaUpper[0] > 'F')
            throw new ArgumentException($"Zona climatica non valida: '{zona}'. Valori ammessi: A-F.", nameof(zona));

        return _database.Esegui(
            """
            SELECT codice_belfiore
            FROM comuni
            WHERE zona_climatica = @z
              AND is_attivo = 1
            ORDER BY codice_belfiore
            """,
            cmd => cmd.Parameters.AddWithValue("@z", zonaUpper),
            r => r.GetString(0));
    }

    // ── Helper Privati ────────────────────────────────────────────────────────

    private static InfoZoneTerritoriali MappaInfoZone(Microsoft.Data.Sqlite.SqliteDataReader r)
    {
        var ordSismica = r.GetOrdinal("zona_sismica");
        var ordClimatica = r.GetOrdinal("zona_climatica");
        var ordLat = r.GetOrdinal("latitudine");
        var ordLng = r.GetOrdinal("longitudine");

        ZonaSismica? zonaSismica = null;
        if (!r.IsDBNull(ordSismica))
        {
            var val = r.GetInt32(ordSismica);
            if (Enum.IsDefined(typeof(ZonaSismica), val))
                zonaSismica = (ZonaSismica)val;
        }

        ZonaClimatica? zonaClimatica = null;
        if (!r.IsDBNull(ordClimatica))
        {
            var valStr = r.GetString(ordClimatica).Trim().ToUpperInvariant();
            if (valStr.Length == 1)
            {
                try { zonaClimatica = (ZonaClimatica)Enum.Parse(typeof(ZonaClimatica), valStr); }
                catch (ArgumentException) { /* valore sconosciuto */ }
            }
        }

        return new InfoZoneTerritoriali(
            CodiceBelfiore: r.GetString(r.GetOrdinal("codice_belfiore")),
            ZonaSismica: zonaSismica,
            ZonaClimatica: zonaClimatica,
            Latitudine: r.IsDBNull(ordLat) ? null : r.GetDouble(ordLat),
            Longitudine: r.IsDBNull(ordLng) ? null : r.GetDouble(ordLng));
    }
}
