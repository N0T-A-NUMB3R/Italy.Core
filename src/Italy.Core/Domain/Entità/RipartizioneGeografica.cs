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

/// <summary>
/// Zona altimetrica ISTAT del comune (classificazione ufficiale).
/// Fonte: ISTAT, colonna "zona_altimetrica" nel file Elenco-comuni-italiani.csv.
/// </summary>
public enum ZonaAltimetrica
{
    /// <summary>Pianura: altitudine media ≤ 300 m, pendenza bassa.</summary>
    Pianura = 1,
    /// <summary>Collina interna: rilievi non costieri tra 300 e 600 m circa.</summary>
    CollinaInterna = 2,
    /// <summary>Collina litoranea: rilievi entro 5 km dal mare.</summary>
    CollinaLitoranea = 3,
    /// <summary>Montagna interna: altitudine media > 600 m, non costiera.</summary>
    MontagnaInterna = 4,
    /// <summary>Montagna litoranea: altitudine media > 600 m, entro 5 km dal mare.</summary>
    MontagnaLitoranea = 5
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
