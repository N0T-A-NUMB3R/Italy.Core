using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Calcola festività nazionali e locali italiane.
/// Include festività mobili (Pasqua via algoritmo Meeus/Jones/Butcher).
/// Il santo patrono viene letto dal DB (1.000+ comuni) se disponibile,
/// altrimenti dal dizionario hardcoded dei 107 capoluoghi.
/// </summary>
public sealed class ServiziFestività : IProviderFestività
{
    private readonly IRepositoryComuni? _repositoryComuni;

    // Dizionario fallback (107 capoluoghi) usato quando il repository non è disponibile
    private static readonly Dictionary<string, (int Giorno, int Mese, string Nome)> _santiPatroniFallback = new()
    {
        { "F205", (7,  12, "Sant'Ambrogio") },            // Milano
        { "H501", (29,  6, "Santi Pietro e Paolo") },     // Roma
        { "F839", (19,  9, "San Gennaro") },              // Napoli
        { "L219", (24,  6, "San Giovanni Battista") },    // Torino
        { "A944", (4,  10, "San Petronio") },             // Bologna
        { "D969", (24,  6, "San Giovanni Battista") },    // Genova
        { "L781", (15,  7, "Santa Rosalia") },            // Palermo
        { "C351", (5,   2, "Sant'Agata") },               // Catania
        { "L736", (25,  4, "San Marco Evangelista") },    // Venezia
        { "D612", (24,  6, "San Giovanni Battista") },    // Firenze
        { "L424", (13,  6, "Sant'Antonio di Padova") },   // Padova
        { "B157", (6,  12, "San Nicola") },               // Bari
        { "L682", (21,  5, "San Zeno") },                 // Verona
        { "G224", (10, 10, "San Cetteo") },               // Pescara
        { "A182", (26,  8, "Sant'Alessandro") },          // Alessandria
        { "A326", (7,   9, "San Grato") },                // Aosta
        { "C117", (21,  5, "San Secondo") },              // Asti
        { "A794", (26,  8, "Sant'Alessandro") },          // Bergamo
        { "B563", (7,   8, "San Virgilio") },             // Bolzano
        { "B474", (15,  2, "Santi Faustino e Giovita") }, // Brescia
        { "H907", (1,   5, "Sant'Efisio") },              // Cagliari
        { "C632", (25, 11, "Santa Caterina d'Alessandria") }, // Campobasso
        { "C347", (8,   7, "Santa Irene") },              // Catanzaro
        { "C245", (11,  5, "San Giustino") },             // Chieti
        { "C933", (2,  12, "Sant'Abbondio") },            // Como
        { "D761", (25,  4, "San Marco") },                // Cosenza
        { "D284", (13, 11, "Sant'Omobono") },             // Cremona
        { "D860", (23,  4, "San Giorgio") },              // Crotone
        { "D286", (23,  4, "San Giorgio") },              // Ferrara
        { "E456", (22,  3, "San Guglielmo da Vercelli") },// Foggia
        { "D810", (1,   9, "Santa Corona") },             // Frosinone
        { "E098", (16,  7, "Nostra Signora del Carmelo") },// Gorizia
        { "E243", (10,  8, "San Lorenzo") },              // Grosseto
        { "E388", (10,  6, "San Massimo") },              // L'Aquila
        { "H823", (19,  9, "N.S. della Neve") },          // La Spezia
        { "F065", (26,  8, "Sant'Oronzo") },              // Lecce
        { "E507", (7,  12, "Sant'Ambrogio") },            // Lecco
        { "E648", (19,  4, "San Bassiano") },             // Lodi
        { "F704", (18,  3, "Sant'Anselmo") },             // Mantova
        { "F258", (2,   7, "Madonna della Bruna") },      // Matera
        { "F152", (3,   6, "Madonna della Lettera") },    // Messina
        { "F857", (31,  1, "San Geminiano") },            // Modena
        { "E801", (19,  5, "San Pietro Celestino") },     // Isernia
        { "E897", (13, 10, "N.S. della Solitudine") },    // Nuoro
        { "D960", (2,   5, "San Costantino") },           // Oristano
        { "G453", (29,  1, "San Costanzo") },             // Perugia
        { "G337", (4,   7, "Sant'Antonino") },            // Piacenza
        { "G388", (22,  1, "San Gaudenzio") },            // Novara
        { "G491", (16,  6, "San Ranieri") },              // Pisa
        { "G702", (30,  5, "San Gerardo") },              // Potenza
        { "G580", (29,  9, "San Giorgio") },              // Ragusa
        { "F356", (23,  7, "Sant'Apollinare") },          // Ravenna
        { "G943", (23,  4, "San Giorgio") },              // Reggio Calabria
        { "G888", (24, 11, "San Prospero") },             // Reggio Emilia
        { "H282", (16, 12, "Santa Barbara") },            // Rieti
        { "E289", (14, 10, "San Gaudenzo") },             // Rimini
        { "H199", (26, 11, "San Bellino") },              // Rovigo
        { "I407", (21,  9, "San Matteo") },               // Salerno
        { "I452", (6,  12, "San Nicola") },               // Sassari
        { "I726", (20,  5, "San Bernardino") },           // Siena
        { "H882", (13, 12, "Santa Lucia") },              // Siracusa
        { "I628", (29,  6, "Santi Gervasio e Protasio") },// Sondrio
        { "I531", (26,  6, "San Vigilio") },              // Trento
        { "B024", (14,  2, "San Valentino") },            // Terni
        { "I907", (19, 11, "San Berardo") },              // Teramo
        { "L049", (10,  5, "San Cataldo") },              // Taranto
        { "L109", (7,   8, "Sant'Alberto") },             // Trapani
        { "I791", (27,  4, "San Liberale") },             // Treviso
        { "L483", (25,  4, "San Marco") },                // Udine
        { "L833", (20,  3, "San Giuseppe") },             // Verbania
        { "L869", (1,   8, "Sant'Eusebio") },             // Vercelli
        { "L182", (4,   9, "Santa Rosa") },               // Viterbo
        { "A052", (4,  10, "San Francesco") },            // Assisi
        { "A049", (25,  2, "San Gerlando") },             // Agrigento
        { "A565", (5,   8, "Sant'Emidio") },              // Ascoli Piceno
        { "A271", (4,   5, "San Ciriaco") },              // Ancona
        { "A345", (7,   8, "San Donato") },               // Arezzo
        { "A662", (12,  2, "San Modestino") },            // Avellino
        { "A783", (24,  8, "San Bartolomeo") },           // Benevento
        { "B548", (9,  11, "San Teodoro d'Amasea") },     // Brindisi
        { "C352", (29,  9, "San Michele Arcangelo") },    // Caltanissetta
        { "B934", (26,  7, "Sant'Anna") },                // Caserta
        { "D643", (29, 11, "Beato Giacomo della Marca") },// Fermo
        { "E625", (6,   5, "San Marco") },                // Latina
        { "F023", (12,  7, "San Paolino") },              // Lucca
        { "F251", (31,  8, "San Giuliano") },             // Macerata
        { "E379", (26,  6, "Santi Giovanni e Paolo") },   // Imperia
        { "H700", (18,  7, "San Matteo") },               // Savona
        { "C574", (4,  10, "San Francesco") },            // Città di Castello
        { "A516", (28,  8, "Sant'Agostino") },            // Belluno
        { "F132", (28,  4, "San Marco") },                // Pordenone
        { "L120", (2,   6, "San Nicola Pellegrino") },    // Trani
    };

