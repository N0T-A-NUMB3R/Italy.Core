namespace Italy.Core.Domain.Entità;

// ZonaSismica e ZonaClimatica sono definiti in RipartizioneGeografica.cs

/// <summary>
/// Informazioni sulle zone territoriali di un comune:
/// classificazione sismica, climatica, altimetrica e coordinate geografiche.
/// </summary>
public sealed record InfoZoneTerritoriali(
    string CodiceBelfiore,
    ZonaSismica? ZonaSismica,
    ZonaClimatica? ZonaClimatica,
    double? Latitudine,
    double? Longitudine,
    ZonaAltimetrica? ZonaAltimetrica = null);
