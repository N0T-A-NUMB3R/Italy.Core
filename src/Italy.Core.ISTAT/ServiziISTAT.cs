using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Italy.Core.Applicazione.Servizi;

namespace Italy.Core.ISTAT
{
    /// <summary>
    /// Integrazione con l'API SDMX pubblica di ISTAT.
    /// Espone dati demografici, economici e sociali aggiornati per comune, provincia e regione.
    ///
    /// Tutti gli endpoint sono pubblici e non richiedono API key.
    /// Fonte: sdmx.istat.it/SDMXWS/rest/ (standard SDMX 2.1, formato JSON).
    ///
    /// Utilizzo base:
    ///   var istat = new ServiziISTAT(atlante.Comuni);
    ///   var pop = await istat.GetPopolazioneAsync("015146");
    ///   Console.WriteLine(pop.Totale);  // 1.352.000
    /// </summary>
    public sealed class ServiziISTAT
    {
        private readonly ServiziComuni _comuni;
        private readonly HttpClient _http;

        private const string BaseUrl = "https://sdmx.istat.it/SDMXWS/rest/";

        /// <summary>
        /// Crea il servizio usando un HttpClient di default.
        /// </summary>
        /// <param name="comuni">Servizio comuni di Italy.Core (per risolvere codici Belfiore → ISTAT).</param>
        public ServiziISTAT(ServiziComuni comuni) : this(comuni, new HttpClient())
        {
        }

        /// <summary>
        /// Crea il servizio con un HttpClient fornito dall'esterno (es. tramite IHttpClientFactory).
        /// </summary>
        /// <param name="comuni">Servizio comuni di Italy.Core.</param>
        /// <param name="httpClient">HttpClient configurato.</param>
        public ServiziISTAT(ServiziComuni comuni, HttpClient httpClient)
        {
            _comuni = comuni ?? throw new ArgumentNullException(nameof(comuni));
            _http   = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri(BaseUrl);

            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            if (_http.Timeout == TimeSpan.FromSeconds(100))
                _http.Timeout = TimeSpan.FromSeconds(30);
        }

        // ── Popolazione ──────────────────────────────────────────────────────────

        /// <summary>
        /// Restituisce la popolazione residente per un comune tramite codice ISTAT a 6 cifre.
        /// Dataset ISTAT: DCIS_POPRES1 (Popolazione residente al 1° gennaio).
        /// </summary>
        /// <param name="codiceISTAT">Codice ISTAT comune a 6 cifre (es. "015146" per Milano).</param>
        /// <param name="anno">Anno di riferimento (default: anno corrente - 1).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<PopolazioneComune> GetPopolazioneAsync(
            string codiceISTAT,
            int? anno = null,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(codiceISTAT))
                throw new ArgumentException("Codice ISTAT richiesto.", nameof(codiceISTAT));

