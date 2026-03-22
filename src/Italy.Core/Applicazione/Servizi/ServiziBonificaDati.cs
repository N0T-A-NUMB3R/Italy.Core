using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Motore di bonifica dati per la pulizia di database legacy con 10-20 anni di errori.
///
/// Casi gestiti:
/// - Comuni rinominati (sigla provincia aggiornata)
/// - Comuni fusi (suggerisce il successore)
/// - CAP non corrispondente al comune dichiarato
/// - Sigla provincia obsoleta (es. vecchie province soppresse: IS, CB, CH pre-riforma)
/// - CF incongruente con il comune di nascita nel record
/// - Varianti ortografiche (accenti mancanti, apostrofi)
/// </summary>
public sealed class ServiziBonificaDati
{
    private readonly IRepositoryComuni _repositoryComuni;
    private readonly IRepositoryCAP _repositoryCAP;
    private readonly ServiziCodiceFiscale _serviziCF;
    private readonly ServiziParserIndirizzi _parser;

    public ServiziBonificaDati(
        IRepositoryComuni repositoryComuni,
        IRepositoryCAP repositoryCAP,
        ServiziCodiceFiscale serviziCF,
        ServiziParserIndirizzi parser)
    {
        _repositoryComuni = repositoryComuni;
        _repositoryCAP = repositoryCAP;
        _serviziCF = serviziCF;
        _parser = parser;
    }

    // ── API Pubblica ──────────────────────────────────────────────────────────

