using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Time Machine deterministica per la risoluzione storica dei comuni italiani.
///
/// Risponde in modo deterministico alle domande:
/// - "Esisteva 'Corigliano' il 1° gennaio 1980?" → sì, era Corigliano Calabro (autonomo)
/// - "Come si chiamava il comune A662 nel 1950?" → "Bagnolo"
/// - "In quale comune era 'Via Roma 5, Corigliano' nel 1990?" → Corigliano Calabro (CS)
/// - "Qual era il Codice Belfiore valido per CF nel 1980?" → C619 (ancora valido allora)
///
/// FONDAMENTALE PER: calcolo CF di persone nate in comuni poi soppressi,
/// validazione di dati anagrafici storici, ricerca atti notarili, pratiche pensionistiche.
/// </summary>
public sealed class ServiziTimeMachine
{
    private readonly IRepositoryComuni _repository;

    public ServiziTimeMachine(IRepositoryComuni repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    // ── API Time Machine ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifica se un comune esisteva come entità autonoma in una data specifica.
    ///
    /// Esempio:
    /// <code>
    /// atlante.TimeMachine.EsistevaInData("Corigliano", new DateTime(1980, 1, 1));
    /// // → true (era ancora Corigliano Calabro, soppresso nel 2018)
    /// </code>
    /// </summary>
    public bool EsistevaInData(string nomeComuneOBelfiore, DateTime data)
    {
        var snapshot = OttieniSnapshotInData(nomeComuneOBelfiore, data);
        return snapshot?.EsistevaComeEntitàAutonoma ?? false;
    }

    /// <summary>
    /// Restituisce lo snapshot completo di un comune in una data specifica.
    /// Usa le variazioni storiche per ricostruire lo stato esatto.
    /// </summary>
    public SnapshotStorico? OttieniSnapshotInData(string nomeComuneOBelfiore, DateTime data)
    {
        // Cerca per nome o belfiore (entrambi i casi)
        Comune? comune = null;
        if (nomeComuneOBelfiore.Length <= 4 && nomeComuneOBelfiore.All(c => char.IsLetterOrDigit(c)))
        {
            // Probabilmente un Codice Belfiore
            comune = _repository.DaCodiceBelfiore(nomeComuneOBelfiore.ToUpperInvariant());
        }

        if (comune == null)
        {
            // Cerca per nome (anche soppressi)
            var tutti = _repository.TuttiInclusiSoppressi();
            comune = tutti.FirstOrDefault(c =>
                string.Equals(c.DenominazioneUfficiale, nomeComuneOBelfiore.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        if (comune == null) return null;

        return CostruisciSnapshot(comune, data);
    }

    /// <summary>
    /// Dato un Codice Fiscale, restituisce il comune di nascita VALIDO alla data di nascita.
    /// Fondamentale per: validazione CF di persone nate in comuni poi soppressi.
    ///
    /// Esempio:
    /// <code>
    /// // Persona nata a Corigliano Calabro nel 1980 (oggi Corigliano-Rossano)
    /// var snapshot = atlante.TimeMachine.OttieniComuneNascitaPerCF("RSSMRA80A01C619Z");
    /// // → SnapshotStorico { DenominazioneinData: "Corigliano Calabro", EsistevaComeEntitàAutonoma: true }
    /// </code>
    /// </summary>
    public SnapshotStorico? OttieniComuneNascitaPerCF(string codiceFiscale)
    {
        if (string.IsNullOrWhiteSpace(codiceFiscale) || codiceFiscale.Length != 16)
            return null;

        var cf = codiceFiscale.ToUpperInvariant();
        var belfiore = cf.Substring(11, 4);
        if (belfiore[0] == 'Z') return null; // Nato all'estero

        // Decodifica data di nascita
        if (!TentaDecodificaAnnoNascita(cf, out var annoNascita))
            return null;

        var dataNascita = new DateTime(annoNascita, 1, 1); // approximazione: usiamo 1° gen dell'anno

        var comune = _repository.DaCodiceBelfiore(belfiore);
        if (comune == null) return null;

        return CostruisciSnapshot(comune, dataNascita);
    }

    /// <summary>
    /// Restituisce la storia completa di un comune come serie di snapshot annuali.
    /// Ideale per visualizzazioni grafiche della storia amministrativa.
    /// </summary>
    public IReadOnlyList<SnapshotStorico> OttieniSerieStorica(
        string codiceBelfiore,
        int annoInizio = 1861,
        int? annoFine = null)
    {
        annoFine ??= DateTime.Today.Year;
        var comune = _repository.DaCodiceBelfiore(codiceBelfiore.ToUpperInvariant());
        if (comune == null) return Array.Empty<SnapshotStorico>();

        var risultati = new List<SnapshotStorico>();
        for (var anno = annoInizio; anno <= annoFine; anno += 5) // campionamento ogni 5 anni
        {
            var data = new DateTime(anno, 1, 1);
            var snapshot = CostruisciSnapshot(comune, data);
            if (snapshot != null)
                risultati.Add(snapshot);
        }
        return risultati;
    }

    /// <summary>
    /// Restituisce il Codice Belfiore corretto da usare nel Codice Fiscale
    /// per una persona nata in quel comune in quella data.
    /// Il Belfiore non cambia mai, quindi questo metodo verifica solo
    /// se il comune esisteva come entità autonoma.
    /// </summary>
    public RisultatoBelfiorePerCF OttieniBelfiorePerCF(string nomeComuneOBelfiore, DateTime dataNascita)
    {
        var snapshot = OttieniSnapshotInData(nomeComuneOBelfiore, dataNascita);

        if (snapshot == null)
            return new RisultatoBelfiorePerCF
            {
                Trovato = false,
                Messaggio = $"Comune '{nomeComuneOBelfiore}' non trovato nel database."
            };

        if (!snapshot.EsistevaComeEntitàAutonoma)
            return new RisultatoBelfiorePerCF
            {
                Trovato = true,
                CodiceBelfiore = snapshot.CodiceBelfiore,
                Valido = false,
                Messaggio = $"Il {dataNascita:dd/MM/yyyy} '{snapshot.DenominazioneInData}' " +
                            $"non esisteva come entità autonoma. Faceva parte di: {snapshot.FacevaParte}."
            };

        return new RisultatoBelfiorePerCF
        {
            Trovato = true,
            CodiceBelfiore = snapshot.CodiceBelfiore,
            DenominazioneInData = snapshot.DenominazioneInData,
            Valido = true,
            Messaggio = $"'{snapshot.DenominazioneInData}' era un comune autonomo il {dataNascita:dd/MM/yyyy}. " +
                        $"Codice Belfiore per CF: {snapshot.CodiceBelfiore}."
        };
    }

    // ── Algoritmi Privati ─────────────────────────────────────────────────────

    private SnapshotStorico CostruisciSnapshot(Comune comune, DateTime data)
    {
        var variazioni = _repository.OttieniStorico(comune.CodiceBelfiore)
            .OrderBy(v => v.DataVariazione)
            .ToList();

        // Trova la variazione in vigore alla data
        var variazioniPassate = variazioni
            .Where(v => v.DataVariazione <= data)
            .OrderByDescending(v => v.DataVariazione)
            .ToList();

        // Il comune esisteva prima della sua istituzione?
        var dataIstituzione = comune.DataIstituzione == DateTime.MinValue
            ? new DateTime(1861, 3, 17) // nascita del Regno d'Italia
            : comune.DataIstituzione;

        if (data < dataIstituzione)
        {
            // Prima dell'istituzione: non esisteva come entità autonoma
            var varIstituzione = variazioni
                .FirstOrDefault(v => v.Tipo == TipoVariazione.Istituzione || v.Tipo == TipoVariazione.Fusione);

            return new SnapshotStorico
            {
                CodiceBelfiore = comune.CodiceBelfiore,
                DataSnapshot = data,
                DenominazioneInData = comune.DenominazioneUfficiale,
                SiglaProvinciaInData = comune.SiglaProvincia,
                NomeProvinciaInData = comune.NomeProvincia,
                NomeRegioneInData = comune.NomeRegione,
                EsistevaComeEntitàAutonoma = false,
                FacevaParte = varIstituzione?.CodiciOrigine.FirstOrDefault(),
                FonteDato = "ISTAT",
            };
        }

        // Dopo la soppressione?
        if (comune.DataSoppressione.HasValue && data >= comune.DataSoppressione.Value)
        {
            return new SnapshotStorico
            {
                CodiceBelfiore = comune.CodiceBelfiore,
                DataSnapshot = data,
                DenominazioneInData = comune.DenominazioneUfficiale,
                SiglaProvinciaInData = comune.SiglaProvincia,
                NomeProvinciaInData = comune.NomeProvincia,
                NomeRegioneInData = comune.NomeRegione,
                EsistevaComeEntitàAutonoma = false,
                FacevaParte = comune.CodiceSuccessore,
                FonteDato = "ISTAT",
            };
        }

        // Comune esisteva: recupera denominazione storica
        var ultimaVariazione = variazioniPassate.FirstOrDefault();
        var denominazione = ultimaVariazione?.DenominazionePrecedente
                            ?? comune.DenominazioneUfficiale;
        var siglaProv = ultimaVariazione?.ProvinciaPrecedente
                        ?? comune.SiglaProvincia;

        return new SnapshotStorico
        {
            CodiceBelfiore = comune.CodiceBelfiore,
            DataSnapshot = data,
            DenominazioneInData = denominazione,
            SiglaProvinciaInData = siglaProv,
            NomeProvinciaInData = comune.NomeProvincia,
            NomeRegioneInData = comune.NomeRegione,
            EsistevaComeEntitàAutonoma = true,
            VariazioneVigente = ultimaVariazione,
            FonteDato = "ISTAT",
            RiferimentoNormativo = ultimaVariazione?.RiferimentoNormativo,
        };
    }

    private static bool TentaDecodificaAnnoNascita(string cf, out int anno)
    {
        anno = 0;
        if (!int.TryParse(cf.Substring(6, 2), out var aa)) return false;
        anno = aa <= DateTime.Today.Year % 100 ? 2000 + aa : 1900 + aa;
        return true;
    }
}

/// <summary>Risultato della ricerca del Codice Belfiore per la compilazione del CF.</summary>
public sealed class RisultatoBelfiorePerCF
{
    public bool Trovato { get; set; }
    public bool Valido { get; set; }
    public string? CodiceBelfiore { get; set; }
    public string? DenominazioneInData { get; set; }
    public string Messaggio { get; set; } = string.Empty;
}
