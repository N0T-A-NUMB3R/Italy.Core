namespace Italy.Core.Domain.Entità;

/// <summary>Risultato validazione Codice Fiscale.</summary>
public sealed class RisultatoCodiceFiscale
{
    public bool IsValido { get; init; }
    public string? ComuneNascita { get; init; }
    public string? CodiceBelfiore { get; init; }
    public DateTime? DataNascita { get; init; }
    public char? Sesso { get; init; }
    public string? CodiceFiscaleNormalizzato { get; init; }
    public IReadOnlyList<string> Anomalie { get; init; } = Array.Empty<string>();
}

/// <summary>Risultato validazione Partita IVA con lookup ATECO 2007.</summary>
public sealed class RisultatoPartitaIVA
{
    public bool IsValida { get; init; }
    public string? CodiceATECO { get; init; }
    public string? DescrizioneATECO { get; init; }
    public string? SezioneATECO { get; init; }
    public string? ProvinciaSede { get; init; }
    public IReadOnlyList<string> Anomalie { get; init; } = Array.Empty<string>();
}

/// <summary>Risultato validazione IBAN italiano.</summary>
public sealed class RisultatoIBAN
{
    public bool IsValido { get; init; }
    public string? CodiceBIC { get; init; }
    public string? NomeBanca { get; init; }
    public string? FilialeCodice { get; init; }
    public string? IBAN_Formattato { get; init; }
    public IReadOnlyList<string> Anomalie { get; init; } = Array.Empty<string>();
}

/// <summary>Risultato validazione targa automobilistica italiana.</summary>
public sealed class RisultatoTarga
{
    public bool IsValida { get; init; }
    public FormatoTarga Formato { get; init; }
    public string? ProvinciaDiImmatricolazione { get; init; }
    public string? SiglaOrigine { get; init; }
    public string? Note { get; init; }
}

/// <summary>Dati demografici di un comune.</summary>
public sealed class DatiDemografici
{
    public string CodiceBelfiore { get; init; } = string.Empty;
    public int Popolazione { get; init; }
    public int Anno { get; init; }
    public double? SuperficieKmq { get; init; }
    public double? DensitàAbitativa { get; init; }
    public int? MaschiResidenti { get; init; }
    public int? FemmineResidenti { get; init; }
}

/// <summary>Coordinata geografica WGS84.</summary>
public sealed class CoordinateGeo
{
    public double Latitudine { get; init; }
    public double Longitudine { get; init; }
    public double? Altitudine { get; init; }
}

/// <summary>Indirizzo normalizzato per ANPR.</summary>
public sealed class IndirizzoNormalizzato
{
    public string Via { get; init; } = string.Empty;
    public string? Civico { get; init; }
    public string CAP { get; init; } = string.Empty;
    public string Comune { get; init; } = string.Empty;
    public string Provincia { get; init; } = string.Empty;
    public string FormatoANPR { get; init; } = string.Empty;
    public bool IsValido { get; init; }
    public IReadOnlyList<string> Anomalie { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Risultato della risoluzione di un codice ISTAT storico/vecchio.
/// Contiene sia il dato storico che il comune attivo attuale.
/// </summary>
public sealed class RisultatiLookupISTAT
{
    public string CodiceISTAT { get; init; } = string.Empty;
    public bool Trovato { get; init; }
    public bool IsAttivo { get; init; }

    /// <summary>Il comune (anche se soppresso) corrispondente al codice ISTAT.</summary>
    public Comune? Comune { get; init; }

    /// <summary>Il comune attivo successore (se il comune è soppresso).</summary>
    public Comune? SuccessoreAttivo { get; init; }

    public string Messaggio { get; init; } = string.Empty;
}

/// <summary>Mapping NUTS Eurostat per un comune.</summary>
public sealed class MappingNUTS
{
    public string CodiceBelfiore { get; init; } = string.Empty;
    public string CodiceNUTS3 { get; init; } = string.Empty;
    public string CodiceNUTS2 { get; init; } = string.Empty;
    public string CodiceNUTS1 { get; init; } = string.Empty;
    public string DescrizioneNUTS3 { get; init; } = string.Empty;
}
