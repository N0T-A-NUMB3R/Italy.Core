using System;

namespace Italy.Core.ISTAT
{
    /// <summary>Popolazione residente al 1° gennaio per comune.</summary>
    public sealed class PopolazioneComune
    {
        /// <summary>Codice ISTAT comune a 6 cifre.</summary>
        public string CodiceISTAT { get; }

        /// <summary>Anno di riferimento.</summary>
        public int Anno { get; }

        /// <summary>Popolazione totale residente.</summary>
        public long Totale { get; }

        /// <summary>Popolazione maschile (null se non disponibile).</summary>
        public long? Maschi { get; }

        /// <summary>Popolazione femminile (null se non disponibile).</summary>
        public long? Femmine { get; }

        public PopolazioneComune(string codiceISTAT, int anno, long totale, long? maschi, long? femmine)
        {
            CodiceISTAT = codiceISTAT;
            Anno        = anno;
            Totale      = totale;
            Maschi      = maschi;
            Femmine     = femmine;
        }
    }

    /// <summary>Tassi di natalità e mortalità per regione (per 1.000 abitanti).</summary>
    public sealed class NatalitaMortalita
    {
        /// <summary>Codice ISTAT regione a 2 cifre.</summary>
        public string CodiceRegioneISTAT { get; }

        /// <summary>Anno di riferimento.</summary>
        public int Anno { get; }

        /// <summary>Nati vivi per 1.000 abitanti.</summary>
        public decimal? TassoNatalita { get; }

        /// <summary>Morti per 1.000 abitanti.</summary>
        public decimal? TassoMortalita { get; }

        public NatalitaMortalita(string codiceRegioneISTAT, int anno, decimal? tassoNatalita, decimal? tassoMortalita)
        {
            CodiceRegioneISTAT = codiceRegioneISTAT;
            Anno               = anno;
            TassoNatalita      = tassoNatalita;
            TassoMortalita     = tassoMortalita;
        }
    }

    /// <summary>
    /// Indici di inflazione mensili ISTAT (variazione % tendenziale annua).
    /// </summary>
    public sealed class IndiciInflazione
    {
        /// <summary>Periodo di riferimento in formato "YYYY-MM".</summary>
        public string Periodo { get; }

        /// <summary>NIC — Indice Nazionale dei prezzi al Consumo per l'Intera Collettività.</summary>
        public decimal? IndiceNIC { get; }

        /// <summary>FOI — Indice dei prezzi al Consumo per Famiglie di Operai e Impiegati.</summary>
        public decimal? IndiceFOI { get; }

        /// <summary>IPCA — Indice dei Prezzi al Consumo Armonizzato (standard EU Eurostat).</summary>
        public decimal? IndiceIPCA { get; }

        public IndiciInflazione(string periodo, decimal? indiceNIC, decimal? indiceFOI, decimal? indiceIPCA)
        {
            Periodo    = periodo;
            IndiceNIC  = indiceNIC;
            IndiceFOI  = indiceFOI;
            IndiceIPCA = indiceIPCA;
        }
    }

    /// <summary>PIL e PIL pro capite per regione (valori in euro correnti).</summary>
    public sealed class PILRegione
    {
        /// <summary>Codice ISTAT regione a 2 cifre.</summary>
        public string CodiceRegioneISTAT { get; }

        /// <summary>Anno di riferimento.</summary>
        public int Anno { get; }

        /// <summary>PIL pro capite in euro.</summary>
        public decimal? PILProCapite { get; }

        /// <summary>PIL totale regionale in milioni di euro.</summary>
        public decimal? PILTotale { get; }

        public PILRegione(string codiceRegioneISTAT, int anno, decimal? pilProCapite, decimal? pilTotale)
        {
            CodiceRegioneISTAT = codiceRegioneISTAT;
            Anno               = anno;
            PILProCapite       = pilProCapite;
            PILTotale          = pilTotale;
        }
    }

    /// <summary>Indicatori del mercato del lavoro per provincia (valori percentuali).</summary>
    public sealed class MercatoLavoroProvincia
    {
        /// <summary>Sigla provincia (es. "MI").</summary>
        public string SiglaProvincia { get; }

        /// <summary>Anno di riferimento.</summary>
        public int Anno { get; }

        /// <summary>Tasso di occupazione (% occupati su popolazione 15-64 anni).</summary>
        public decimal? TassoOccupazione { get; }

        /// <summary>Tasso di disoccupazione (% disoccupati su forze lavoro).</summary>
        public decimal? TassoDisoccupazione { get; }

        /// <summary>Tasso di attività (% forze lavoro su popolazione 15-64 anni).</summary>
        public decimal? TassoAttivita { get; }

        public MercatoLavoroProvincia(string siglaProvincia, int anno,
            decimal? tassoOccupazione, decimal? tassoDisoccupazione, decimal? tassoAttivita)
        {
            SiglaProvincia      = siglaProvincia;
            Anno                = anno;
            TassoOccupazione    = tassoOccupazione;
            TassoDisoccupazione = tassoDisoccupazione;
            TassoAttivita       = tassoAttivita;
        }
    }

    /// <summary>Statistiche sulle famiglie residenti per comune.</summary>
    public sealed class FamiglieComune
    {
        /// <summary>Codice ISTAT comune a 6 cifre.</summary>
        public string CodiceISTAT { get; }

        /// <summary>Anno di riferimento.</summary>
        public int Anno { get; }

        /// <summary>Numero di famiglie residenti.</summary>
        public long? NumeroFamiglie { get; }

        /// <summary>Numero medio di componenti per famiglia.</summary>
        public decimal? ComponentoMedio { get; }

        public FamiglieComune(string codiceISTAT, int anno, long? numeroFamiglie, decimal? componentoMedio)
        {
            CodiceISTAT     = codiceISTAT;
            Anno            = anno;
            NumeroFamiglie  = numeroFamiglie;
            ComponentoMedio = componentoMedio;
        }
    }

    /// <summary>
    /// Eccezione lanciata quando l'API SDMX di ISTAT restituisce un errore o è irraggiungibile.
    /// </summary>
    public sealed class ISTATException : Exception
    {
        public ISTATException(string message) : base(message) { }
        public ISTATException(string message, Exception inner) : base(message, inner) { }
    }
}
