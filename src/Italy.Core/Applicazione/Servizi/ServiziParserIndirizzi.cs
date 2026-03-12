using System.Text.RegularExpressions;
using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Parser e normalizzatore di indirizzi italiani.
/// Zero dipendenze esterne: usa regex + lookup nel database Atlante esistente.
///
/// Gestisce:
/// - Formato libero:    "via roma 10, 17025 loano sv"
/// - Formato sporco:    "V.LE GRAMSCI, 5/A - SAVONA (17100)"
/// - Multi-riga:        "Corso Italia 23\n20122 Milano (MI)"
/// - Abbreviazioni:     "P.ZZA", "LGO", "VLLO", "STR.", ecc.
/// - Inversioni:        "10 Via Roma" → "Via Roma 10"
/// - CAP invertito:     comune prima del CAP
/// </summary>
public sealed class ServiziParserIndirizzi
{
    private readonly IRepositoryComuni _repositoryComuni;
    private readonly IRepositoryCAP _repositoryCAP;

    // ── Dizionario Toponimi ───────────────────────────────────────────────────

    private static readonly Dictionary<string, string> _toponimi = new(StringComparer.OrdinalIgnoreCase)
    {
        // Forme estese → forma canonica
        { "VIA", "VIA" }, { "V.", "VIA" }, { "V/", "VIA" },
        { "VIALE", "VIALE" }, { "V.LE", "VIALE" }, { "VLE", "VIALE" },
        { "PIAZZA", "PIAZZA" }, { "P.ZA", "PIAZZA" }, { "P.ZZA", "PIAZZA" },
          { "PZA", "PIAZZA" }, { "PZ", "PIAZZA" }, { "P.", "PIAZZA" },
        { "CORSO", "CORSO" }, { "C.SO", "CORSO" }, { "CSO", "CORSO" },
        { "VICOLO", "VICOLO" }, { "VCL", "VICOLO" }, { "VCO", "VICOLO" },
        { "LARGO", "LARGO" }, { "LGO", "LARGO" }, { "L.GO", "LARGO" },
        { "LUNGOMARE", "LUNGOMARE" }, { "LGM", "LUNGOMARE" },
        { "LUNGOTEVERE", "LUNGOTEVERE" },
        { "LUNGOPO", "LUNGOPO" },
        { "CONTRADA", "CONTRADA" }, { "C.DA", "CONTRADA" }, { "CTRA", "CONTRADA" },
        { "STRADA", "STRADA" }, { "STR.", "STRADA" }, { "STR", "STRADA" },
        { "FRAZIONE", "FRAZIONE" }, { "FRAZ.", "FRAZIONE" }, { "FRAZ", "FRAZIONE" },
        { "LOCALITA", "LOCALITA'" }, { "LOC.", "LOCALITA'" }, { "LOC", "LOCALITA'" },
        { "VICO", "VICO" },
        { "SALITA", "SALITA" },
        { "DISCESA", "DISCESA" },
        { "SCALINATA", "SCALINATA" },
        { "REGIONE", "REGIONE" }, { "REG.", "REGIONE" }, { "REG", "REGIONE" },
        { "BORGATA", "BORGATA" }, { "BARG.", "BORGATA" },
        { "BORGO", "BORGO" }, { "B.GO", "BORGO" },
        { "TRAVERSA", "TRAVERSA" }, { "TRAV.", "TRAVERSA" },
        { "PIAZZALE", "PIAZZALE" }, { "P.LE", "PIAZZALE" }, { "PLE", "PIAZZALE" },
        { "ROTONDA", "ROTONDA" },
        { "GALLERIA", "GALLERIA" }, { "GAL.", "GALLERIA" },
        { "PASSAGGIO", "PASSAGGIO" },
        { "SPALTO", "SPALTO" },
        { "CALATA", "CALATA" },
        { "SOTTOPORTICO", "SOTTOPORTICO" },
        { "VILLAGGIO", "VILLAGGIO" }, { "VILL.", "VILLAGGIO" },
        { "CORTE", "CORTE" }, { "C.TE", "CORTE" },
        { "PONTE", "PONTE" },
        { "MOLO", "MOLO" },
        { "RAMPA", "RAMPA" },
        { "CALLE", "CALLE" },          // Venezia
        { "CAMPIELLO", "CAMPIELLO" },  // Venezia
        { "FONDAMENTA", "FONDAMENTA" }, // Venezia
        { "RIO TERA'", "RIO TERA'" },   // Venezia
    };

