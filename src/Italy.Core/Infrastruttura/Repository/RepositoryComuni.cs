using Microsoft.Data.Sqlite;
using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;
using System.Text.Json;

namespace Italy.Core.Infrastruttura.Repository;

/// <summary>
/// Implementazione SQLite del repository comuni.
/// Usa FTS5 per la ricerca fuzzy e indici B-Tree per le query dirette.
/// </summary>
public sealed class RepositoryComuni : IRepositoryComuni
{
    private readonly DatabaseAtlante _db;

    public RepositoryComuni(DatabaseAtlante db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // ── Lookup Diretto ───────────────────────────────────────────────────────

    public Comune? DaCodiceBelfiore(string codiceBelfiore) =>
        _db.Esegui(
            "SELECT * FROM comuni WHERE codice_belfiore = @cb LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()),
            MappaComune).FirstOrDefault();

    public Comune? DaCodiceISTAT(string codiceISTAT) =>
        _db.Esegui(
            "SELECT * FROM comuni WHERE codice_istat = @ci LIMIT 1",
            cmd => cmd.Parameters.AddWithValue("@ci", codiceISTAT),
            MappaComune).FirstOrDefault();

    // ── Ricerca Fuzzy ────────────────────────────────────────────────────────

    /// <summary>
    /// Cerca comuni con doppia strategia:
    /// 1. FTS5 per risultati rapidi
    /// 2. Levenshtein per fuzzy matching residuo
    /// </summary>
    public IReadOnlyList<Comune> Cerca(string testo, int massimo = 10, double sogliaLevenshtein = 0.7)
    {
        if (string.IsNullOrWhiteSpace(testo)) return Array.Empty<Comune>();

        var testoPulito = testo.Trim();

        // Prima prova ricerca FTS5
        var risultatiFTS = _db.Esegui(
            """
            SELECT c.* FROM comuni c
            JOIN comuni_fts f ON c.rowid = f.rowid
            WHERE comuni_fts MATCH @q
            ORDER BY rank
            LIMIT @max
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@q", testoPulito + "*");
                cmd.Parameters.AddWithValue("@max", massimo);
            },
            MappaComune);

        if (risultatiFTS.Count >= massimo)
            return risultatiFTS;

        // Fallback: LIKE su denominazione + filtro Levenshtein in memoria
        var candidati = _db.Esegui(
            """
            SELECT * FROM comuni
            WHERE denominazione LIKE @q
               OR denominazione_alt LIKE @q
            LIMIT 200
            """,
            cmd => cmd.Parameters.AddWithValue("@q", $"%{testoPulito}%"),
            MappaComune);

        var conPunteggio = candidati
            .Select(c => new
            {
                Comune = c,
                Punteggio = CalcolaSimilarità(testoPulito.ToLowerInvariant(),
                                              c.DenominazioneUfficiale.ToLowerInvariant())
            })
            .Where(x => x.Punteggio >= sogliaLevenshtein)
            .OrderByDescending(x => x.Punteggio)
            .Take(massimo)
            .Select(x => x.Comune)
            .ToList();

        // Unisci i due risultati senza duplicati
        var tuttiCodici = risultatiFTS.Select(c => c.CodiceBelfiore).ToHashSet();
        var uniti = risultatiFTS.ToList();
        uniti.AddRange(conPunteggio.Where(c => !tuttiCodici.Contains(c.CodiceBelfiore)));
        return uniti.Take(massimo).ToList();
    }

    // ── Gerarchia ────────────────────────────────────────────────────────────

    public IReadOnlyList<Comune> TuttiAttivi() =>
        _db.Esegui("SELECT * FROM comuni WHERE is_attivo = 1 ORDER BY denominazione",
            null, MappaComune);

    public IReadOnlyList<Comune> TuttiInclusiSoppressi() =>
        _db.Esegui("SELECT * FROM comuni ORDER BY denominazione", null, MappaComune);

    public IReadOnlyList<Comune> DaProvincia(string siglaProvincia) =>
        _db.Esegui(
            "SELECT * FROM comuni WHERE sigla_provincia = @sp AND is_attivo = 1 ORDER BY denominazione",
            cmd => cmd.Parameters.AddWithValue("@sp", siglaProvincia.ToUpperInvariant()),
            MappaComune);

    public IReadOnlyList<Comune> DaRegione(string nomeRegione) =>
        _db.Esegui(
            "SELECT * FROM comuni WHERE nome_regione = @nr AND is_attivo = 1 ORDER BY denominazione",
            cmd => cmd.Parameters.AddWithValue("@nr", nomeRegione),
            MappaComune);

    public IReadOnlyList<Comune> DaRipartizione(RipartizioneGeografica ripartizione) =>
        _db.Esegui(
            "SELECT * FROM comuni WHERE ripartizione = @rip AND is_attivo = 1 ORDER BY denominazione",
            cmd => cmd.Parameters.AddWithValue("@rip", (int)ripartizione),
            MappaComune);

    // ── Risoluzione Storica ─────────────────────────────────────────────────

    public Comune? OttieniSuccessore(string codiceBelfiore)
    {
        var comune = DaCodiceBelfiore(codiceBelfiore);
        if (comune == null || comune.IsAttivo) return comune;
        if (comune.CodiceSuccessore == null) return null;
        return DaCodiceBelfiore(comune.CodiceSuccessore);
    }

    public Comune? OttieniDatiStorici(string codiceBelfiore, DateTime data)
    {
        // Cerca tra le variazioni storiche la denominazione valida a quella data
        var variazioni = OttieniStorico(codiceBelfiore)
            .Where(v => v.DataVariazione <= data)
            .OrderByDescending(v => v.DataVariazione)
            .ToList();

        var comune = DaCodiceBelfiore(codiceBelfiore);
        if (comune == null) return null;

        var ultimaVariazione = variazioni.FirstOrDefault();
        if (ultimaVariazione?.DenominazionePrecedente != null)
        {
            // Restituisce un "snapshot" con la denominazione storica
            return new Comune
            {
                CodiceBelfiore = comune.CodiceBelfiore,
                CodiceISTAT = comune.CodiceISTAT,
                DenominazioneUfficiale = ultimaVariazione.DenominazionePrecedente,
                SiglaProvincia = ultimaVariazione.ProvinciaPrecedente ?? comune.SiglaProvincia,
                NomeProvincia = comune.NomeProvincia,
                NomeRegione = comune.NomeRegione,
                Ripartizione = comune.Ripartizione,
                IsAttivo = true,
                DataIstituzione = comune.DataIstituzione
            };
        }
        return comune;
    }

    public IReadOnlyList<VariazioneStorica> OttieniStorico(string codiceBelfiore) =>
        _db.Esegui(
            """
            SELECT * FROM variazioni_storiche
            WHERE codice_belfiore = @cb
            ORDER BY data_variazione ASC
            """,
            cmd => cmd.Parameters.AddWithValue("@cb", codiceBelfiore.ToUpperInvariant()),
            MappaVariazione);

    // ── Paginazione ──────────────────────────────────────────────────────────

    public IReadOnlyList<Comune> OttieniPagina(int pagina, int dimensione = 100) =>
        _db.Esegui(
            "SELECT * FROM comuni WHERE is_attivo = 1 ORDER BY denominazione LIMIT @dim OFFSET @off",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@dim", dimensione);
                cmd.Parameters.AddWithValue("@off", pagina * dimensione);
            },
            MappaComune);

    public int ContaTotale() =>
        _db.EseguiScalare<int>("SELECT COUNT(*) FROM comuni WHERE is_attivo = 1");

    // ── Mappatura ────────────────────────────────────────────────────────────

    private static Comune MappaComune(SqliteDataReader r) => new()
    {
        CodiceBelfiore = r.GetString(r.GetOrdinal("codice_belfiore")),
        CodiceISTAT = r.GetString(r.GetOrdinal("codice_istat")),
        DenominazioneUfficiale = r.GetString(r.GetOrdinal("denominazione")),
        DenominazioneAlternativa = LeggioStringaNullabile(r, "denominazione_alt"),
        SiglaProvincia = r.GetString(r.GetOrdinal("sigla_provincia")),
        NomeProvincia = r.GetString(r.GetOrdinal("nome_provincia")),
        CodiceProvinciaISTAT = r.GetString(r.GetOrdinal("codice_provincia")),
        NomeRegione = r.GetString(r.GetOrdinal("nome_regione")),
        CodiceRegioneISTAT = r.GetString(r.GetOrdinal("codice_regione")),
        Ripartizione = (RipartizioneGeografica)r.GetInt32(r.GetOrdinal("ripartizione")),
        IsCapoluogoProvincia = r.GetInt32(r.GetOrdinal("is_capoluogo")) == 1,
        IsCittàMetropolitana = r.GetInt32(r.GetOrdinal("is_citta_metro")) == 1,
        IsComuneMontano = r.GetInt32(r.GetOrdinal("is_montano")) == 1,
        IsAttivo = r.GetInt32(r.GetOrdinal("is_attivo")) == 1,
        DataIstituzione = ParseData(LeggioStringaNullabile(r, "data_istituzione")) ?? DateTime.MinValue,
        DataSoppressione = ParseData(LeggioStringaNullabile(r, "data_soppressione")),
        CodiceSuccessore = LeggioStringaNullabile(r, "codice_successore"),
        CAPPrincipale = LeggioStringaNullabile(r, "cap_principale"),
        Latitudine = LeggioDoubleNullabile(r, "latitudine"),
        Longitudine = LeggioDoubleNullabile(r, "longitudine"),
        AltitudineMetri = LeggioDoubleNullabile(r, "altitudine"),
        SuperficieKmq = LeggioDoubleNullabile(r, "superficie_kmq"),
        Popolazione = LeggioIntNullabile(r, "popolazione"),
        AnnoRilevazionePopolazione = LeggioIntNullabile(r, "anno_rilevazione"),
        CodiceNUTS3 = LeggioStringaNullabile(r, "nuts3"),
        CodiceNUTS2 = LeggioStringaNullabile(r, "nuts2"),
        CodiceNUTS1 = LeggioStringaNullabile(r, "nuts1"),
        ZonaAltimetrica = LeggioZonaAltimetrica(r, "zona_altimetrica"),
        ZonaSismica = LeggioEnumInt<ZonaSismica>(r, "zona_sismica"),
        ZonaClimatica = LeggioEnumStringa<ZonaClimatica>(r, "zona_climatica"),
        GradiGiorno = LeggioIntNullabile(r, "gradi_giorno"),
        ClasseAreeInterne = LeggioClasseAreeInterne(r, "classe_aree_interne"),
        SantoPatrono = LeggioStringaNullabile(r, "santo_patrono"),
        PatronoGiorno = LeggioIntNullabile(r, "patrono_giorno"),
        PatronoMese = LeggioIntNullabile(r, "patrono_mese"),
        PEC = LeggioStringaNullabile(r, "pec"),
        PercRaccoltaDifferenziata = LeggioDoubleNullabile(r, "perc_raccolta_diff"),
        RifiutiKgAbitante = LeggioDoubleNullabile(r, "rifiuti_kg_ab"),
        RifiutiTotT = LeggioDoubleNullabile(r, "rifiuti_tot_t"),
        RifiutiIndT = LeggioDoubleNullabile(r, "rifiuti_ind_t"),
        RifiutiRdT = LeggioDoubleNullabile(r, "rifiuti_rd_t"),
        RdUmidoT = LeggioDoubleNullabile(r, "rd_umido_t"),
        RdCartaT = LeggioDoubleNullabile(r, "rd_carta_t"),
        RdVetroT = LeggioDoubleNullabile(r, "rd_vetro_t"),
        RdPlasticaT = LeggioDoubleNullabile(r, "rd_plastica_t"),
        RdLegnoT = LeggioDoubleNullabile(r, "rd_legno_t"),
        RdMetalloT = LeggioDoubleNullabile(r, "rd_metallo_t"),
        RdVerdeT = LeggioDoubleNullabile(r, "rd_verde_t"),
        RdRaeeT = LeggioDoubleNullabile(r, "rd_raee_t"),
        AnnoRilevazionRifiuti = LeggioIntNullabile(r, "anno_rifiuti"),
    };

    private static ZonaAltimetrica? LeggioZonaAltimetrica(SqliteDataReader r, string colonna)
    {
        // La colonna potrebbe non esistere in DB generati prima di questa versione
        try
        {
            var ordine = r.GetOrdinal(colonna);
            if (r.IsDBNull(ordine)) return null;
            var val = r.GetInt32(ordine);
            return Enum.IsDefined(typeof(ZonaAltimetrica), val) ? (ZonaAltimetrica)val : null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null; // colonna assente nel DB corrente
        }
    }

    private static T? LeggioEnumInt<T>(SqliteDataReader r, string colonna) where T : struct, Enum
    {
        try
        {
            var ordine = r.GetOrdinal(colonna);
            if (r.IsDBNull(ordine)) return null;
            var val = r.GetInt32(ordine);
            return Enum.IsDefined(typeof(T), val) ? (T)(object)val : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static T? LeggioEnumStringa<T>(SqliteDataReader r, string colonna) where T : struct, Enum
    {
        try
        {
            var ordine = r.GetOrdinal(colonna);
            if (r.IsDBNull(ordine)) return null;
            var s = r.GetString(ordine).Trim();
            return Enum.TryParse<T>(s, ignoreCase: true, out var result) ? result : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static ClasseAreeInterne? LeggioClasseAreeInterne(SqliteDataReader r, string colonna)
    {
        try
        {
            var ordine = r.GetOrdinal(colonna);
            if (r.IsDBNull(ordine)) return null;
            var s = r.GetString(ordine).Trim().ToUpperInvariant();
            return s switch
            {
                "A" or "CENTRO" => ClasseAreeInterne.Centro,
                "B" or "CINTURA" => ClasseAreeInterne.Cintura,
                "C" or "INTERMEDIO" => ClasseAreeInterne.Intermedio,
                "D" or "PERIFERICO" => ClasseAreeInterne.Periferico,
                "E" or "ULTRAPERIFERICO" => ClasseAreeInterne.Ultraperiferico,
                _ => null
            };
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static VariazioneStorica MappaVariazione(SqliteDataReader r)
    {
        var codiciOrigineJson = LeggioStringaNullabile(r, "codici_origine");
        var codiciDestJson = LeggioStringaNullabile(r, "codici_destinazione");

        return new VariazioneStorica
        {
            Id = r.GetInt32(r.GetOrdinal("id")),
            CodiceBelfiore = r.GetString(r.GetOrdinal("codice_belfiore")),
            Tipo = (TipoVariazione)Enum.Parse(typeof(TipoVariazione), r.GetString(r.GetOrdinal("tipo_variazione"))),
            DataVariazione = ParseData(r.GetString(r.GetOrdinal("data_variazione"))) ?? DateTime.MinValue,
            DenominazionePrecedente = LeggioStringaNullabile(r, "denominazione_prec"),
            DenominazioneSuccessiva = LeggioStringaNullabile(r, "denominazione_succ"),
            ProvinciaPrecedente = LeggioStringaNullabile(r, "provincia_prec"),
            ProvinciaSuccessiva = LeggioStringaNullabile(r, "provincia_succ"),
            CodiciOrigine = codiciOrigineJson != null
                ? JsonSerializer.Deserialize<List<string>>(codiciOrigineJson) ?? new()
                : new List<string>(),
            CodiciDestinazione = codiciDestJson != null
                ? JsonSerializer.Deserialize<List<string>>(codiciDestJson) ?? new()
                : new List<string>(),
            RiferimentoNormativo = LeggioStringaNullabile(r, "riferimento_norm"),
            Note = LeggioStringaNullabile(r, "note"),
        };
    }

    // ── Utilità ──────────────────────────────────────────────────────────────

    private static string? LeggioStringaNullabile(SqliteDataReader r, string colonna)
    {
        try { var o = r.GetOrdinal(colonna); return r.IsDBNull(o) ? null : r.GetString(o); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static double? LeggioDoubleNullabile(SqliteDataReader r, string colonna)
    {
        try { var o = r.GetOrdinal(colonna); return r.IsDBNull(o) ? null : r.GetDouble(o); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static int? LeggioIntNullabile(SqliteDataReader r, string colonna)
    {
        try { var o = r.GetOrdinal(colonna); return r.IsDBNull(o) ? null : r.GetInt32(o); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static DateTime? ParseData(string? s) =>
        DateTime.TryParse(s, out var d) ? d : null;

    /// <summary>Calcola la similarità tra due stringhe (normalizzata 0-1).</summary>
    private static double CalcolaSimilarità(string a, string b)
    {
        if (a == b) return 1.0;
        var distanza = Levenshtein(a, b);
        var massima = Math.Max(a.Length, b.Length);
        return massima == 0 ? 1.0 : 1.0 - (double)distanza / massima;
    }

    private static int Levenshtein(string a, string b)
    {
#if NET8_0_OR_GREATER
        var dp = new int[a.Length + 1, b.Length + 1];
#else
        var dp = new int[a.Length + 1, b.Length + 1];
#endif
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            dp[i, j] = a[i - 1] == b[j - 1]
                ? dp[i - 1, j - 1]
                : 1 + Math.Min(dp[i - 1, j], Math.Min(dp[i, j - 1], dp[i - 1, j - 1]));
        }
        return dp[a.Length, b.Length];
    }
}
