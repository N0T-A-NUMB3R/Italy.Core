using Italy.Core.Domain.Entità;

namespace Italy.Core.Domain.Interfacce;

public interface IRepositoryCAP
{
    IReadOnlyList<ZonaCAP> OttieniZone(string codiceBelfiore);
    IReadOnlyList<ZonaCAP> DaCAP(string cap);
    IReadOnlyList<ZonaCAP> CAPStorici(string codiceBelfiore);
    string? CAPPrincipale(string codiceBelfiore);
}