    // ── Pattern Regex ─────────────────────────────────────────────────────────

    private static readonly Regex _regexCAP =
        new(@"\b(\d{5})\b", RegexOptions.Compiled);

    private static readonly Regex _regexProvincia =
        new(@"\(([A-Z]{2})\)|\b([A-Z]{2})\b$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _regexCivico =
        new(@"\b(\d{1,5}(?:[\/\-][A-Za-z0-9]{1,3})?(?:\s*(?:bis|ter|quater))?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _regexSeparatori =
        new(@"[\r\n\t]+|(?:\s*[,;/\-]\s*)|(?:\s{2,})", RegexOptions.Compiled);

    public ServiziParserIndirizzi(IRepositoryComuni repositoryComuni, IRepositoryCAP repositoryCAP)
    {
        _repositoryComuni = repositoryComuni ?? throw new ArgumentNullException(nameof(repositoryComuni));
        _repositoryCAP = repositoryCAP ?? throw new ArgumentNullException(nameof(repositoryCAP));
    }

    // ── API Pubblica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Analizza una stringa indirizzo libera e la scompone nei componenti canonici.
    ///
    /// Esempi:
    /// <code>
    /// Analizza("Via Roma 10, 17025 Loano (SV)")
    /// Analizza("P.ZZA GARIBALDI 5/A - SAVONA 17100")
    /// Analizza("Corso Vittorio Emanuele, 23\n20122 Milano")
    /// </code>
    /// </summary>
    public IndirizzoItaliano Analizza(string indirizzoGrezzo)
    {
        if (string.IsNullOrWhiteSpace(indirizzoGrezzo))
            return new IndirizzoItaliano
            {
                Anomalie = ["Indirizzo vuoto."],
                ScoreQualità = 0.0
            };

        var anomalie = new List<string>();
        var testo = NormalizzaTesto(indirizzoGrezzo);

        // ── Estrai CAP ────────────────────────────────────────────────────────
        var cap = EstraiCAP(testo, out testo);

        // ── Estrai Provincia ──────────────────────────────────────────────────
        var sigla = EstraiProvincia(testo, out testo);

        // ── Estrai Toponimo e Via ─────────────────────────────────────────────
        var (toponimo, nomeVia, testoRimanente) = EstraiVia(testo);

        // ── Estrai Civico ─────────────────────────────────────────────────────
        var civico = EstraiCivico(testoRimanente, out var testoDopoVia);

        // ── Estrai Comune dal testo rimanente ────────────────────────────────
        var nomeComune = EstraiComune(testoDopoVia.Trim());

        // ── Risoluzione DB ────────────────────────────────────────────────────
        Comune? comuneRisolto = null;
        string? nomeProvincia = null;

        if (cap != null)
        {
            // Prima prova con CAP
            var zoneCap = _repositoryCAP.DaCAP(cap);
            if (zoneCap.Count == 1)
            {
                comuneRisolto = _repositoryComuni.DaCodiceBelfiore(zoneCap[0].CodiciBelfiore[0]);
            }
            else if (zoneCap.Count > 1 && nomeComune != null)
            {
                // CAP ambiguo: disambigua con il nome comune
                comuneRisolto = zoneCap
                    .SelectMany(z => z.CodiciBelfiore.Select(b => _repositoryComuni.DaCodiceBelfiore(b)))
                    .Where(c => c != null)
                    .FirstOrDefault(c =>
                        c!.DenominazioneUfficiale.StartsWith(nomeComune, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (comuneRisolto == null && nomeComune != null)
        {
            // Fuzzy search sul nome comune
            var risultati = _repositoryComuni.Cerca(nomeComune, 3);
            if (risultati.Count == 1)
                comuneRisolto = risultati[0];
            else if (risultati.Count > 1 && sigla != null)
                comuneRisolto = risultati.FirstOrDefault(c =>
                    c.SiglaProvincia.Equals(sigla, StringComparison.OrdinalIgnoreCase));
        }

        if (comuneRisolto != null)
        {
            nomeProvincia = comuneRisolto.NomeProvincia;
            sigla ??= comuneRisolto.SiglaProvincia;

            // Valida coerenza CAP-Comune
            if (cap != null)
            {
                var capComune = _repositoryCAP.OttieniZone(comuneRisolto.CodiceBelfiore);
                if (capComune.Count > 0 && !capComune.Any(z => z.CAP == cap))
                    anomalie.Add($"Il CAP '{cap}' non corrisponde al comune '{comuneRisolto.DenominazioneUfficiale}'.");
            }
        }
        else if (nomeComune != null)
        {
            anomalie.Add($"Comune '{nomeComune}' non risolto nel database.");
        }

        // ── Calcolo Score ─────────────────────────────────────────────────────
        var score = CalcolaScore(toponimo, nomeVia, civico, cap, comuneRisolto, sigla, anomalie);

        return new IndirizzoItaliano
        {
            Toponimo = toponimo,
            NomeVia = nomeVia,
            Civico = civico,
            CAP = cap,
            NomeComune = comuneRisolto?.DenominazioneUfficiale ?? nomeComune,
            SiglaProvincia = sigla?.ToUpperInvariant(),
            NomeProvincia = nomeProvincia,
            ComuneRisolto = comuneRisolto,
            IsCompleto = cap != null && comuneRisolto != null && nomeVia != null,
            ScoreQualità = score,
            Anomalie = anomalie
        };
    }

    /// <summary>
    /// Analizza un indirizzo e restituisce il formato ANPR normalizzato.
    /// </summary>
    public string NormalizzaPerANPR(string indirizzoGrezzo)
    {
        var risultato = Analizza(indirizzoGrezzo);
        return risultato.FormatoANPR;
    }

    /// <summary>
    /// Estrae il comune di nascita da una stringa libera (es. "nato a Loano, SV").
    /// </summary>
    public Comune? EstraiComuneNascita(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;

        // Rimuovi prefissi comuni
        var pulito = Regex.Replace(testo.Trim(),
            @"^(?:nato\s+a|nata\s+a|comune\s+di|in\s+)\s*", "",
            RegexOptions.IgnoreCase).Trim();

        // Estrai sigla provincia se presente
        var matchProv = _regexProvincia.Match(pulito);
        string? sigla = null;
        if (matchProv.Success)
        {
            sigla = (matchProv.Groups[1].Value + matchProv.Groups[2].Value).ToUpperInvariant();
            pulito = pulito[..matchProv.Index].Trim().TrimEnd(',', '(', ' ');
        }

        var risultati = _repositoryComuni.Cerca(pulito, 5);
        if (sigla != null)
            return risultati.FirstOrDefault(c =>
                c.SiglaProvincia.Equals(sigla, StringComparison.OrdinalIgnoreCase));

        return risultati.FirstOrDefault();
    }

    // ── Algoritmi Privati ─────────────────────────────────────────────────────

    private static string NormalizzaTesto(string s)
    {
        // Unifica separatori multipli e normalizza spazi
        s = _regexSeparatori.Replace(s, " ").Trim();
        // Normalizza apici e caratteri speciali
        s = s.Replace("'", "'").Replace("`", "'");
        return s;
    }

    private static string? EstraiCAP(string testo, out string testoSenzaCAP)
    {
        var match = _regexCAP.Match(testo);
        if (match.Success)
        {
            testoSenzaCAP = (testo[..match.Index] + testo[(match.Index + match.Length)..]).Trim();
            return match.Groups[1].Value;
        }
        testoSenzaCAP = testo;
        return null;
    }

    private static string? EstraiProvincia(string testo, out string testoSenzaProvincia)
    {
        // Cerca (XX) oppure sigla a 2 lettere in fondo
        var match = Regex.Match(testo, @"\(([A-Za-z]{2})\)|(?<=\s)([A-Za-z]{2})$",
            RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        if (match.Success)
        {
            var sigla = (match.Groups[1].Value + match.Groups[2].Value).ToUpperInvariant();
            testoSenzaProvincia = (testo[..match.Index] + testo[(match.Index + match.Length)..]).Trim();
            return sigla;
        }
        testoSenzaProvincia = testo;
        return null;
    }

    private static (string? Toponimo, string? NomeVia, string Rimanente) EstraiVia(string testo)
    {
        var testoUpper = testo.ToUpperInvariant();

        foreach (var kvToponimo in _toponimi.OrderByDescending(k => k.Key.Length))
        {
            var abbr = kvToponimo.Key;
            var canonico = kvToponimo.Value;
            var pattern = $@"(?<!\w){Regex.Escape(abbr.ToUpperInvariant())}\.?\s+";
            var match = Regex.Match(testoUpper, pattern);
            if (!match.Success) continue;

            var dopoToponimo = testo[(match.Index + match.Length)..];

            // Cerca il civico per delimitare il nome via
            var matchCivico = _regexCivico.Match(dopoToponimo);
            string nomeVia, rimanente;

            if (matchCivico.Success)
            {
                nomeVia = dopoToponimo[..matchCivico.Index].Trim().TrimEnd(',', ' ');
                rimanente = dopoToponimo[matchCivico.Index..];
            }
            else
            {
                // Prende tutto fino al primo separatore forte
                var sepMatch = Regex.Match(dopoToponimo, @"[,;]|\d{5}");
                nomeVia = sepMatch.Success
                    ? dopoToponimo[..sepMatch.Index].Trim()
                    : dopoToponimo.Trim();
                rimanente = sepMatch.Success ? dopoToponimo[sepMatch.Index..] : "";
            }

            // Capitalizza correttamente
            nomeVia = CapitalizzaVia(nomeVia);

            var inizioMatch = testo[..match.Index];
            return (canonico, nomeVia, inizioMatch + " " + rimanente);
        }

        return (null, null, testo);
    }

    private static string? EstraiCivico(string testo, out string testoSenzaCivico)
    {
        var match = _regexCivico.Match(testo);
        if (match.Success)
        {
            testoSenzaCivico = (testo[..match.Index] + testo[(match.Index + match.Length)..]).Trim();
            return match.Groups[1].Value.Trim();
        }
        testoSenzaCivico = testo;
        return null;
    }

    private static string? EstraiComune(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        // Prendi la prima parte significativa (prima di eventuali separatori)
        var pulito = testo.Split(',', ';', '-')[0].Trim();
        return string.IsNullOrWhiteSpace(pulito) ? null : pulito;
    }

    private static string CapitalizzaVia(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        // Articoli e preposizioni in minuscolo
        var articoli = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "di", "de", "del", "della", "dei", "degli", "delle", "da", "dal", "dalla",
              "in", "a", "al", "alla", "e", "il", "lo", "la", "i", "gli", "le" };

        var parole = s.Split(' ');
        return string.Join(" ", parole.Select((p, i) =>
            i == 0 || !articoli.Contains(p)
                ? char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()
                : p.ToLowerInvariant()));
    }

    private static double CalcolaScore(
        string? toponimo, string? nomeVia, string? civico,
        string? cap, Comune? comune, string? sigla,
        IReadOnlyList<string> anomalie)
    {
        var score = 0.0;
        if (nomeVia != null) score += 0.25;
        if (civico != null) score += 0.10;
        if (cap != null) score += 0.25;
        if (comune != null) score += 0.30;
        if (sigla != null) score += 0.10;
        score -= anomalie.Count * 0.15;
        return Math.Max(0.0, Math.Min(1.0, score));
    }
}
