namespace Italy.Core.Domain.Entità;

/// <summary>
/// Scomposizione strutturata di un Codice Fiscale nei suoi segmenti costitutivi.
/// </summary>
public sealed class ScomposizioneCodiceFiscale
{
    /// <summary>I 3 caratteri del cognome (posizioni 0-2).</summary>
    public string SegmentoCognome { get; set; } = string.Empty;

    /// <summary>I 3 caratteri del nome (posizioni 3-5).</summary>
    public string SegmentoNome { get; set; } = string.Empty;

    /// <summary>Le 2 cifre dell'anno di nascita (posizioni 6-7).</summary>
    public string AnnoEncoded { get; set; } = string.Empty;

    /// <summary>Il carattere del mese di nascita (posizione 8).</summary>
    public char MeseEncoded { get; set; }

    /// <summary>Le 2 cifre del giorno (posizione 9-10). Per le donne è il giorno + 40.</summary>
    public string GiornoEncoded { get; set; } = string.Empty;

    /// <summary>Il codice Belfiore del comune/paese di nascita (posizioni 11-14).</summary>
    public string CodiceBelfiore { get; set; } = string.Empty;

    /// <summary>Il carattere di controllo (posizione 15).</summary>
    public char CarattereControllo { get; set; }

    /// <summary>Data di nascita decodificata.</summary>
    public DateTime? DataNascita { get; set; }

    /// <summary>Sesso decodificato ('M' o 'F').</summary>
    public char? Sesso { get; set; }

    /// <summary>True se il soggetto è nato all'estero (Belfiore inizia con 'Z').</summary>
    public bool IsNatoAllEstero { get; set; }
}

/// <summary>Risultato validazione Codice Fiscale.</summary>
public sealed class RisultatoCodiceFiscale
{
    public bool IsValido { get; set; }
    public string? ComuneNascita { get; set; }
    public string? CodiceBelfiore { get; set; }
    public DateTime? DataNascita { get; set; }
    public char? Sesso { get; set; }
    public string? CodiceFiscaleNormalizzato { get; set; }
    public IReadOnlyList<string> Anomalie { get; set; } = Array.Empty<string>();
}

/// <summary>Risultato validazione Partita IVA con lookup ATECO 2007.</summary>
public sealed class RisultatoPartitaIVA
{
    public bool IsValida { get; set; }
    public string? CodiceATECO { get; set; }
    public string? DescrizioneATECO { get; set; }
    public string? SezioneATECO { get; set; }
    public string? ProvinciaSede { get; set; }
    public IReadOnlyList<string> Anomalie { get; set; } = Array.Empty<string>();
}

/// <summary>Risultato validazione IBAN italiano.</summary>
public sealed class RisultatoIBAN
{
    public bool IsValido { get; set; }
    public string? CodiceBIC { get; set; }
    public string? NomeBanca { get; set; }
    public string? FilialeCodice { get; set; }
    public string? IBAN_Formattato { get; set; }
    public IReadOnlyList<string> Anomalie { get; set; } = Array.Empty<string>();
}

/// <summary>Risultato validazione targa automobilistica italiana.</summary>
public sealed class RisultatoTarga
{
    public bool IsValida { get; set; }
    public FormatoTarga Formato { get; set; }
    public string? ProvinciaDiImmatricolazione { get; set; }
    public string? SiglaOrigine { get; set; }
    public string? Note { get; set; }
}

/// <summary>Dati demografici di un comune.</summary>
public sealed class DatiDemografici
{
    public string CodiceBelfiore { get; set; } = string.Empty;
    public int Popolazione { get; set; }
    public int Anno { get; set; }
    public double? SuperficieKmq { get; set; }
    public double? DensitàAbitativa { get; set; }
    public int? MaschiResidenti { get; set; }
    public int? FemmineResidenti { get; set; }
}

/// <summary>Coordinata geografica WGS84.</summary>
public sealed class CoordinateGeo
{
    public double Latitudine { get; set; }
    public double Longitudine { get; set; }
    public double? Altitudine { get; set; }
}

/// <summary>Indirizzo normalizzato per ANPR.</summary>
public sealed class IndirizzoNormalizzato
{
    public string Via { get; set; } = string.Empty;
    public string? Civico { get; set; }
    public string CAP { get; set; } = string.Empty;
    public string Comune { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string FormatoANPR { get; set; } = string.Empty;
    public bool IsValido { get; set; }
    public IReadOnlyList<string> Anomalie { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Risultato della risoluzione di un codice ISTAT storico/vecchio.
/// Contiene sia il dato storico che il comune attivo attuale.
/// </summary>
public sealed class RisultatiLookupISTAT
{
    public string CodiceISTAT { get; set; } = string.Empty;
    public bool Trovato { get; set; }
    public bool IsAttivo { get; set; }

    /// <summary>Il comune (anche se soppresso) corrispondente al codice ISTAT.</summary>
    public Comune? Comune { get; set; }

    /// <summary>Il comune attivo successore (se il comune è soppresso).</summary>
    public Comune? SuccessoreAttivo { get; set; }

    public string Messaggio { get; set; } = string.Empty;
}

/// <summary>Mapping NUTS Eurostat per un comune.</summary>
public sealed class MappingNUTS
{
    public string CodiceBelfiore { get; set; } = string.Empty;
    public string CodiceNUTS3 { get; set; } = string.Empty;
    public string CodiceNUTS2 { get; set; } = string.Empty;
    public string CodiceNUTS1 { get; set; } = string.Empty;
    public string DescrizioneNUTS3 { get; set; } = string.Empty;
}
