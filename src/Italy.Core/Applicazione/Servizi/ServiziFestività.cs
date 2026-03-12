using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Calcola festività nazionali e locali italiane.
/// Include festività mobili (Pasqua via algoritmo Meeus/Jones/Butcher).
/// </summary>
public sealed class ServiziFestività : IProviderFestività
{
    // Santo Patrono per comune (CodiceBelfiore → (giorno, mese, nome))
    private static readonly Dictionary<string, (int Giorno, int Mese, string Nome)> _santiPatroni = new()
    {
        { "F205", (7, 12, "Sant'Ambrogio") },       // Milano
        { "H501", (29, 6, "Santi Pietro e Paolo") }, // Roma
        { "F839", (19, 9, "San Gennaro") },          // Napoli
        { "L219", (24, 6, "San Giovanni Battista") }, // Torino
        { "A944", (4, 10, "San Petronio") },          // Bologna
        { "D969", (24, 6, "San Giovanni Battista") }, // Genova
        { "L781", (2, 7, "Santi Pietro e Paolo") },  // Palermo
        { "C351", (5, 2, "Sant'Agata") },             // Catania
        { "L736", (25, 4, "San Marco") },             // Venezia
        { "D612", (24, 6, "San Giovanni Battista") }, // Firenze
        { "L424", (13, 6, "Sant'Antonio") },           // Padova
        { "G482", (25, 4, "San Marco") },             // Venezia laguna
    };

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
        if (codiceBelfiore != null && _santiPatroni.TryGetValue(codiceBelfiore.ToUpperInvariant(), out var patrono))
        {
            lista.Add(new Festività
            {
                Nome = patrono.Nome,
                Data = new DateTime(anno, patrono.Mese, patrono.Giorno),
                Tipo = TipoFestività.SantoPatrono,
                CodiceBelfiore = codiceBelfiore
            });
        }

        return lista.OrderBy(f => f.Data).ToList();
    }

    public bool IsFestivo(DateTime data, string? codiceBelfiore = null)
    {
        if (data.DayOfWeek == DayOfWeek.Sunday) return true;
        var festività = OttieniFestività(data.Year, codiceBelfiore);
        return festività.Any(f => f.Data.Date == data.Date);
    }

    public int CalcolaGiorniLavorativi(DateTime dal, DateTime al, string? codiceBelfiore = null)
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
            if (corrente.DayOfWeek != DayOfWeek.Saturday
                && corrente.DayOfWeek != DayOfWeek.Sunday
                && !tutteFestività.Contains(corrente))
            {
                contatore++;
            }
            corrente = corrente.AddDays(1);
        }
        return contatore;
    }
}
