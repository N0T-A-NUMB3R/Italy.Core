using Italy.Core.Domain.Entità;

namespace Italy.Core.Domain.Interfacce;

/// <summary>
/// Provider estendibile per il calcolo delle festività italiane.
/// Implementare questa interfaccia per aggiungere festività contrattuali personalizzate.
/// </summary>
public interface IProviderFestività
{
    IReadOnlyList<Festività> OttieniFestività(int anno, string? codiceBelfiore = null);
    bool IsFestivo(DateTime data, string? codiceBelfiore = null);
    int CalcolaGiorniLavorativi(DateTime dal, DateTime al, string? codiceBelfiore = null, bool sabatoLavorativo = false);
    DateTime CalcolaPasqua(int anno);
}

public interface IProviderGeografico
{
    CoordinateGeo? OttieniCoordinate(string codiceBelfiore);
    double? CalcolaDistanzaKm(string codiceBelfiore1, string codiceBelfiore2);
    IReadOnlyList<Comune> TrovaNelRaggio(CoordinateGeo punto, double raggioKm);
    string? OttieniGeoJSON(string codiceBelfiore);
}

public interface IProviderDemografico
{
    DatiDemografici? OttieniDati(string codiceBelfiore, int? anno = null);
    IReadOnlyList<DatiDemografici> OttieniSerieStorica(string codiceBelfiore);
}

public interface IProviderTelefonia
{
    PrefissoTelefonico? DaPrefisso(string prefisso);
    string? OttieniPrefisso(string codiceBelfiore);
    OperatoreMobile? IdentificaOperatore(string numero);
    RisultatoNumeroTelefonico Valida(string numero);
}
