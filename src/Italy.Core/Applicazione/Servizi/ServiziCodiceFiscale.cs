using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi per la validazione e il calcolo del Codice Fiscale italiano.
/// Implementa l'algoritmo ufficiale dell'Agenzia delle Entrate.
/// </summary>
public sealed class ServiziCodiceFiscale
{
    private readonly IRepositoryComuni _repositoryComuni;

    // Tabella di conversione carattere → valore dispari (posizioni 1,3,5,...)
    private static readonly Dictionary<char, int> _valoriDispari = new()
    {
        {'0',1},{'1',0},{'2',5},{'3',7},{'4',9},{'5',13},{'6',15},{'7',17},{'8',19},{'9',21},
        {'A',1},{'B',0},{'C',5},{'D',7},{'E',9},{'F',13},{'G',15},{'H',17},{'I',19},{'J',21},
        {'K',2},{'L',4},{'M',18},{'N',20},{'O',11},{'P',3},{'Q',6},{'R',8},{'S',12},{'T',14},
        {'U',16},{'V',10},{'W',22},{'X',25},{'Y',24},{'Z',23}
    };

    private static readonly string _mesiCodice = "ABCDEHLMPRST";

    public ServiziCodiceFiscale(IRepositoryComuni repositoryComuni)
    {
        _repositoryComuni = repositoryComuni ?? throw new ArgumentNullException(nameof(repositoryComuni));
    }

    // ── Validazione ──────────────────────────────────────────────────────────

    /// <summary>
    /// Valida un Codice Fiscale e ne estrae i dati anagrafici.
    /// </summary>
    public RisultatoCodiceFiscale Valida(string codiceFiscale)
    {
        var anomalie = new List<string>();
        if (string.IsNullOrWhiteSpace(codiceFiscale))
            return Invalido(["Il Codice Fiscale non può essere vuoto."]);

        var cf = codiceFiscale.Trim().ToUpperInvariant();

        if (cf.Length != 16)
            return Invalido([$"Lunghezza errata: {cf.Length} caratteri (attesi 16)."]);

        // Verifica carattere di controllo
        if (!VerificaCarattereControllo(cf))
            return Invalido(["Carattere di controllo non valido."]);

        // Decodifica data e sesso
        if (!TentaDecodificaData(cf, out var dataNascita, out var sesso))
            return Invalido(["Data di nascita non decodificabile."]);

        // Decodifica comune di nascita
        var codiceBelfiore = cf.Substring(11, 4);
        Comune? comune = null;
        if (codiceBelfiore[0] == 'Z')
        {
            // Nato all'estero - codice paese
            anomalie.Add($"Nato all'estero (codice paese: {codiceBelfiore}).");
        }
        else
        {
            comune = _repositoryComuni.DaCodiceBelfiore(codiceBelfiore);
            if (comune == null)
                anomalie.Add($"Codice Belfiore '{codiceBelfiore}' non trovato nel database.");
            else if (!comune.IsAttivo)
                anomalie.Add($"Il comune '{comune.DenominazioneUfficiale}' è soppresso. Successore: {comune.CodiceSuccessore}.");
        }

        return new RisultatoCodiceFiscale
        {
            IsValido = anomalie.Count == 0,
            ComuneNascita = comune?.DenominazioneUfficiale,
            CodiceBelfiore = codiceBelfiore,
            DataNascita = dataNascita,
            Sesso = sesso,
            CodiceFiscaleNormalizzato = cf,
            Anomalie = anomalie
        };
    }

    // ── Calcolo ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Calcola il Codice Fiscale dato i dati anagrafici.
    /// </summary>
    public string Calcola(string cognome, string nome, DateTime dataNascita, char sesso, string codiceBelfiore)
    {
        if (string.IsNullOrWhiteSpace(cognome)) throw new ArgumentException("Cognome obbligatorio.", nameof(cognome));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Nome obbligatorio.", nameof(nome));
        if (sesso != 'M' && sesso != 'F') throw new ArgumentException("Sesso deve essere 'M' o 'F'.", nameof(sesso));

        var codCognome = CodificaCognome(cognome);
        var codNome = CodificaNome(nome);
        var codData = CodificaData(dataNascita, sesso);
        var codComune = codiceBelfiore.ToUpperInvariant();

        var parziale = codCognome + codNome + codData + codComune;
        var controllo = CalcolaCarattereControllo(parziale);

        return parziale + controllo;
    }

    /// <summary>
    /// Calcola il Codice Fiscale risolvendo automaticamente il codice Belfiore
    /// dal nome del comune e dalla sigla provincia.
    /// </summary>
    /// <exception cref="ArgumentException">Se il comune non viene trovato.</exception>
    public string Calcola(string cognome, string nome, DateTime dataNascita, char sesso, string nomeComune, string siglaProvincia)
    {
        if (string.IsNullOrWhiteSpace(nomeComune))   throw new ArgumentException("Nome comune obbligatorio.", nameof(nomeComune));
        if (string.IsNullOrWhiteSpace(siglaProvincia)) throw new ArgumentException("Sigla provincia obbligatoria.", nameof(siglaProvincia));

        var sigla = siglaProvincia.Trim().ToUpperInvariant();
        var candidati = _repositoryComuni.DaProvincia(sigla);
        var comune = candidati.FirstOrDefault(c =>
            string.Equals(c.DenominazioneUfficiale, nomeComune.Trim(), StringComparison.OrdinalIgnoreCase));

        if (comune == null)
            throw new ArgumentException($"Comune '{nomeComune} ({siglaProvincia})' non trovato.", nameof(nomeComune));

        return Calcola(cognome, nome, dataNascita, sesso, comune.CodiceBelfiore);
    }

