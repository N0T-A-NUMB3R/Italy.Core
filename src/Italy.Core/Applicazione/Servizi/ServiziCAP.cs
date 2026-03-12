using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

public sealed class ServiziCAP
{
    private readonly IRepositoryCAP _repository;

    public ServiziCAP(IRepositoryCAP repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Restituisce tutte le zone CAP di un comune (incluse le multi-CAP per grandi città).
    /// Es: OttieniZone("F205") → [20100, 20121, 20122, ...]
    /// </summary>
    public IReadOnlyList<ZonaCAP> OttieniZone(string codiceBelfiore) =>
        _repository.OttieniZone(codiceBelfiore.ToUpperInvariant());

    /// <summary>
    /// Ricerca inversa: dato un CAP restituisce i comuni associati.
    /// Un CAP può coprire più comuni piccoli.
    /// </summary>
    public IReadOnlyList<ZonaCAP> DaCAP(string cap)
    {
        if (string.IsNullOrWhiteSpace(cap) || cap.Length != 5 || !cap.All(char.IsDigit))
            throw new ArgumentException("Il CAP deve essere composto da esattamente 5 cifre.", nameof(cap));
        return _repository.DaCAP(cap);
    }

    /// <summary>Restituisce lo storico dei CAP di un comune (per validare indirizzi legacy).</summary>
    public IReadOnlyList<ZonaCAP> CAPStorici(string codiceBelfiore) =>
        _repository.CAPStorici(codiceBelfiore.ToUpperInvariant());

    public string? CAPPrincipale(string codiceBelfiore) =>
        _repository.CAPPrincipale(codiceBelfiore.ToUpperInvariant());
}
