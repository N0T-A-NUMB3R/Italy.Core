namespace Italy.Core.Domain.Entità;

/// <summary>
/// Snapshot deterministico dello stato di un comune in una data precisa.
/// Risponde alla domanda: "Come si chiamava questo comune il 1° gennaio 1980?"
///
/// Fonte tracciabile: ogni campo include la fonte ufficiale del dato.
/// </summary>
public sealed class SnapshotStorico
{
    public string CodiceBelfiore { get; set; } = string.Empty;
    public DateTime DataSnapshot { get; set; }

    public string DenominazioneInData { get; set; } = string.Empty;
    public string SiglaProvinciaInData { get; set; } = string.Empty;
    public string NomeProvinciaInData { get; set; } = string.Empty;
    public string NomeRegioneInData { get; set; } = string.Empty;

    /// <summary>True se il comune esisteva come entità autonoma in quella data.</summary>
    public bool EsistevaComeEntitàAutonoma { get; set; }

    /// <summary>Se non esisteva come autonomo, indica di quale comune faceva parte.</summary>
    public string? FacevaParte { get; set; }

    /// <summary>Variazione in vigore alla data (es. era già fuso? ancora separato?).</summary>
    public VariazioneStorica? VariazioneVigente { get; set; }

    // ── Tracciabilità Fonte ─────────────────────────────────────────────────
    public string FonteDato { get; set; } = "ISTAT";
    public string? RiferimentoNormativo { get; set; }
    public DateTime? DataAggiornamentoDato { get; set; }
}

/// <summary>
/// Versione del Data Pack del database Atlante.
/// Ogni rilascio di dati ha una versione semantica indipendente dal codice.
/// </summary>
public sealed class VersioneDataPack
{
    /// <summary>Es: "2026.03" (anno.mese aggiornamento ISTAT).</summary>
    public string Versione { get; set; } = string.Empty;

    public DateTime DataPubblicazione { get; set; }
    public DateTime DataRiferimentoISTAT { get; set; }
    public int TotaleComuni { get; set; }
    public int TotaleVariazioni { get; set; }
    public string? Note { get; set; }

    /// <summary>Fonti utilizzate per questo pack.</summary>
    public IReadOnlyList<FonteDatiAtlante> Fonti { get; set; } = Array.Empty<FonteDatiAtlante>();

    public override string ToString() => $"Data Pack {Versione} ({DataPubblicazione:yyyy-MM-dd})";
}

public sealed class FonteDatiAtlante
{
    public string Nome { get; set; } = string.Empty;
    public string URL { get; set; } = string.Empty;
    public DateTime DataDownload { get; set; }
    public string? Checksum { get; set; }
    public TipoFonteDati Tipo { get; set; }
}

public enum TipoFonteDati
{
    ISTAT,
    AgenziaEntrate,
    MinisteroInterno,
    AGCOM,
    IndicePA,
    PosteItaliane,
    Eurostat
}
