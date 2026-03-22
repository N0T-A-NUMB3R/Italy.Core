using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Italy.Core.Applicazione.Servizi;

namespace Italy.Core.PA
{
    /// <summary>
    /// Integrazione con il catalogo IndicePA (Indice delle Pubbliche Amministrazioni).
    /// Espone ricerca enti, codici SdI per fatturazione B2G, PEC ufficiali e dati anagrafici PA.
    ///
    /// Tutti gli endpoint usano l'Open Data API pubblica di IndicePA (CKAN DataStore).
    /// Nessuna autenticazione richiesta.
    /// Fonte: indicepa.gov.it/ipa-dati/api/3/action/datastore_search
    /// Licenza dati: CC BY 4.0.
    ///
    /// Utilizzo base:
    ///   var pa = new ServiziPAEstesi(atlante.Comuni);
    ///   var enti = await pa.CercaEnteIPAAsync("Comune di Milano");
    ///   var sdi  = await pa.OttieniCodiciSdIAsync("cod_amm_milano");
    ///   var pref = await pa.CercaEnteIPAAsync("Prefettura di Roma");
    /// </summary>
    public sealed class ServiziPAEstesi
    {
        private readonly ServiziComuni _comuni;
        private readonly HttpClient _http;

        private const string BaseUrl    = "https://indicepa.gov.it/ipa-dati/api/3/action/";
        private const string ResAmm     = "3ed63523-ff9c-41f6-a6fe-980f3d9e501f"; // amministrazioni
        private const string ResUffici  = "c_1f9bca5b-ff44-4b7e-a9a5-03f0c6a6dd3b"; // uffici/SdI

        /// <summary>
        /// Crea il servizio con HttpClient di default.
        /// </summary>
        public ServiziPAEstesi(ServiziComuni comuni) : this(comuni, new HttpClient())
        {
        }

        /// <summary>
        /// Crea il servizio con HttpClient fornito (es. tramite IHttpClientFactory).
        /// </summary>
        public ServiziPAEstesi(ServiziComuni comuni, HttpClient httpClient)
        {
            _comuni = comuni ?? throw new ArgumentNullException(nameof(comuni));
            _http   = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_http.BaseAddress == null)
                _http.BaseAddress = new Uri(BaseUrl);

            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

            if (_http.Timeout == TimeSpan.FromSeconds(100))
                _http.Timeout = TimeSpan.FromSeconds(30);
        }

        // ── Ricerca enti ──────────────────────────────────────────────────────────

        /// <summary>
        /// Ricerca enti nel catalogo IPA per nome (full-text, case-insensitive).
        /// </summary>
        /// <param name="query">Testo da cercare (es. "Comune di Milano", "INPS", "ASL Napoli").</param>
        /// <param name="maxRisultati">Numero massimo di risultati (default 10, max 100).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<IReadOnlyList<EnteIPA>> CercaEnteIPAAsync(
            string query,
            int maxRisultati = 10,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<EnteIPA>();

