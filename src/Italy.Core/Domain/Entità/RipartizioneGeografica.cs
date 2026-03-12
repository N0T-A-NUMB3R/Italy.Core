namespace Italy.Core.Domain.Entità;

/// <summary>
/// Ripartizione geografica ISTAT del territorio italiano.
/// </summary>
public enum RipartizioneGeografica
{
    NordOvest = 1,
    NordEst = 2,
    Centro = 3,
    Sud = 4,
    Isole = 5
}

public enum TipoVariazione
{
    Fusione,
    Scissione,
    CambioDenominazione,
    CambioProvincia,
    Istituzione,
    Soppressione
}

public enum TipoPrefisso
{
    Geografico,
    Mobile,
    TollFree,
    Premium,
    Emergenza,
    VoIP
}

public enum TecnologiaRete
{
    GSM,
    MVNO,
    VoIP,
    UMTS,
    LTE,
    NR
}

public enum TipoFestività
{
    Nazionale,
    SantoPatrono,
    ContrattoCollettivo,
    RegionaleStorica
}

public enum ZonaSismica
{
    Zona1 = 1,
    Zona2 = 2,
    Zona3 = 3,
    Zona4 = 4
}

public enum ZonaClimatica
{
    A, B, C, D, E, F
}

public enum ClasseAreeInterne
{
    Centro,
    Cintura,
    Intermedio,
    Periferico,
    Ultraperiferico
}

public enum FormatoTarga
{
    Attuale,
    Storica,
    Rimorchio,
    Ciclomotore,
    Prova,
    EE,
    CD,
    SCV
}

public enum FormatoRDF
{
    Turtle,
    JsonLd,
    NTriples
}
