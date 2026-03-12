using Microsoft.Data.Sqlite;
using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Infrastruttura.Repository;

public sealed class RepositoryCAP : IRepositoryCAP
{
    private readonly DatabaseAtlante _db;

    public RepositoryCAP(DatabaseAtlante db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public IReadOnlyList<ZonaCAP> OttieniZone(string codiceBelfiore) =>
        _db.Esegui(
            """
            SELECT cap, codice_belfiore, codice_istat, descrizione_zona,
                   data_attivazione, data_disattivazione
            FROM cap
            WHERE codice_belfiore = @cb
              AND (data_disattivazione IS NULL OR data_disattivazione > date('now'))
            ORDER BY cap
            """,
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()),
            MappaZona);

    public IReadOnlyList<ZonaCAP> DaCAP(string cap) =>
        _db.Esegui(
            """
            SELECT cap, codice_belfiore, codice_istat, descrizione_zona,
                   data_attivazione, data_disattivazione
            FROM cap
            WHERE cap = @cap
              AND (data_disattivazione IS NULL OR data_disattivazione > date('now'))
            ORDER BY codice_belfiore
            """,
            cmd => cmd.Parameters.AddWithValue("@cap", cap),
            MappaZona);

    public IReadOnlyList<ZonaCAP> CAPStorici(string codiceBelfiore) =>
        _db.Esegui(
            """
            SELECT cap, codice_belfiore, codice_istat, descrizione_zona,
                   data_attivazione, data_disattivazione
            FROM cap
            WHERE codice_belfiore = @cb
            ORDER BY data_attivazione ASC
            """,
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()),
            MappaZona);

    public string? CAPPrincipale(string codiceBelfiore) =>
        _db.EseguiScalare<string?>(
            "SELECT cap_principale FROM comuni WHERE codice_belfiore = @cb",
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()));

    private static ZonaCAP MappaZona(SqliteDataReader r) => new()
    {
        CAP = r.GetString(0),
        CodiciBelfiore = new[] { r.GetString(1) },
        CodiciISTAT = new[] { r.GetString(2) },
        DescrizioneZona = r.IsDBNull(3) ? null : r.GetString(3),
        DataAttivazione = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
        DataDisattivazione = r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)),
    };
}