            var url = "datastore_search?resource_id=" + ResAmm
                    + "&q=" + Uri.EscapeDataString(query.Trim())
                    + "&limit=" + Math.Min(maxRisultati, 100);

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParseEntiIPA(json);
            }
            catch (HttpRequestException ex)
            {
                throw new IndicepaException("Errore ricerca enti IPA: " + query, ex);
            }
        }

        /// <summary>
        /// Restituisce tutti gli enti IPA con sede nel comune indicato (per sigla provincia).
        /// </summary>
        /// <param name="siglaProvincia">Sigla provincia (es. "MI", "RM").</param>
        /// <param name="maxRisultati">Numero massimo di risultati (default 50).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<IReadOnlyList<EnteIPA>> OttieniEntiIPAPerProvinciaAsync(
            string siglaProvincia,
            int maxRisultati = 50,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(siglaProvincia))
                return new List<EnteIPA>();

            var sigla = siglaProvincia.Trim().ToUpperInvariant();
            var sql   = "SELECT * FROM \"" + ResAmm + "\" WHERE \"Provincia\" = '" + sigla + "' LIMIT " + Math.Min(maxRisultati, 100);
            var url   = "datastore_search_sql?sql=" + Uri.EscapeDataString(sql);

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParseEntiIPA(json);
            }
            catch (HttpRequestException ex)
            {
                throw new IndicepaException("Errore recupero enti IPA per provincia " + siglaProvincia, ex);
            }
        }

        /// <summary>
        /// Cerca un ente per codice fiscale esatto.
        /// </summary>
        /// <param name="codiceFiscale">Codice fiscale dell'ente (11 cifre per persona giuridica).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<EnteIPA> OttieniEnteIPAPerCFAsync(
            string codiceFiscale,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(codiceFiscale))
                return null;

            var cf  = codiceFiscale.Trim().ToUpperInvariant();
            var sql = "SELECT * FROM \"" + ResAmm + "\" WHERE UPPER(\"cf\") = '" + cf + "' LIMIT 1";
            var url = "datastore_search_sql?sql=" + Uri.EscapeDataString(sql);

            try
            {
                var json  = await _http.GetStringAsync(url).ConfigureAwait(false);
                var lista = ParseEntiIPA(json);
                return lista.Count > 0 ? lista[0] : null;
            }
            catch (HttpRequestException ex)
            {
                throw new IndicepaException("Errore recupero ente IPA per CF " + codiceFiscale, ex);
            }
        }

        /// <summary>
        /// Restituisce un ente per codice IPA esatto (es. "c_f205").
        /// </summary>
        /// <param name="codiceIPA">Codice IPA univoco dell'ente.</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<EnteIPA> OttieniEnteIPAPerCodiceAsync(
            string codiceIPA,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(codiceIPA))
                return null;

            var cod = codiceIPA.Trim().ToLowerInvariant();
            var sql = "SELECT * FROM \"" + ResAmm + "\" WHERE LOWER(\"cod_amm\") = '" + cod + "' LIMIT 1";
            var url = "datastore_search_sql?sql=" + Uri.EscapeDataString(sql);

            try
            {
                var json  = await _http.GetStringAsync(url).ConfigureAwait(false);
                var lista = ParseEntiIPA(json);
                return lista.Count > 0 ? lista[0] : null;
            }
            catch (HttpRequestException ex)
            {
                throw new IndicepaException("Errore recupero ente IPA per codice " + codiceIPA, ex);
            }
        }

        // ── Codici SdI ────────────────────────────────────────────────────────────

        /// <summary>
        /// Restituisce i codici destinatario SdI (7 caratteri) per un ente IPA.
        /// Usati nella fatturazione elettronica B2G (XML FatturaPA campo CodiceDestinatario).
        /// </summary>
        /// <param name="codiceIPA">Codice IPA dell'ente (campo cod_amm).</param>
        /// <param name="ct">Token di cancellazione.</param>
        public async Task<IReadOnlyList<CodiceDestinatarioSdI>> OttieniCodiciSdIAsync(
            string codiceIPA,
            CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(codiceIPA))
                return new List<CodiceDestinatarioSdI>();

            var cod = codiceIPA.Trim().ToLowerInvariant();
            var sql = "SELECT * FROM \"" + ResUffici + "\" WHERE LOWER(\"cod_amm\") = '" + cod + "'";
            var url = "datastore_search_sql?sql=" + Uri.EscapeDataString(sql);

            try
            {
                var json = await _http.GetStringAsync(url).ConfigureAwait(false);
                return ParseCodiciSdI(json);
            }
            catch (HttpRequestException ex)
            {
                throw new IndicepaException("Errore recupero codici SdI per ente " + codiceIPA, ex);
            }
        }

        // ── Parsing ───────────────────────────────────────────────────────────────

        private static IReadOnlyList<EnteIPA> ParseEntiIPA(string json)
        {
            var risultati = new List<EnteIPA>();
            var root      = JsonNode.Parse(json);

            var success = root?["success"]?.GetValue<bool>();
            if (success != true) return risultati;

            var records = root?["result"]?["records"];
            if (records == null) return risultati;

            foreach (var rec in records.AsArray())
            {
                if (rec == null) continue;

                var pec = Str(rec["mail1"]);
                // Cerca la prima mail di tipo PEC tra mail1..mail5
                for (var i = 1; i <= 5; i++)
                {
                    var tipo = Str(rec["tipo_mail" + i]);
                    if (tipo != null && tipo.ToUpperInvariant().Contains("PEC"))
                    {
                        pec = Str(rec["mail" + i]);
                        break;
                    }
                }

                risultati.Add(new EnteIPA(
                    codiceIPA:           Str(rec["cod_amm"]) ?? string.Empty,
                    denominazione:       Str(rec["des_amm"]) ?? string.Empty,
                    tipoEnte:            ParseTipoEnte(Str(rec["tipologia_amm"])),
                    codiceFiscale:       Str(rec["cf"]),
                    siglaProvincia:      Str(rec["Provincia"]),
                    comune:              Str(rec["Comune"]),
                    cap:                 Str(rec["Cap"]),
                    indirizzo:           Str(rec["Indirizzo"]),
                    sitoWeb:             Str(rec["sito_istituzionale"]),
                    pec:                 pec,
                    regione:             Str(rec["Regione"]),
                    attivo:              true
                ));
            }

            return risultati;
        }

        private static IReadOnlyList<CodiceDestinatarioSdI> ParseCodiciSdI(string json)
        {
            var risultati = new List<CodiceDestinatarioSdI>();
            var root      = JsonNode.Parse(json);

            var success = root?["success"]?.GetValue<bool>();
            if (success != true) return risultati;

            var records = root?["result"]?["records"];
            if (records == null) return risultati;

            foreach (var rec in records.AsArray())
            {
                if (rec == null) continue;

                var codice = Str(rec["cod_uni_ou"]) ?? Str(rec["Codice_uni_ou"]);
                if (string.IsNullOrWhiteSpace(codice)) continue;

                risultati.Add(new CodiceDestinatarioSdI(
                    codice:               codice,
                    denominazioneUfficio: Str(rec["nome_resp"]) ?? Str(rec["desc_ou"]) ?? string.Empty,
                    tipologiaFattura:     null,
                    attivo:               true
                ));
            }

            return risultati;
        }

        private static string Str(JsonNode node)
        {
            if (node == null) return null;
            var s = node.GetValue<string>();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static TipoEnteIPA ParseTipoEnte(string tipo)
        {
            if (tipo == null) return TipoEnteIPA.AltroEnte;

            var t = tipo.ToUpperInvariant();
            if (t.Contains("MINIST"))    return TipoEnteIPA.Ministero;
            if (t.Contains("REGIONE"))   return TipoEnteIPA.Regione;
            if (t.Contains("COMUNE"))    return TipoEnteIPA.Comune;
            if (t.Contains("PROVINCIA")) return TipoEnteIPA.Provincia;
            if (t.Contains("UNIVER"))    return TipoEnteIPA.Universita;
            if (t.Contains("ASL") || t.Contains("AZIENDA SANIT")) return TipoEnteIPA.ASL;
            if (t.Contains("OSPEDALE") || t.Contains("AO "))      return TipoEnteIPA.AziendaOspedaliera;
            if (t.Contains("CAMERA DI COMMERCIO"))                 return TipoEnteIPA.CameraCommercio;
            if (t.Contains("UNIONE"))    return TipoEnteIPA.UnioneComuni;
            if (t.Contains("COMUNITA") || t.Contains("MONTANO"))   return TipoEnteIPA.ComunitaMontana;
            if (t.Contains("INPS") || t.Contains("INAIL") || t.Contains("PREVID")) return TipoEnteIPA.EntePrevidenziale;
            if (t.Contains("AGENZIA"))   return TipoEnteIPA.AgenziaGoverniativa;
            if (t.Contains("ARPA") || t.Contains("ARPAE"))         return TipoEnteIPA.ARPA;
            return TipoEnteIPA.AltroEnte;
        }
    }

    // ── Domain Models ─────────────────────────────────────────────────────────────

    /// <summary>Ente censito nel catalogo IndicePA (IPA).</summary>
    public sealed class EnteIPA
    {
        /// <summary>Codice IPA univoco (es. "c_f205").</summary>
        public string CodiceIPA { get; }

        /// <summary>Denominazione ufficiale dell'ente.</summary>
        public string Denominazione { get; }

        /// <summary>Tipologia ente (Comune, Regione, ASL, Ministero, ecc.).</summary>
        public TipoEnteIPA TipoEnte { get; }

        /// <summary>Codice fiscale dell'ente.</summary>
        public string CodiceFiscale { get; }

        /// <summary>Sigla provincia sede dell'ente.</summary>
        public string SiglaProvincia { get; }

        /// <summary>Comune sede dell'ente.</summary>
        public string Comune { get; }

        /// <summary>CAP sede dell'ente.</summary>
        public string CAP { get; }

        /// <summary>Indirizzo sede dell'ente.</summary>
        public string Indirizzo { get; }

        /// <summary>Sito web istituzionale.</summary>
        public string SitoWeb { get; }

        /// <summary>Indirizzo PEC ufficiale.</summary>
        public string PEC { get; }

        /// <summary>Regione sede dell'ente.</summary>
        public string Regione { get; }

        /// <summary>Indica se l'ente è attivo nel catalogo IPA.</summary>
        public bool Attivo { get; }

        public EnteIPA(string codiceIPA, string denominazione, TipoEnteIPA tipoEnte,
            string codiceFiscale, string siglaProvincia, string comune, string cap,
            string indirizzo, string sitoWeb, string pec, string regione, bool attivo)
        {
            CodiceIPA      = codiceIPA;
            Denominazione  = denominazione;
            TipoEnte       = tipoEnte;
            CodiceFiscale  = codiceFiscale;
            SiglaProvincia = siglaProvincia;
            Comune         = comune;
            CAP            = cap;
            Indirizzo      = indirizzo;
            SitoWeb        = sitoWeb;
            PEC            = pec;
            Regione        = regione;
            Attivo         = attivo;
        }
    }

    /// <summary>Codice destinatario SdI per fatturazione elettronica B2G (7 caratteri).</summary>
    public sealed class CodiceDestinatarioSdI
    {
        /// <summary>Codice univoco ufficio (7 caratteri, es. "A4707H").</summary>
        public string Codice { get; }

        /// <summary>Denominazione dell'ufficio destinatario.</summary>
        public string DenominazioneUfficio { get; }

        /// <summary>Tipologia fattura accettata (null = qualsiasi).</summary>
        public string TipologiaFattura { get; }

        /// <summary>Indica se il codice è attivo.</summary>
        public bool Attivo { get; }

        public CodiceDestinatarioSdI(string codice, string denominazioneUfficio,
            string tipologiaFattura, bool attivo)
        {
            Codice               = codice;
            DenominazioneUfficio = denominazioneUfficio;
            TipologiaFattura     = tipologiaFattura;
            Attivo               = attivo;
        }
    }

    /// <summary>
    /// Eccezione lanciata quando l'API IndicePA restituisce un errore o è irraggiungibile.
    /// </summary>
    public sealed class IndicepaException : Exception
    {
        public IndicepaException(string message) : base(message) { }
        public IndicepaException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Tipologia ente IPA.</summary>
    public enum TipoEnteIPA
    {
        Ministero, AgenziaGoverniativa, EntePrevidenziale, Regione, Provincia,
        CittaMetropolitana, Comune, UnioneComuni, ComunitaMontana, Universita,
        ASL, AziendaOspedaliera, ARPA, CameraCommercio, AutoritaPortuale, AltroEnte,
    }
}