            var annoRif = anno ?? DateTime.Today.Year - 1;
            var url = "data/DCIS_POPRES1/A." + codiceISTAT + "..T+M+F./?startPeriod=" + annoRif + "&endPeriod=" + annoRif + "&format=jsondata";

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParsePopolazione(json, codiceISTAT, annoRif);
            }
            catch (HttpRequestException ex)
            {
                throw new ISTATException("Errore recupero popolazione per comune " + codiceISTAT + ".", ex);
            }
        }

        /// <summary>
        /// Restituisce la popolazione residente per comune a partire dal codice Belfiore.
        /// Risolve automaticamente Belfiore → codice ISTAT tramite Italy.Core.
        /// </summary>
        /// <param name="codiceBelfiore">Codice catastale del comune (es. "F205" per Milano).</param>
        /// <param name="anno">Anno di riferimento (default: anno corrente - 1).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<PopolazioneComune> GetPopolazioneDaBelfioreAsync(
            string codiceBelfiore,
            int? anno = null,
            CancellationToken ct = default(CancellationToken))
        {
            var comune = _comuni.DaCodiceBelfiore(codiceBelfiore);
            if (comune == null)
                throw new ArgumentException("Comune non trovato per codice Belfiore '" + codiceBelfiore + "'.", nameof(codiceBelfiore));

            return await GetPopolazioneAsync(comune.CodiceISTAT, anno, ct).ConfigureAwait(false);
        }

        // ── Natalità / Mortalità ─────────────────────────────────────────────────

        /// <summary>
        /// Restituisce i tassi di natalità e mortalità per regione (per 1.000 abitanti).
        /// Dataset ISTAT: DCIS_NATALMORTA.
        /// </summary>
        /// <param name="codiceRegioneISTAT">Codice ISTAT regione a 2 cifre (es. "03" per Lombardia).</param>
        /// <param name="anno">Anno di riferimento (default: anno corrente - 1).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<NatalitaMortalita> GetNatalitaMortalitaAsync(
            string codiceRegioneISTAT,
            int? anno = null,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(codiceRegioneISTAT))
                throw new ArgumentException("Codice regione richiesto.", nameof(codiceRegioneISTAT));

            var annoRif = anno ?? DateTime.Today.Year - 1;
            var codice  = codiceRegioneISTAT.PadLeft(2, '0');
            var url     = "data/DCIS_NATALMORTA/A." + codice + "../?startPeriod=" + annoRif + "&endPeriod=" + annoRif + "&format=jsondata";

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParseNatalitaMortalita(json, codiceRegioneISTAT, annoRif);
            }
            catch (HttpRequestException ex)
            {
                throw new ISTATException("Errore recupero natalità/mortalità per regione " + codiceRegioneISTAT + ".", ex);
            }
        }

        // ── Inflazione ───────────────────────────────────────────────────────────

        /// <summary>
        /// Restituisce gli indici di inflazione più recenti (NIC, FOI, IPCA).
        /// Dataset ISTAT: DCSP_PREZZI.
        /// </summary>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<IndiciInflazione> GetInflazioneAsync(
            CancellationToken ct = default(CancellationToken))
        {
            var url = "data/DCSP_PREZZI/M..NIC+FOI+IPCA./?lastNObservations=1&format=jsondata";

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParseInflazione(json);
            }
            catch (HttpRequestException ex)
            {
                throw new ISTATException("Errore recupero indici inflazione ISTAT.", ex);
            }
        }

        // ── PIL Regionale ────────────────────────────────────────────────────────

        /// <summary>
        /// Restituisce il PIL e il PIL pro capite per regione.
        /// Dataset ISTAT: DCSC_PILPROCAP.
        /// Nota: i dati PIL sono disponibili con circa 2 anni di ritardo.
        /// </summary>
        /// <param name="codiceRegioneISTAT">Codice ISTAT regione a 2 cifre (es. "03" per Lombardia).</param>
        /// <param name="anno">Anno di riferimento (default: anno corrente - 2).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<PILRegione> GetPILRegioneAsync(
            string codiceRegioneISTAT,
            int? anno = null,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(codiceRegioneISTAT))
                throw new ArgumentException("Codice regione richiesto.", nameof(codiceRegioneISTAT));

            var annoRif = anno ?? DateTime.Today.Year - 2;
            var codice  = codiceRegioneISTAT.PadLeft(2, '0');
            var url     = "data/DCSC_PILPROCAP/A." + codice + "./?startPeriod=" + annoRif + "&endPeriod=" + annoRif + "&format=jsondata";

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParsePIL(json, codiceRegioneISTAT, annoRif);
            }
            catch (HttpRequestException ex)
            {
                throw new ISTATException("Errore recupero PIL per regione " + codiceRegioneISTAT + ".", ex);
            }
        }

        // ── Mercato del Lavoro ───────────────────────────────────────────────────

        /// <summary>
        /// Restituisce i principali indicatori del mercato del lavoro per provincia.
        /// Dataset ISTAT: DCIS_OCUPATDIS.
        /// </summary>
        /// <param name="siglaProvincia">Sigla provincia (es. "MI", "RM", "NA").</param>
        /// <param name="anno">Anno di riferimento (default: anno corrente - 1).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<MercatoLavoroProvincia> GetMercatoLavoroAsync(
            string siglaProvincia,
            int? anno = null,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(siglaProvincia))
                throw new ArgumentException("Sigla provincia richiesta.", nameof(siglaProvincia));

            var annoRif = anno ?? DateTime.Today.Year - 1;
            var sigla   = siglaProvincia.Trim().ToUpperInvariant();
            var url     = "data/DCIS_OCUPATDIS/A." + sigla + "../?startPeriod=" + annoRif + "&endPeriod=" + annoRif + "&format=jsondata";

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParseMercatoLavoro(json, sigla, annoRif);
            }
            catch (HttpRequestException ex)
            {
                throw new ISTATException("Errore recupero mercato lavoro per provincia " + siglaProvincia + ".", ex);
            }
        }

        // ── Famiglie ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Restituisce statistiche sulle famiglie residenti per comune.
        /// Dataset ISTAT: DCIS_FAMIGLIE.
        /// </summary>
        /// <param name="codiceISTAT">Codice ISTAT comune a 6 cifre.</param>
        /// <param name="anno">Anno di riferimento (default: anno corrente - 1).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<FamiglieComune> GetFamiglieAsync(
            string codiceISTAT,
            int? anno = null,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(codiceISTAT))
                throw new ArgumentException("Codice ISTAT richiesto.", nameof(codiceISTAT));

            var annoRif = anno ?? DateTime.Today.Year - 1;
            var url     = "data/DCIS_FAMIGLIE/A." + codiceISTAT + "../?startPeriod=" + annoRif + "&endPeriod=" + annoRif + "&format=jsondata";

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParseFamiglie(json, codiceISTAT, annoRif);
            }
            catch (HttpRequestException ex)
            {
                throw new ISTATException("Errore recupero famiglie per comune " + codiceISTAT + ".", ex);
            }
        }

        // ── Parsing SDMX JSON ────────────────────────────────────────────────────

        private static PopolazioneComune ParsePopolazione(string json, string codiceISTAT, int anno)
        {
            var root = JsonNode.Parse(json);
            var obs  = root?["dataSets"]?[0]?["observations"];
            if (obs == null) return null;

            long? totale = null, maschi = null, femmine = null;

            foreach (var kv in obs.AsObject())
            {
                var rawVal = kv.Value?[0]?.GetValue<double>();
                if (rawVal == null) continue;
                var val = (long)rawVal.Value;

                var parts = kv.Key.Split(':');
                if (parts.Length < 3) continue;

                var sesso = parts[2];
                if      (sesso == "0") totale  = val;
                else if (sesso == "1") maschi  = val;
                else if (sesso == "2") femmine = val;
            }

            if (totale == null) return null;

            return new PopolazioneComune(codiceISTAT, anno, totale.Value, maschi, femmine);
        }

        private static NatalitaMortalita ParseNatalitaMortalita(string json, string codiceRegione, int anno)
        {
            var root = JsonNode.Parse(json);
            var obs  = root?["dataSets"]?[0]?["observations"];
            if (obs == null) return null;

            decimal? tassoNatalita = null, tassoMortalita = null;

            foreach (var kv in obs.AsObject())
            {
                var rawVal = kv.Value?[0]?.GetValue<double>();
                if (rawVal == null) continue;
                var val = (decimal)rawVal.Value;

                var parts = kv.Key.Split(':');
                var ind   = parts.Length > 1 ? parts[1] : "0";

                if      (ind == "0") tassoNatalita  = val;
                else if (ind == "1") tassoMortalita = val;
            }

            return new NatalitaMortalita(codiceRegione, anno, tassoNatalita, tassoMortalita);
        }

        private static IndiciInflazione ParseInflazione(string json)
        {
            var root   = JsonNode.Parse(json);
            var series = root?["dataSets"]?[0]?["series"];
            if (series == null) return null;

            decimal? nic = null, foi = null, ipca = null;
            var periodo = string.Empty;

            foreach (var s in series.AsObject())
            {
                var obsInner = s.Value?["observations"];
                if (obsInner == null) continue;

                foreach (var kv in obsInner.AsObject())
                {
                    periodo = kv.Key;
                    var rawVal = kv.Value?[0]?.GetValue<double>();
                    if (rawVal == null) continue;
                    var val = (decimal)rawVal.Value;

                    var parts = s.Key.Split(':');
                    var ind   = parts.Length > 2 ? parts[2] : "0";

                    if      (ind == "0") nic  = val;
                    else if (ind == "1") foi  = val;
                    else if (ind == "2") ipca = val;
                }
            }

            return new IndiciInflazione(periodo, nic, foi, ipca);
        }

        private static PILRegione ParsePIL(string json, string codiceRegione, int anno)
        {
            var root = JsonNode.Parse(json);
            var obs  = root?["dataSets"]?[0]?["observations"];
            if (obs == null) return null;

            decimal? pilProCapite = null, pilTotale = null;

            foreach (var kv in obs.AsObject())
            {
                var rawVal = kv.Value?[0]?.GetValue<double>();
                if (rawVal == null) continue;
                var val = (decimal)rawVal.Value;

                var parts = kv.Key.Split(':');
                var ind   = parts.Length > 1 ? parts[1] : "0";

                if      (ind == "0") pilProCapite = val;
                else if (ind == "1") pilTotale    = val;
            }

            return new PILRegione(codiceRegione, anno, pilProCapite, pilTotale);
        }

        private static MercatoLavoroProvincia ParseMercatoLavoro(string json, string siglaProvincia, int anno)
        {
            var root = JsonNode.Parse(json);
            var obs  = root?["dataSets"]?[0]?["observations"];
            if (obs == null) return null;

            decimal? tassoOccupazione = null, tassoDisoccupazione = null, tassoAttivita = null;

            foreach (var kv in obs.AsObject())
            {
                var rawVal = kv.Value?[0]?.GetValue<double>();
                if (rawVal == null) continue;
                var val = (decimal)rawVal.Value;

                var parts = kv.Key.Split(':');
                var ind   = parts.Length > 1 ? parts[1] : "0";

                if      (ind == "0") tassoOccupazione    = val;
                else if (ind == "1") tassoDisoccupazione = val;
                else if (ind == "2") tassoAttivita       = val;
            }

            return new MercatoLavoroProvincia(siglaProvincia, anno, tassoOccupazione, tassoDisoccupazione, tassoAttivita);
        }

        private static FamiglieComune ParseFamiglie(string json, string codiceISTAT, int anno)
        {
            var root = JsonNode.Parse(json);
            var obs  = root?["dataSets"]?[0]?["observations"];
            if (obs == null) return null;

            long?    numeroFamiglie  = null;
            decimal? componentoMedio = null;

            foreach (var kv in obs.AsObject())
            {
                var rawVal = kv.Value?[0]?.GetValue<double>();
                if (rawVal == null) continue;

                var parts = kv.Key.Split(':');
                var ind   = parts.Length > 1 ? parts[1] : "0";

                if      (ind == "0") numeroFamiglie  = (long)rawVal.Value;
                else if (ind == "1") componentoMedio = (decimal)rawVal.Value;
            }

            return new FamiglieComune(codiceISTAT, anno, numeroFamiglie, componentoMedio);
        }
    }
}
