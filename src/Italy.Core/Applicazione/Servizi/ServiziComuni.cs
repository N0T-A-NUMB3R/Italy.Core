using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi di ricerca e navigazione dei comuni italiani.
/// Espone l'API principale della libreria per la gestione dei comuni.
/// </summary>
public sealed class ServiziComuni
{
    private readonly IRepositoryComuni _repository;

    public ServiziComuni(IRepositoryComuni repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // ── Ricerca ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ricerca con Fuzzy Matching (distanza di Levenshtein).
    /// Es: Cerca("Mialno") → restituisce Milano.
    /// </summary>
    public IReadOnlyList<Comune> Cerca(string testo, int massimo = 10) =>
        _repository.Cerca(testo, massimo);

    /// <summary>Ottiene un comune per Codice Belfiore. Lancia eccezione se non trovato.</summary>
    public Comune DaCodiceBelfiore(string codiceBelfiore)
    {
        if (string.IsNullOrWhiteSpace(codiceBelfiore))
            throw new ArgumentException("Il Codice Belfiore non può essere vuoto.", nameof(codiceBelfiore));

        return _repository.DaCodiceBelfiore(codiceBelfiore.ToUpperInvariant())
            ?? throw new CodiceBelfioreNonTrovatoException(codiceBelfiore);
    }

    /// <summary>Ottiene un comune per Codice Belfiore. Restituisce null se non trovato.</summary>
    public Comune? TrovaDaCodiceBelfiore(string codiceBelfiore) =>
        string.IsNullOrWhiteSpace(codiceBelfiore)
            ? null
            : _repository.DaCodiceBelfiore(codiceBelfiore.ToUpperInvariant());

    public Comune? TrovaDaCodiceISTAT(string codiceISTAT) =>
        _repository.DaCodiceISTAT(codiceISTAT);

    /// <summary>
    /// Risolve un codice ISTAT vecchio/storico identificando il comune corrispondente.
    ///
    /// PROBLEMA: i codici ISTAT cambiano con fusioni e soppressioni.
    /// Il Codice Belfiore (Agenzia delle Entrate) è l'unico identificativo immutabile.
    ///
    /// Flusso di risoluzione:
    ///   1. Cerca il comune per codice ISTAT (anche se soppresso)
    ///   2. Se soppresso, identifica il successore attivo
    ///   3. Restituisce sia il dato storico che quello attuale
    ///
    /// Esempio:
    /// <code>
    /// var r = atlante.Comuni.RisolviCodiceISTATStorico("076020");
    /// // r.Comune     → "Corigliano Calabro" (soppresso 2018)
    /// // r.SuccessoreAttivo → "Corigliano-Rossano"
    /// </code>
    /// </summary>
    public RisultatiLookupISTAT RisolviCodiceISTATStorico(string codiceISTAT)
    {
        var comune = _repository.DaCodiceISTAT(codiceISTAT);

        if (comune == null)
        {
            return new RisultatiLookupISTAT
            {
                CodiceISTAT = codiceISTAT,
                Trovato = false,
                Messaggio = $"Codice ISTAT '{codiceISTAT}' non trovato nel database. " +
                            "I codici ISTAT pre-1991 potrebbero non essere presenti. " +
                            "Prova con il Codice Belfiore (es. 'F205') per ricerche storiche complete."
            };
        }

        Comune? successore = null;
        if (!comune.IsAttivo && comune.CodiceSuccessore != null)
            successore = OttieniSuccessore(comune.CodiceBelfiore);

        return new RisultatiLookupISTAT
        {
            CodiceISTAT = codiceISTAT,
            Trovato = true,
            Comune = comune,
            IsAttivo = comune.IsAttivo,
            SuccessoreAttivo = successore,
            Messaggio = comune.IsAttivo
                ? $"Comune attivo: {comune.DenominazioneUfficiale} ({comune.SiglaProvincia})"
                : $"Comune '{comune.DenominazioneUfficiale}' soppresso" +
                  (comune.DataSoppressione.HasValue ? $" il {comune.DataSoppressione:dd/MM/yyyy}" : "") +
                  (successore != null
                      ? $". Successore attuale: {successore.DenominazioneUfficiale} ({successore.SiglaProvincia})"
                      : ". Nessun successore registrato.")
        };
    }

    // ── Gerarchia ────────────────────────────────────────────────────────────

    public IReadOnlyList<Comune> DaProvincia(string siglaProvincia) =>
        _repository.DaProvincia(siglaProvincia.ToUpperInvariant());

    public IReadOnlyList<Comune> DaRegione(string nomeRegione) =>
        _repository.DaRegione(nomeRegione);

    public IReadOnlyList<Comune> DaRipartizione(RipartizioneGeografica ripartizione) =>
        _repository.DaRipartizione(ripartizione);

    // ── Risoluzione Storica ─────────────────────────────────────────────────

    /// <summary>
    /// Restituisce il comune successore attivo di un comune soppresso.
    /// Es: OttieniSuccessore("C619") → Corigliano-Rossano.
    /// </summary>
    public Comune? OttieniSuccessore(string codiceBelfiore) =>
        _repository.OttieniSuccessore(codiceBelfiore.ToUpperInvariant());

    /// <summary>
    /// Recupera i dati corretti di un comune in una data specifica nel passato.
    /// Es: OttieniDatiStorici("A662", new DateTime(1950,1,1)) → "Bagnolo".
    /// </summary>
    public Comune? OttieniDatiStorici(string codiceBelfiore, DateTime data) =>
        _repository.OttieniDatiStorici(codiceBelfiore.ToUpperInvariant(), data);

    /// <summary>Restituisce tutte le variazioni storiche di un comune.</summary>
    public IReadOnlyList<VariazioneStorica> OttieniStorico(string codiceBelfiore) =>
        _repository.OttieniStorico(codiceBelfiore.ToUpperInvariant());

    // ── Statistiche ──────────────────────────────────────────────────────────

    public IReadOnlyList<Comune> TuttiAttivi() => _repository.TuttiAttivi();
    public IReadOnlyList<Comune> TuttiInclusiSoppressi() => _repository.TuttiInclusiSoppressi();
    public int ContaTotale() => _repository.ContaTotale();
    public IReadOnlyList<Comune> OttieniPagina(int pagina, int dimensione = 100) =>
        _repository.OttieniPagina(pagina, dimensione);
}