    // ── Scomposizione ────────────────────────────────────────────────────────

    /// <summary>
    /// Scompone un Codice Fiscale nei suoi segmenti costitutivi senza effettuare
    /// lookup sul database dei comuni. Utile per analisi strutturale e debug.
    /// </summary>
    /// <param name="codiceFiscale">CF da scomporre (16 caratteri).</param>
    /// <returns>La scomposizione strutturata, o null se il CF è malformato.</returns>
    public ScomposizioneCodiceFiscale? Scomponi(string codiceFiscale)
    {
        if (string.IsNullOrWhiteSpace(codiceFiscale))
            return null;

        var cf = codiceFiscale.Trim().ToUpperInvariant();
        if (cf.Length != 16)
            return null;

        if (!TentaDecodificaData(cf, out var dataNascita, out var sesso))
            return null;

        var belfiore = cf.Substring(11, 4);

        return new ScomposizioneCodiceFiscale
        {
            SegmentoCognome     = cf.Substring(0, 3),
            SegmentoNome        = cf.Substring(3, 3),
            AnnoEncoded         = cf.Substring(6, 2),
            MeseEncoded         = cf[8],
            GiornoEncoded       = cf.Substring(9, 2),
            CodiceBelfiore      = belfiore,
            CarattereControllo  = cf[15],
            DataNascita         = dataNascita,
            Sesso               = sesso,
            IsNatoAllEstero     = belfiore[0] == 'Z'
        };
    }

    // ── Lookup da CF ──────────────────────────────────────────────────────────

    /// <summary>
    /// Restituisce il comune di nascita estratto da un Codice Fiscale.
    /// </summary>
    public Comune? DaCodiceFiscale(string codiceFiscale)
    {
        var risultato = Valida(codiceFiscale);
        if (!risultato.IsValido || risultato.CodiceBelfiore == null)
            return null;
        return _repositoryComuni.DaCodiceBelfiore(risultato.CodiceBelfiore);
    }

    // ── Algoritmi Privati ────────────────────────────────────────────────────

    private static bool VerificaCarattereControllo(string cf)
    {
        var somma = 0;
        for (var i = 0; i < 15; i++)
        {
            var c = cf[i];
            somma += (i % 2 == 0) // posizione dispari (1-based)
                ? _valoriDispari.GetValueOrDefault(c, 0)
                : char.IsDigit(c) ? c - '0' : c - 'A';
        }
        return (char)('A' + somma % 26) == cf[15];
    }

    private static char CalcolaCarattereControllo(string parziale15)
    {
        var somma = 0;
        for (var i = 0; i < 15; i++)
        {
            var c = parziale15[i];
            somma += (i % 2 == 0)
                ? _valoriDispari.GetValueOrDefault(c, 0)
                : char.IsDigit(c) ? c - '0' : c - 'A';
        }
        return (char)('A' + somma % 26);
    }

    private static bool TentaDecodificaData(string cf, out DateTime? data, out char? sesso)
    {
        data = null;
        sesso = null;
        try
        {
            var anno = int.Parse(cf.Substring(6, 2));
            var meseChar = cf[8];
            var mese = _mesiCodice.IndexOf(meseChar) + 1;
            if (mese <= 0) return false;

            var giornoRaw = int.Parse(cf.Substring(9, 2));
            if (giornoRaw > 40)
            {
                sesso = 'F';
                giornoRaw -= 40;
            }
            else
            {
                sesso = 'M';
            }

            // Anno: euristica per 1900/2000
            var annoCompleto = anno >= 0 && anno <= DateTime.Today.Year % 100
                ? 2000 + anno
                : 1900 + anno;

            data = new DateTime(annoCompleto, mese, giornoRaw);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CodificaCognome(string s)
    {
        var consonanti = FiltroConsonanti(s);
        var vocali = FiltroVocali(s);
        var risultato = (consonanti + vocali + "XXX")[..3];
        return risultato.ToUpperInvariant();
    }

    private static string CodificaNome(string s)
    {
        var consonanti = FiltroConsonanti(s);
        if (consonanti.Length >= 4)
            return $"{consonanti[0]}{consonanti[2]}{consonanti[3]}".ToUpperInvariant();
        var vocali = FiltroVocali(s);
        return (consonanti + vocali + "XXX")[..3].ToUpperInvariant();
    }

    private static string CodificaData(DateTime data, char sesso)
    {
        var anno = data.Year % 100;
        var mese = _mesiCodice[data.Month - 1];
        var giorno = sesso == 'F' ? data.Day + 40 : data.Day;
        return $"{anno:D2}{mese}{giorno:D2}";
    }

    private static string FiltroConsonanti(string s) =>
        new(s.ToUpperInvariant()
              .Where(c => "BCDFGHJKLMNPQRSTVWXYZ".Contains(c))
              .ToArray());

    private static string FiltroVocali(string s) =>
        new(s.ToUpperInvariant()
              .Where(c => "AEIOU".Contains(c))
              .ToArray());

    private static RisultatoCodiceFiscale Invalido(IReadOnlyList<string> anomalie) =>
        new() { IsValido = false, Anomalie = anomalie };
}
