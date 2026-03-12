namespace Italy.Core.Domain.Entità;

/// <summary>
/// Snapshot deterministico dello stato di un comune in una data precisa.
/// Risponde alla domanda: "Come si chiamava questo comune il 1° gennaio 1980?"
///
/// Fonte tracciabile: ogni campo include la fonte ufficiale del dato.
/// </summary>
public sealed class SnapshotStorico
{
    public string CodiceBelfiore { get; init; } = string.Empty;
    public DateTime DataSnapshot { get; init; }

    public string DenominazioneInData { get; init; } = string.Empty;
    public string SiglaProvinciaInData { get; init; } = string.Empty;
    public string NomeProvinciaInData { get; init; } = string.Empty;
    public string NomeRegioneInData { get; init; } = string.Empty;

    /// <summary>True se il comune esisteva come entità autonoma in quella data.</summary>
    public bool EsistevaComeEntitàAutonoma { get; init; }

    /// <summary>Se non esisteva come autonomo, indica di quale comune faceva parte.</summary>
    public string? FacevaParte { get; init; }

    /// <summary>Variazione in vigore alla data (es. era già fuso? ancora separato?).</summary>
    public VariazioneStorica? VariazioneVigente { get; init; }

    // ── Tracciabilità Fonte ─────────────────────────────────────────────────
    public string FonteDato { get; init; } = "ISTAT";
    public string? RiferimentoNormativo { get; init; }
    public DateTime? DataAggiornamentoDato { get; init; }
}

/// <summary>
/// Versione del Data Pack del database Atlante.
/// Ogni rilascio di dati ha una versione semantica indipendente dal codice.
/// </summary>
public sealed class VersioneDataPack
{
    /// <summary>Es: "2026.03" (anno.mese aggiornamento ISTAT).</summary>
    public string Versione { get; init; } = string.Empty;

    public DateTime DataPubblicazione { get; init; }
    public DateTime DataRiferimentoISTAT { get; init; }
    public int TotaleComuni { get; init; }
    public int TotaleVariazioni { get; init; }
    public string? Note { get; init; }

    /// <summary>Fonti utilizzate per questo pack.</summary>
    public IReadOnlyList<FonteDatiAtlante> Fonti { get; init; } = Array.Empty<FonteDatiAtlante>();

    public override string ToString() => $"Data Pack {Versione} ({DataPubblicazione:yyyy-MM-dd})";
}

public sealed class FonteDatiAtlante
{
    public string Nome { get; init; } = string.Empty;
    public string URL { get; init; } = string.Empty;
    public DateTime DataDownload { get; init; }
    public string? Checksum { get; init; }
    public TipoFonteDati Tipo { get; init; }
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