    /// <summary>Costruttore senza DI (usato da Atlante standalone).</summary>
    public ServiziFestività() { }

    /// <summary>Costruttore con DI (preferito — usa il DB per 1.000+ comuni).</summary>
    public ServiziFestività(IRepositoryComuni repositoryComuni)
    {
        _repositoryComuni = repositoryComuni;
    }

    public DateTime CalcolaPasqua(int anno)
    {
        // Algoritmo di Meeus/Jones/Butcher
        var a = anno % 19;
        var b = anno / 100;
        var c = anno % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mese = (h + l - 7 * m + 114) / 31;
        var giorno = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(anno, mese, giorno);
    }

    public IReadOnlyList<Festività> OttieniFestività(int anno, string? codiceBelfiore = null)
    {
        var lista = new List<Festività>();

        // ── Festività Nazionali Fisse ────────────────────────────────────────
        lista.Add(new Festività { Nome = "Capodanno", Data = new DateTime(anno, 1, 1), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Epifania", Data = new DateTime(anno, 1, 6), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Festa della Liberazione", Data = new DateTime(anno, 4, 25), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Festa del Lavoro", Data = new DateTime(anno, 5, 1), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Festa della Repubblica", Data = new DateTime(anno, 6, 2), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Ferragosto", Data = new DateTime(anno, 8, 15), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Ognissanti", Data = new DateTime(anno, 11, 1), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Immacolata Concezione", Data = new DateTime(anno, 12, 8), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Natale", Data = new DateTime(anno, 12, 25), Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Santo Stefano", Data = new DateTime(anno, 12, 26), Tipo = TipoFestività.Nazionale });

        // ── Festività Mobili ─────────────────────────────────────────────────
        var pasqua = CalcolaPasqua(anno);
        lista.Add(new Festività { Nome = "Pasqua", Data = pasqua, Tipo = TipoFestività.Nazionale });
        lista.Add(new Festività { Nome = "Lunedì dell'Angelo (Pasquetta)", Data = pasqua.AddDays(1), Tipo = TipoFestività.Nazionale });

        // ── Santo Patrono Locale ─────────────────────────────────────────────
        if (codiceBelfiore != null)
        {
            var cb = codiceBelfiore.ToUpperInvariant();
            var patrono = TrovaPatrono(cb);
            if (patrono.HasValue)
            {
                lista.Add(new Festività
                {
                    Nome = patrono.Value.Nome,
                    Data = new DateTime(anno, patrono.Value.Mese, patrono.Value.Giorno),
                    Tipo = TipoFestività.SantoPatrono,
                    CodiceBelfiore = codiceBelfiore,
                });
            }
        }

        return lista.OrderBy(f => f.Data).ToList();
    }

    public bool IsFestivo(DateTime data, string? codiceBelfiore = null)
    {
        if (data.DayOfWeek == DayOfWeek.Sunday) return true;
        var festività = OttieniFestività(data.Year, codiceBelfiore);
        return festività.Any(f => f.Data.Date == data.Date);
    }

    public int CalcolaGiorniLavorativi(DateTime dal, DateTime al, string? codiceBelfiore = null, bool sabatoLavorativo = false)
    {
        if (al < dal) throw new ArgumentException("La data finale deve essere successiva alla data iniziale.");

        var anni = Enumerable.Range(dal.Year, al.Year - dal.Year + 1);
        var tutteFestività = anni
            .SelectMany(a => OttieniFestività(a, codiceBelfiore))
            .Select(f => f.Data.Date)
            .ToHashSet();

        var contatore = 0;
        var corrente = dal.Date;
        while (corrente <= al.Date)
        {
            var isSabato = corrente.DayOfWeek == DayOfWeek.Saturday;
            var isDomenica = corrente.DayOfWeek == DayOfWeek.Sunday;
            if (!isDomenica
                && !(isSabato && !sabatoLavorativo)
                && !tutteFestività.Contains(corrente))
            {
                contatore++;
            }
            corrente = corrente.AddDays(1);
        }
        return contatore;
    }

    // ── Lookup patrono: DB → fallback dizionario ─────────────────────────────

    private (int Giorno, int Mese, string Nome)? TrovaPatrono(string codiceBelfiore)
    {
        // 1. Prova via repository DB (1.000+ comuni da santiebeati.it)
        if (_repositoryComuni != null)
        {
            var comune = _repositoryComuni.DaCodiceBelfiore(codiceBelfiore);
            if (comune?.SantoPatrono != null && comune.PatronoGiorno.HasValue && comune.PatronoMese.HasValue)
                return (comune.PatronoGiorno.Value, comune.PatronoMese.Value, comune.SantoPatrono);
        }

        // 2. Fallback dizionario hardcoded (107 capoluoghi)
        if (_santiPatroniFallback.TryGetValue(codiceBelfiore, out var p))
            return p;

        return null;
    }
}
