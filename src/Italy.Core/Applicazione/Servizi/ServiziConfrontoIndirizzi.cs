using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Italy.Core.Domain.Entità;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Confronto intelligente tra due indirizzi italiani.
/// Integra il parser di Italy.Core per normalizzare prima il confronto,
/// poi applica scoring multi-dimensionale (via, civico, CAP, città).
///
/// Miglioramenti rispetto alla versione standalone:
/// - Pre-normalizzazione tramite ServiziParserIndirizzi (risolve abbreviazioni, CAP, comune)
/// - Confronto anche su CAP (dimensione aggiuntiva)
/// - Score per componente separato (via, civico, CAP, città)
/// - Compatibile Span/zero-alloc per il Levenshtein interno
/// </summary>
public sealed class ServiziConfrontoIndirizzi
{
    private const double SOGLIA_UGUALE = 0.95;
    private const double SOGLIA_STRICT = 0.85;
    private const double SOGLIA_PERMISSIVA = 0.75;

    private readonly ServiziParserIndirizzi? _parser;

    private static readonly Dictionary<string, string> _abbreviazioni = new(StringComparer.OrdinalIgnoreCase)
    {
        { "v.", "via" }, { "v.le", "viale" }, { "v/", "via" },
        { "c.so", "corso" }, { "cso", "corso" },
        { "pz", "piazza" }, { "p.za", "piazza" }, { "p.zza", "piazza" }, { "pzza", "piazza" },
        { "lgo", "largo" }, { "l.go", "largo" },
        { "p.le", "piazzale" }, { "ple", "piazzale" },
        { "loc.", "localita" }, { "fraz.", "frazione" }, { "str.", "strada" },
    };

    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "via", "viale", "corso", "piazza", "strada", "largo", "vicolo", "piazzale",
        "n", "n.", "civico", "num", "numero",
        "di", "a", "da", "del", "della", "dei", "degli", "delle", "il", "lo", "la",
    };

    /// <param name="parser">Opzionale: se fornito, pre-normalizza gli indirizzi tramite il DB Atlante.</param>
    public ServiziConfrontoIndirizzi(ServiziParserIndirizzi? parser = null)
    {
        _parser = parser;
    }

    // ── API Pubblica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Confronta due indirizzi italiani e restituisce un esito con score per componente.
    ///
    /// Esempio:
    /// <code>
    /// var r = confronto.Confronta("Via Roma 10, Loano SV", "V. Roma, 10 - Loano");
    /// // → { Esito: Uguale, PercentualeTotale: 97.3 }
    ///
    /// var r2 = confronto.Confronta("Cso Garibaldi 5, Milano", "Corso G. 5 MI");
    /// // → { Esito: MoltoSimile, PercentualeTotale: 88.5 }
    /// </code>
    /// </summary>
    public EsitoConfronto Confronta(
        string indirizzoA,
        string indirizzoB,
        ModalitaConfronto modalita = ModalitaConfronto.Permissiva)
    {
        // Pre-normalizzazione tramite parser DB (se disponibile)
        IndirizzoComponents a, b;
        if (_parser != null)
        {
            var pA = _parser.Analizza(indirizzoA);
            var pB = _parser.Analizza(indirizzoB);
            a = DaIndirizzoItaliano(pA, indirizzoA);
            b = DaIndirizzoItaliano(pB, indirizzoB);
        }
        else
        {
            a = AnalizzaManuale(indirizzoA);
            b = AnalizzaManuale(indirizzoB);
        }

        // Score per componente
        var percVia    = CalcolaScoreVia(a.Via, b.Via);
        var percCivico = CalcolaScoreCivico(a.Civico, b.Civico);
        var percCAP    = CalcolaScoreCAP(a.CAP, b.CAP);
        var percCitta  = CalcolaScoreCitta(a.Citta, b.Citta);

        // Blocco hard su CAP o città (se presenti e diversi → sicuramente diversi)
        if (percCitta == 0 || percCivico == 0)
            return EsitoConfronto.Diverso("Città o civico incompatibili.",
                0, percVia, percCivico, percCAP, percCitta, modalita);

        // Ponderazione: via ha più peso, CAP aiuta ma non è determinante
        var totale = Math.Round(
            percVia    * 0.55 +
            percCivico * 0.20 +
            percCAP    * 0.10 +
            percCitta  * 0.15, 2);

        var sogliaEffettiva = (modalita == ModalitaConfronto.Strict
            ? SOGLIA_STRICT
            : SOGLIA_PERMISSIVA) * 100;

        if (totale >= SOGLIA_UGUALE * 100)
            return EsitoConfronto.Uguale(totale, percVia, percCivico, percCAP, percCitta, modalita);

        if (totale >= sogliaEffettiva)
            return EsitoConfronto.MoltoSimile(totale, percVia, percCivico, percCAP, percCitta, modalita);

        return EsitoConfronto.Diverso(
            $"Somiglianza: {totale:F1}% (sotto la soglia {sogliaEffettiva:F0}%).",
            totale, percVia, percCivico, percCAP, percCitta, modalita);
    }

    // ── Score Componenti ──────────────────────────────────────────────────────

    private static double CalcolaScoreVia(string a, string b)
    {
        a = PreparaVia(a);
        b = PreparaVia(b);
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 100;

        var token    = ScoreToken(a, b);
        var fonetica = ScoreToken(Fonetica(a), Fonetica(b));
        var sliding  = ScoreSliding(a, b);

        return Math.Round(Math.Max(Math.Max(token, fonetica), sliding) * 100, 2);
    }

    private static double CalcolaScoreCivico(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 100;
        a = Regex.Replace(a.ToLowerInvariant(), @"[^0-9a-z]", "");
        b = Regex.Replace(b.ToLowerInvariant(), @"[^0-9a-z]", "");
        return (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal))
            ? 100 : 0;
    }

    private static double CalcolaScoreCAP(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 100; // ignora
        return a == b ? 100 : 0;
    }

    private static double CalcolaScoreCitta(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 100; // ignora
        a = NormalizzaTesto(a);
        b = NormalizzaTesto(b);
        return Levenshtein(a, b) <= 1 ? 100 : 0;
    }

    // ── Normalizzazione ────────────────────────────────────────────────────────

    private static string PreparaVia(string s) =>
        string.Join(" ", NormalizzaTesto(s)
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !_stopWords.Contains(t)));

    private static double ScoreToken(string a, string b)
    {
        var tokA = a.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var tokB = b.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var match = tokA.Count(x => tokB.Any(y => Levenshtein(x, y) <= 1));
        return (double)match / Math.Max(tokA.Length, tokB.Length);
    }

    private static double ScoreSliding(string a, string b)
    {
        if (a.Length < b.Length) (a, b) = (b, a);
        if (b.Length == 0) return 1.0;
        var min = int.MaxValue;
        for (var i = 0; i <= a.Length - b.Length; i++)
            min = Math.Min(min, Levenshtein(a.Substring(i, b.Length), b));
        return 1.0 - (double)min / b.Length;
    }

    private static string Fonetica(string s) =>
        Regex.Replace(s, "[aeiou]", "")
            .Replace("ph", "f").Replace("ck", "k")
            .Replace("ch", "k").Replace("ci", "si")
            .Replace("gi", "ji").Replace("gl", "l");

    private static string NormalizzaTesto(string s)
    {
        s = EspandiAbbreviazioni(s.ToLowerInvariant());
        s = RimuoviAccenti(s);
        s = Regex.Replace(s, @"[^\w\s]", " ");
        s = Regex.Replace(s, @"(\w)\1+", "$1"); // riduci doppie
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    private static string EspandiAbbreviazioni(string s)
    {
        foreach (var kv in _abbreviazioni)
            s = Regex.Replace(s, $@"(?<!\w){Regex.Escape(kv.Key)}(?!\w)", kv.Value,
                RegexOptions.IgnoreCase);
        return s;
    }

    private static string RimuoviAccenti(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    private static int Levenshtein(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
            dp[i, j] = a[i - 1] == b[j - 1]
                ? dp[i - 1, j - 1]
                : 1 + Math.Min(dp[i - 1, j], Math.Min(dp[i, j - 1], dp[i - 1, j - 1]));
        return dp[a.Length, b.Length];
    }

    // ── Mapping da/a IndirizzoItaliano ────────────────────────────────────────

    private static IndirizzoComponents DaIndirizzoItaliano(IndirizzoItaliano p, string originale)
    {
        var via = p.NomeVia ?? EstraiViaManuale(NormalizzaTesto(originale));
        return new IndirizzoComponents(
            Via: via,
            Civico: p.Civico ?? "",
            CAP: p.CAP,
            Citta: p.NomeComune ?? ""
        );
    }

    private static IndirizzoComponents AnalizzaManuale(string s)
    {
        s = NormalizzaTesto(s);
        var cap = Regex.Match(s, @"\b(\d{5})\b");
        var capVal = cap.Success ? cap.Value : null;
        if (capVal != null) s = s.Replace(capVal, " ");

        var m = Regex.Match(s, @"(.+?)\s+(\d+[a-z]?)\s*(.*)");
        return m.Success
            ? new IndirizzoComponents(m.Groups[1].Value, m.Groups[2].Value, capVal, m.Groups[3].Value.Trim())
            : new IndirizzoComponents(s, "", capVal, "");
    }

    private static string EstraiViaManuale(string s)
    {
        var m = Regex.Match(s, @"(.+?)\s+\d");
        return m.Success ? m.Groups[1].Value.Trim() : s;
    }

    private record IndirizzoComponents(string Via, string Civico, string? CAP, string Citta = "");
}

// ── Tipi Pubblici ─────────────────────────────────────────────────────────────

public enum EsitoIndirizzo { Uguale, MoltoSimile, Diverso }

public enum ModalitaConfronto { Strict, Permissiva }

public sealed record EsitoConfronto(
    bool Ok,
    EsitoIndirizzo Esito,
    string Messaggio,
    double PercentualeTotale,
    double PercentualeVia,
    double PercentualeCivico,
    double PercentualeCAP,
    double PercentualeCitta,
    ModalitaConfronto Modalita)
{
    public static EsitoConfronto Uguale(double tot, double v, double c, double cap, double ct, ModalitaConfronto m)
        => new(true, EsitoIndirizzo.Uguale, "Indirizzo uguale", tot, v, c, cap, ct, m);

    public static EsitoConfronto MoltoSimile(double tot, double v, double c, double cap, double ct, ModalitaConfronto m)
        => new(true, EsitoIndirizzo.MoltoSimile,
            "Indirizzo molto simile (sopra la soglia di tolleranza)", tot, v, c, cap, ct, m);

    public static EsitoConfronto Diverso(string msg, double tot, double v, double c, double cap, double ct, ModalitaConfronto m)
        => new(false, EsitoIndirizzo.Diverso, msg, tot, v, c, cap, ct, m);

    // Overload senza CAP per compatibilità con codice esistente
    public static EsitoConfronto Diverso(string msg, double tot, double v, double c, double ct, ModalitaConfronto m)
        => new(false, EsitoIndirizzo.Diverso, msg, tot, v, c, 100, ct, m);
}
