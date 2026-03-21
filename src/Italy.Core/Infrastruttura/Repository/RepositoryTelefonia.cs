using Microsoft.Data.Sqlite;
using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;
using System.Text.Json;

namespace Italy.Core.Infrastruttura.Repository;

public sealed class RepositoryTelefonia : IProviderTelefonia
{
    private readonly DatabaseAtlante _db;

    public RepositoryTelefonia(DatabaseAtlante db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public PrefissoTelefonico? DaPrefisso(string prefisso) =>
        _db.Esegui(
            "SELECT * FROM prefissi_telefonici WHERE prefisso = @p AND is_attivo = 1 LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@p", prefisso),
            MappaPrefisso).FirstOrDefault();

    public string? OttieniPrefisso(string codiceBelfiore)
    {
        // Recupera il prefisso geografico provinciale del comune.
        // codici_istat è un JSON array — usa json_each per il match esatto.
        return _db.EseguiScalare<string?>(
            """
            SELECT pt.prefisso
            FROM prefissi_telefonici pt, json_each(pt.codici_istat) je
            WHERE je.value = (SELECT codice_istat FROM comuni WHERE codice_belfiore = @cb LIMIT 1)
              AND pt.tipo = 'Geografico'
              AND pt.is_attivo = 1
            LIMIT 1
            """,
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()));
    }

    public OperatoreMobile? IdentificaOperatore(string numero)
    {
        if (string.IsNullOrWhiteSpace(numero) || numero.Length < 3) return null;
        var prefisso3 = numero[..3];
        return _db.Esegui(
            "SELECT * FROM operatori_mobili WHERE prefisso = @p AND is_attivo = 1 LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@p", prefisso3),
            MappaOperatore).FirstOrDefault();
    }

    public RisultatoNumeroTelefonico Valida(string numero)
    {
        // Delegato al ServiziTelefonia per la logica di validazione
        throw new NotSupportedException("Usare ServiziTelefonia.Valida()");
    }

    private static PrefissoTelefonico MappaPrefisso(SqliteDataReader r)
    {
        var codiciJson = r.IsDBNull(3) ? null : r.GetString(3);
        return new PrefissoTelefonico
        {
            Prefisso = r.GetString(0),
            Tipo = (TipoPrefisso)Enum.Parse(typeof(TipoPrefisso), r.GetString(1)),
            AreaGeografica = r.IsDBNull(2) ? null : r.GetString(2),
            CodiciISTAT = codiciJson != null
                ? JsonSerializer.Deserialize<List<string>>(codiciJson) ?? new()
                : new List<string>(),
            IsAttivo = r.GetInt32(4) == 1
        };
    }

    private static OperatoreMobile MappaOperatore(SqliteDataReader r) => new()
    {
        Prefisso = r.GetString(0),
        NomeOperatore = r.GetString(1),
        Tecnologia = (TecnologiaRete)Enum.Parse(typeof(TecnologiaRete), r.GetString(2)),
        IsAttivo = r.GetInt32(3) == 1
    };
}
