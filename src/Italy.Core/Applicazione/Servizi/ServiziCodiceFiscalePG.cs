using Italy.Core.Domain.Entità;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Validazione e calcolo del Codice Fiscale per Persone Giuridiche (società, enti).
///
/// Il CF delle PG è strutturato diversamente da quello delle persone fisiche:
/// - 11 cifre (uguale alla Partita IVA numerica, ma non sempre coincide)
/// - Assegnato dall'Agenzia delle Entrate all'atto di registrazione
/// - Può avere formato alfanumerico per enti non commerciali e PA
///
/// Riferimento normativo: DPR 605/1973, Circolare ADE 92/E del 2001.
/// </summary>
public sealed class ServiziCodiceFiscalePG
{
    // ── Validazione ──────────────────────────────────────────────────────────

    /// <summary>
    /// Valida un Codice Fiscale di Persona Giuridica.
    ///
    /// Gestisce entrambi i formati:
    /// - Numerico (11 cifre): identico alla P.IVA, stesso algoritmo Luhn
    /// - Alfanumerico (16 caratteri): assegnato ad enti pubblici e associazioni
    /// </summary>
    public RisultatoCFPersonaGiuridica Valida(string codiceFiscale)
    {
        if (string.IsNullOrWhiteSpace(codiceFiscale))
            return Invalido(["Il Codice Fiscale non può essere vuoto."]);

        var cf = codiceFiscale.Trim().Replace(" ", "").ToUpperInvariant();

        // Formato numerico (11 cifre) — uguale a Partita IVA
        if (cf.Length == 11 && cf.All(char.IsDigit))
            return ValidaFormatoNumerico(cf);

        // Formato alfanumerico (16 caratteri) — come CF persona fisica
        if (cf.Length == 16)
            return ValidaFormatoAlfanumerico(cf);

        return Invalido([$"Lunghezza non valida: {cf.Length} caratteri (attesi 11 o 16)."]);
    }

    // ── Riconoscimento Tipo Ente ──────────────────────────────────────────────

    /// <summary>
    /// Tenta di identificare il tipo di ente dalla struttura del CF.
    /// </summary>
    public TipoEntePG RiconosciTipoEnte(string codiceFiscale)
    {
        var cf = codiceFiscale.Trim().ToUpperInvariant();
        if (cf.Length == 11 && cf.All(char.IsDigit))
        {
            // Identifica dalla prima cifra del codice camera di commercio
            return cf.Substring(7, 3) switch
            {
                var s when int.Parse(s) == 0 => TipoEntePG.EnteStatale,
                _ => TipoEntePG.SocietàCommerciale
            };
        }

        if (cf.Length == 16)
        {
            // CF alfanumerico: tipicamente enti non commerciali, associazioni, PA
            return cf[0] switch
            {
                'A' or 'B' or 'C' or 'D' => TipoEntePG.AssociazioneEnteNonCommerciale,
                _ => TipoEntePG.AltroEnte
            };
        }

        return TipoEntePG.Sconosciuto;
    }

    // ── Algoritmi Privati ────────────────────────────────────────────────────

    private static RisultatoCFPersonaGiuridica ValidaFormatoNumerico(string cf)
    {
        // Stesso algoritmo Luhn della Partita IVA
        var somma = 0;
        for (var i = 0; i < 10; i++)
        {
            var cifra = cf[i] - '0';
            if (i % 2 == 1)
            {
                cifra *= 2;
                if (cifra > 9) cifra -= 9;
            }
            somma += cifra;
        }
        var controllo = (10 - somma % 10) % 10;
        if (controllo != cf[10] - '0')
            return Invalido(["Cifra di controllo non valida (algoritmo Luhn)."]);

        return new RisultatoCFPersonaGiuridica
        {
            IsValido = true,
            FormatoCF = FormatoCFPG.NumericoUndiciFigure,
            CodiceFiscaleNormalizzato = cf
        };
    }

    private static RisultatoCFPersonaGiuridica ValidaFormatoAlfanumerico(string cf)
    {
        // Stessa logica del CF persona fisica (carattere di controllo)
        var _valoriDispari = new Dictionary<char, int>
        {
            {'0',1},{'1',0},{'2',5},{'3',7},{'4',9},{'5',13},{'6',15},{'7',17},{'8',19},{'9',21},
            {'A',1},{'B',0},{'C',5},{'D',7},{'E',9},{'F',13},{'G',15},{'H',17},{'I',19},{'J',21},
            {'K',2},{'L',4},{'M',18},{'N',20},{'O',11},{'P',3},{'Q',6},{'R',8},{'S',12},{'T',14},
            {'U',16},{'V',10},{'W',22},{'X',25},{'Y',24},{'Z',23}
        };

        var somma = 0;
        for (var i = 0; i < 15; i++)
        {
            var c = cf[i];
            somma += (i % 2 == 0)
                ? _valoriDispari.GetValueOrDefault(c, 0)
                : char.IsDigit(c) ? c - '0' : c - 'A';
        }

        if ((char)('A' + somma % 26) != cf[15])
            return Invalido(["Carattere di controllo non valido."]);

        return new RisultatoCFPersonaGiuridica
        {
            IsValido = true,
            FormatoCF = FormatoCFPG.AlfanumericoSediciCaratteri,
            CodiceFiscaleNormalizzato = cf
        };
    }

    private static RisultatoCFPersonaGiuridica Invalido(IReadOnlyList<string> anomalie) =>
        new() { IsValido = false, Anomalie = anomalie };
}

/// <summary>Risultato della validazione del CF di una Persona Giuridica.</summary>
public sealed class RisultatoCFPersonaGiuridica
{
    public bool IsValido { get; init; }
    public FormatoCFPG FormatoCF { get; init; }
    public string? CodiceFiscaleNormalizzato { get; init; }
    public TipoEntePG TipoEnte { get; init; }
    public IReadOnlyList<string> Anomalie { get; init; } = Array.Empty<string>();
}

public enum FormatoCFPG
{
    NumericoUndiciFigure,
    AlfanumericoSediciCaratteri
}

public enum TipoEntePG
{
    Sconosciuto,
    SocietàCommerciale,
    AssociazioneEnteNonCommerciale,
    EnteStatale,
    AltroEnte
}