    /// <summary>
    /// Analizza un singolo campo "comune" e suggerisce correzioni.
    ///
    /// Esempio:
    /// <code>
    /// var r = bonifica.AnalizzaComune("Corigliano Calabro", "CS");
    /// // → { RichiedeCorrezione: true, ValoreSuggerito: "Corigliano-Rossano",
    /// //     Tipo: ComuneFuso, Confidenza: 1.0 }
    /// </code>
    /// </summary>
    public RisultatoBonifica AnalizzaComune(string nomeComune, string? siglaProvincia = null)
    {
        if (string.IsNullOrWhiteSpace(nomeComune))
            return Pulito();

        var risultati = _repositoryComuni.Cerca(nomeComune.Trim(), 5);

        // Corrispondenza esatta
        var esatto = risultati.FirstOrDefault(c =>
            string.Equals(c.DenominazioneUfficiale, nomeComune.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.DenominazioneAlternativa, nomeComune.Trim(), StringComparison.OrdinalIgnoreCase));

        if (esatto != null)
        {
            if (!esatto.IsAttivo)
            {
                // Comune soppresso con nome esatto → suggerisci successore
                var successore = _repositoryComuni.DaCodiceBelfiore(esatto.CodiceSuccessore ?? "");
                return new RisultatoBonifica
                {
                    RichiedeCorrezione = true,
                    CampoProblematico = "Comune",
                    ValoreOriginale = nomeComune,
                    ValoreSuggerito = successore?.DenominazioneUfficiale,
                    ConfidenzaSuggerimento = 1.0,
                    Motivazione = $"'{nomeComune}' è stato soppresso" +
                                  (esatto.DataSoppressione.HasValue
                                      ? $" il {esatto.DataSoppressione:dd/MM/yyyy}"
                                      : "") +
                                  (successore != null ? $". Ora è '{successore.DenominazioneUfficiale}'." : "."),
                    Tipo = TipoBonifica.ComuneFuso
                };
            }

            // Comune attivo: verifica coerenza provincia
            if (siglaProvincia != null &&
                !esatto.SiglaProvincia.Equals(siglaProvincia.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return new RisultatoBonifica
                {
                    RichiedeCorrezione = true,
                    CampoProblematico = "SiglaProvincia",
                    ValoreOriginale = siglaProvincia,
                    ValoreSuggerito = esatto.SiglaProvincia,
                    ConfidenzaSuggerimento = 1.0,
                    Motivazione = $"'{nomeComune}' appartiene alla provincia {esatto.SiglaProvincia}, " +
                                  $"non a {siglaProvincia.ToUpperInvariant()}. " +
                                  (esatto.IsCittàMetropolitana
                                      ? $"Nota: è Città Metropolitana."
                                      : ""),
                    Tipo = TipoBonifica.SiglaProvinciaAggiornata
                };
            }

            return Pulito();
        }

        // Nessuna corrispondenza esatta: prova fuzzy
        if (risultati.Count > 0)
        {
            var migliore = siglaProvincia != null
                ? risultati.FirstOrDefault(c => c.SiglaProvincia.Equals(siglaProvincia, StringComparison.OrdinalIgnoreCase))
                  ?? risultati[0]
                : risultati[0];

            var confidenza = CalcolaConfidenzaFuzzy(nomeComune, migliore.DenominazioneUfficiale);

            return new RisultatoBonifica
            {
                RichiedeCorrezione = confidenza < 0.98,
                CampoProblematico = "Comune",
                ValoreOriginale = nomeComune,
                ValoreSuggerito = migliore.DenominazioneUfficiale,
                ConfidenzaSuggerimento = confidenza,
                Motivazione = confidenza >= 0.7
                    ? $"Possibile variante di '{migliore.DenominazioneUfficiale}' ({migliore.SiglaProvincia}). " +
                      $"Confidenza: {confidenza:P0}."
                    : $"'{nomeComune}' non trovato nel database. Suggerimento a bassa confidenza: '{migliore.DenominazioneUfficiale}'.",
                Tipo = TipoBonifica.ComuneRinominato
            };
        }

        return new RisultatoBonifica
        {
            RichiedeCorrezione = true,
            CampoProblematico = "Comune",
            ValoreOriginale = nomeComune,
            ValoreSuggerito = null,
            ConfidenzaSuggerimento = 0.0,
            Motivazione = $"'{nomeComune}' non trovato nel database ISTAT. Verifica manuale necessaria.",
            Tipo = TipoBonifica.ComuneNonTrovato
        };
    }

    /// <summary>
    /// Analizza la coerenza di un indirizzo completo (comune + CAP + provincia).
    /// </summary>
    public IReadOnlyList<RisultatoBonifica> AnalizzaIndirizzo(
        string? nomeComune,
        string? cap,
        string? siglaProvincia,
        string? codiceFiscale = null)
    {
        var risultati = new List<RisultatoBonifica>();

        // 1. Analizza il comune
        if (!string.IsNullOrWhiteSpace(nomeComune))
        {
            var bonificaComune = AnalizzaComune(nomeComune!, siglaProvincia);
            if (bonificaComune.RichiedeCorrezione)
                risultati.Add(bonificaComune);
        }

        // 2. Verifica coerenza CAP ↔ Comune
        if (!string.IsNullOrWhiteSpace(cap) && !string.IsNullOrWhiteSpace(nomeComune))
        {
            var bonificaCAP = VerificaCoerenzaCAP(cap!, nomeComune!, siglaProvincia);
            if (bonificaCAP.RichiedeCorrezione)
                risultati.Add(bonificaCAP);
        }

        // 3. Verifica CF ↔ Comune di nascita (se fornito)
        if (!string.IsNullOrWhiteSpace(codiceFiscale) && !string.IsNullOrWhiteSpace(nomeComune))
        {
            var bonificaCF = VerificaCoerenzaCF(codiceFiscale!, nomeComune!);
            if (bonificaCF.RichiedeCorrezione)
                risultati.Add(bonificaCF);
        }

        return risultati;
    }

    /// <summary>
    /// Elabora un batch di record e restituisce un report completo di bonifica.
    /// Ideale per migrazioni di database legacy.
    /// </summary>
    public ReportBonificaBatch ElaboraBatch(IEnumerable<RecordDaBonificare> records)
    {
        var risultati = new List<RecordBonifica>();
        var indice = 0;

        foreach (var record in records)
        {
            var correzioni = AnalizzaIndirizzo(
                record.NomeComune,
                record.CAP,
                record.SiglaProvincia,
                record.CodiceFiscale);

            risultati.Add(new RecordBonifica
            {
                IndiceRecord = indice++,
                DatoOriginale = record,
                Correzioni = correzioni
            });
        }

        var conAnomalie = risultati.Count(r => r.HasAnomalie);
        return new ReportBonificaBatch
        {
            TotaleRecord = risultati.Count,
            RecordConAnomalie = conAnomalie,
            RecordPuliti = risultati.Count - conAnomalie,
            Risultati = risultati
        };
    }

    /// <summary>
    /// Verifica se una sigla provincia è aggiornata.
    /// Gestisce le province soppresse (es. ex-Carbonia-Iglesias → Sud Sardegna).
    /// </summary>
    public RisultatoBonifica VerificaSiglaProvincia(string sigla, string? nomeComune = null)
    {
        var siglaNorm = sigla.Trim().ToUpperInvariant();

        // Province storiche soppresse con successori noti
        var provinceSoppresse = new Dictionary<string, string>
        {
            { "CI", "SU" },  // Carbonia-Iglesias → Sud Sardegna
            { "OG", "NU" },  // Ogliastra → Nuoro
            { "OT", "SS" },  // Olbia-Tempio → Sassari
            { "VS", "SU" },  // Medio Campidano → Sud Sardegna
        };

        if (provinceSoppresse.TryGetValue(siglaNorm, out var nuovaSigla))
        {
            return new RisultatoBonifica
            {
                RichiedeCorrezione = true,
                CampoProblematico = "SiglaProvincia",
                ValoreOriginale = sigla,
                ValoreSuggerito = nuovaSigla,
                ConfidenzaSuggerimento = 1.0,
                Motivazione = $"La provincia '{siglaNorm}' è stata soppressa. " +
                              $"I comuni appartenenti sono stati assegnati a '{nuovaSigla}'.",
                Tipo = TipoBonifica.SiglaProvinciaAggiornata
            };
        }

        return Pulito();
    }

    // ── Metodi Privati ────────────────────────────────────────────────────────

    private RisultatoBonifica VerificaCoerenzaCAP(string cap, string nomeComune, string? siglaProvincia)
    {
        var zoneCap = _repositoryCAP.DaCAP(cap);
        if (zoneCap.Count == 0)
        {
            return new RisultatoBonifica
            {
                RichiedeCorrezione = true,
                CampoProblematico = "CAP",
                ValoreOriginale = cap,
                ValoreSuggerito = null,
                ConfidenzaSuggerimento = 0.0,
                Motivazione = $"CAP '{cap}' non trovato nel database.",
                Tipo = TipoBonifica.CAPAggiornato
            };
        }

        // Verifica che uno dei comuni associati al CAP corrisponda al comune dichiarato
        var comuniDelCAP = zoneCap
            .SelectMany(z => z.CodiciBelfiore)
            .Select(b => _repositoryComuni.DaCodiceBelfiore(b))
            .Where(c => c != null)
            .ToList();

        var match = comuniDelCAP.Any(c =>
            c!.DenominazioneUfficiale.Contains(nomeComune.Trim(), StringComparison.OrdinalIgnoreCase) ||
            nomeComune.Trim().Contains(c!.DenominazioneUfficiale, StringComparison.OrdinalIgnoreCase));

        if (!match)
        {
            var comuniElencati = string.Join(", ", comuniDelCAP.Take(3).Select(c => c!.DenominazioneUfficiale));
            return new RisultatoBonifica
            {
                RichiedeCorrezione = true,
                CampoProblematico = "CAP",
                ValoreOriginale = cap,
                ValoreSuggerito = null,
                ConfidenzaSuggerimento = 0.5,
                Motivazione = $"Il CAP '{cap}' non corrisponde a '{nomeComune}'. " +
                              $"I comuni con questo CAP sono: {comuniElencati}.",
                Tipo = TipoBonifica.CAPNonCorrispondeAlComune
            };
        }

        return Pulito();
    }

    private RisultatoBonifica VerificaCoerenzaCF(string codiceFiscale, string nomeComune)
    {
        var risultatoCF = _serviziCF.Valida(codiceFiscale);
        if (!risultatoCF.IsValido || risultatoCF.ComuneNascita == null)
            return Pulito();

        if (!risultatoCF.ComuneNascita.Contains(nomeComune.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !nomeComune.Trim().Contains(risultatoCF.ComuneNascita, StringComparison.OrdinalIgnoreCase))
        {
            return new RisultatoBonifica
            {
                RichiedeCorrezione = true,
                CampoProblematico = "CodiceFiscale/Comune",
                ValoreOriginale = $"CF:{codiceFiscale} | Comune:{nomeComune}",
                ValoreSuggerito = risultatoCF.ComuneNascita,
                ConfidenzaSuggerimento = 0.9,
                Motivazione = $"Il Codice Fiscale '{codiceFiscale}' indica come comune di nascita " +
                              $"'{risultatoCF.ComuneNascita}', ma il record riporta '{nomeComune}'.",
                Tipo = TipoBonifica.CodiceFiscaleIncoerente
            };
        }

        return Pulito();
    }

    private static double CalcolaConfidenzaFuzzy(string a, string b)
    {
        a = a.ToLowerInvariant().Trim();
        b = b.ToLowerInvariant().Trim();
        if (a == b) return 1.0;
        var distanza = Levenshtein(a, b);
        return 1.0 - (double)distanza / Math.Max(a.Length, b.Length);
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

    private static RisultatoBonifica Pulito() =>
        new() { RichiedeCorrezione = false, Tipo = TipoBonifica.NessunaCorrezione };
}

/// <summary>Record di input per la bonifica batch.</summary>
public sealed class RecordDaBonificare
{
    public string? NomeComune { get; set; }
    public string? CAP { get; set; }
    public string? SiglaProvincia { get; set; }
    public string? CodiceFiscale { get; set; }
    public string? IndirizzoCompleto { get; set; }
    public Dictionary<string, string?> CampiExtra { get; set; } = new();
}
