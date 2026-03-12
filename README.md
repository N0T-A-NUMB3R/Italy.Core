# Italy.Core

[![NuGet](https://img.shields.io/nuget/v/Italy.Core.svg)](https://www.nuget.org/packages/Italy.Core)
[![CI](https://github.com/YOUR_ORG/Italy.Core/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR_ORG/Italy.Core/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Data Pack](https://img.shields.io/badge/Data_Pack-2026.03-green.svg)](#pipeline-aggiornamento-dati)

> **Il coltellino svizzero C# per i dati amministrativi italiani.**
> Auto-aggiornato mensilmente da ISTAT, IndicePA, GLEIF e GeoNames. Pubblicato automaticamente su NuGet.

---

## Installazione

```shell
dotnet add package Italy.Core
```

**Una sola libreria, nessuna dipendenza aggiuntiva.** Tutti i dati sono embedded nel pacchetto NuGet.

---

## Funzionalità Principali

| Modulo | Funzionalità |
|---|---|
| **Comuni** | Fuzzy search, lookup Belfiore/ISTAT, gerarchia, 7.800+ comuni |
| **Storico** | Time Machine, variazioni 1991-oggi, comuni soppressi/fusi |
| **Codice Fiscale** | Validazione, calcolo, estrazione dati, zero-allocation |
| **ATECO** | Classificazione attività economiche 2007 aggiorn. 2022 |
| **Banche** | Lookup ABI/BIC, validazione BIC italiano, 1.600+ banche |
| **Zone Territoriali** | Zona sismica (PCM 3274/2003), coordinate WGS84 |
| **CAP** | Multi-CAP per comune, ricerca inversa |
| **Pubblica Amministrazione** | Codici IPA/SdI, ASL, aggregazioni sovracomunali |
| **Festività** | Nazionali + patrono locale per ogni comune |
| **Indirizzi** | Parser, normalizzazione ANPR, confronto intelligente |
| **Telefonia** | Lookup prefissi, operatori mobili, validazione |
| **Frontalieri** | Zone frontaliere, regime fiscale Svizzera/UE |
| **Validazioni** | P.IVA (Luhn), IBAN (MOD97), Targa, CF, cross-check |

---

## Utilizzo

### Comuni e Ricerca

```csharp
using Italy.Core;

var atlante = new Atlante();

// Fuzzy matching (Levenshtein) — zero config
var comuni = atlante.Comuni.Cerca("Mialno");        // → [Milano (MI)]
var comuni2 = atlante.Comuni.Cerca("Corigliaho");   // → [Corigliano-Rossano (CS)]

// Lookup diretto
var milano = atlante.Comuni.DaCodiceBelfiore("F205");
var roma   = atlante.Comuni.TrovaDaCodiceISTAT("058091");

// Gerarchia
var comuniMI  = atlante.Comuni.DaProvincia("MI");
var comuniLOM = atlante.Comuni.DaRegione("Lombardia");
var nord      = atlante.Comuni.DaRipartizione(RipartizioneGeografica.NordOvest);

// Inclusi i soppressi
var tutti = atlante.Comuni.TuttiInclusiSoppressi();
```

### Codici ATECO 2007

```csharp
// Lookup diretto per codice
var classe = atlante.ATECO.DaCodice("10.11");
// → { Codice: "10.11", Descrizione: "Produzione di carne...", Livello: "Classe", CodicePadre: "10.1" }

// Ricerca per testo
var risultati = atlante.ATECO.Cerca("software");
// → tutti i codici con "software" nella descrizione

// Navigazione gerarchica
var sezioni    = atlante.ATECO.Sezioni();         // A, B, C, ..., U
var divisioni  = atlante.ATECO.SottoCategorie("C");  // 10, 11, 12, ...
var gruppi     = atlante.ATECO.SottoCategorie("10");  // 10.1, 10.2, ...

// Catena completa
string path = atlante.ATECO.DescrizioneCompleta("10.11");
// → "C > 10 > 10.1 > 10.11"
```

### Banche (ABI / BIC)

```csharp
// Lookup per BIC (8 o 11 caratteri)
var banca = atlante.Banche.DaBIC("BCITITMM");
// → { NomeBanca: "...", CodiceABI: "...", CodiceBIC: "BCITITMM" }

// Normalizzazione automatica BIC11 → BIC8
var stessa = atlante.Banche.DaBIC("BCITITMMXXX"); // stesso risultato

// Lookup per ABI
var b2 = atlante.Banche.DaABI("03069");

// Ricerca per nome
var risultati = atlante.Banche.Cerca("intesa");
// → lista banche con "intesa" nel nome

// Validazione formato BIC italiano (paese = "IT")
bool valido   = atlante.Banche.ValidaBIC("BCITITMM");    // → true
bool valido11 = atlante.Banche.ValidaBIC("BCITITMMXXX"); // → true (BIC11 con filiale)
bool nonIT    = atlante.Banche.ValidaBIC("DEUTDEDB");    // → false (tedesco)
```

### Zone Territoriali

```csharp
// Classificazione sismica e coordinate per comune
var zone = atlante.ZoneTerritoriali.OttieniZone("F205");
// → { CodiceBelfiore: "F205",
//     ZonaSismica: Zona3,          // PCM 3274/2003
//     ZonaClimatica: E,            // DPR 412/1993
//     Latitudine: 45.46,
//     Longitudine: 9.19 }

// Lista comuni per zona sismica
var zona1 = atlante.ZoneTerritoriali.ComuniPerZonaSismica(1); // alta sismicità
var zona4 = atlante.ZoneTerritoriali.ComuniPerZonaSismica(4); // bassa sismicità

// Lista comuni per zona climatica
var zonaE = atlante.ZoneTerritoriali.ComuniPerZonaClimatica("E"); // più comune in Italia

// Zona non valida → ArgumentException
atlante.ZoneTerritoriali.ComuniPerZonaSismica(5); // → ArgumentException
```

### Regioni e Province

```csharp
// Tutte le 20 regioni
var regioni = atlante.Regioni.TutteLeRegioni();
// → [{ Nome: "Abruzzo", CodiceNUTS2: "ITF1", NumeroProvince: 4, NumeroComuni: 305 }, ...]

// Lookup per nome
var lom = atlante.Regioni.DaNome("Lombardia");
// → { Nome: "Lombardia", CodiceNUTS2: "ITC4", NumeroProvince: 12, NumeroComuni: 1516 }

// Lookup per codice NUTS2 (standard EU)
var piemonte = atlante.Regioni.DaCodiceNUTS2("ITC1");

// Tutte le province
var province = atlante.Regioni.TutteLeProvince();

// Lookup per sigla
var mi = atlante.Regioni.DaSigla("MI");
// → { Sigla: "MI", Nome: "Milano", NomeRegione: "Lombardia", CodiceNUTS3: "ITC4C" }

// Province di una regione
var provLomb = atlante.Regioni.DaRegione("Lombardia");
```

### Distanza tra Comuni e Codici NUTS EU

```csharp
// Distanza in km (formula Haversine, coordinate WGS84)
double? km = atlante.GeoDistanza.DistanzaKm("F205", "H501"); // Milano → Roma ≈ 479 km

// Comuni nel raggio (ordinati per distanza)
var vicini = atlante.GeoDistanza.ComuniNelRaggio("F205", raggioKm: 20);
// → [{ CodiceBelfiore: "...", Denominazione: "Sesto S.G.", DistanzaKm: 8.3 }, ...]

// Codici NUTS EU di un comune
var nuts = atlante.GeoDistanza.OttieniNUTS("F205");
// → { NUTS1: "ITC", NUTS2: "ITC4", NUTS3: "ITC4C" }

// Tutti i comuni di una provincia EU (NUTS3)
var comuniProv = atlante.GeoDistanza.ComuniPerNUTS3("ITC4C"); // Milano

// Tutti i comuni di una regione EU (NUTS2)
var comuniReg = atlante.GeoDistanza.ComuniPerNUTS2("ITC4"); // Lombardia
```

### Risoluzione Storica e Codice ISTAT Vecchio

```csharp
// Comune soppresso → successore attivo
var successore = atlante.Comuni.OttieniSuccessore("C619");
// → Corigliano-Rossano (CS)

// Codice ISTAT vecchio → risoluzione completa
var r = atlante.Comuni.RisolviCodiceISTATStorico("076020");
Console.WriteLine(r.Messaggio);
// → "Comune 'Corigliano Calabro' soppresso il 01/11/2018.
//    Successore attuale: Corigliano-Rossano (CS)"

// Storico completo
var storia = atlante.Comuni.OttieniStorico("C619");
// → [CambioDenominazione 1811, Fusione 2018, ...]
```

### Time Machine Deterministica

```csharp
// "Esisteva Corigliano come comune autonomo nel 1980?"
bool esisteva = atlante.TimeMachine.EsistevaInData("Corigliano", new DateTime(1980, 1, 1));

// Snapshot completo in una data
var snap = atlante.TimeMachine.OttieniSnapshotInData("C619", new DateTime(1980, 1, 1));

// Codice Belfiore per CF di persona nata in comune soppresso
var belfiore = atlante.TimeMachine.OttieniBelfiorePerCF("Corigliano Calabro", new DateTime(1980, 1, 1));
// → { CodiceBelfiore: "C619", Valido: true }
```

### Codice Fiscale

```csharp
// Validazione con lookup comune
var cf = atlante.Fiscale.Valida("RSSMRA80A01F205X");
// → { IsValido: true, ComuneNascita: "Milano", DataNascita: 1980-01-01, Sesso: 'M' }

// Calcolo
var calcolato = atlante.Fiscale.Calcola("Rossi", "Mario", new DateTime(1980,1,1), 'M', "F205");

// Zero-allocation (hot path, migliaia/secondo)
bool valido = ValidatoreCFSpan.IsValido("RSSMRA80A01F205X"); // no heap alloc

// CF Persona Giuridica
var cfPG = atlante.FiscalePG.Valida("06655971007");
// → { IsValido: true, FormatoCF: NumericoUndiciFigure }
```

### Pubblica Amministrazione e Fatturazione Elettronica

```csharp
// Codice IPA/SdI per fattura alla PA
var ipa = atlante.PA.OttieniCodiceIPA("F205");
// → { NomeEnte: "Comune di Milano", CodiceIPAUnivoco: "c_f205", CodiceSdI: "UFOVS8" }

// Cerca ente PA per nome
var enti = atlante.PA.CercaEntePA("ASL Milano");

// ASL e aggregazioni territoriali
var (codASL, nomeASL) = atlante.PA.OttieniASL("F205");
var agg = atlante.PA.OttieniAggregazioni("F205");
// → { NomeASL: "ATS Città Metropolitana di Milano",
//     ATORifiuti: "ATO Città Metropolitana Milano",
//     TribunaleCompetente: "Tribunale di Milano" }

// Sedi previdenziali di competenza
var sedeINPS  = atlante.PA.OttieniSedeINPS("F205");
var sedeINAIL = atlante.PA.OttieniSedeINAIL("F205");
```

### CAP

```csharp
var zone = atlante.CAP.OttieniZone("F205"); // multi-CAP Milano → [20100..20162]
var comuni = atlante.CAP.DaCAP("17025");    // ricerca inversa → [Loano]
var storici = atlante.CAP.CAPStorici("F205"); // inclusi CAP disattivati
```

### Festività e Calendario

```csharp
var festività = atlante.Calendario.OttieniFestività(2024, "F205"); // + Sant'Ambrogio (7/12)
bool festivo  = atlante.Calendario.IsFestivo(new DateTime(2024, 12, 7), "F205"); // → true
int giorni    = atlante.Calendario.CalcolaGiorniLavorativi(
    new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)); // → ~251
DateTime pasqua = atlante.Calendario.CalcolaPasqua(2025); // → 20 aprile 2025
```

### Parser Indirizzi

```csharp
var addr = atlante.Parser.Analizza("P.ZZA GARIBALDI 5/A - SAVONA 17100");
// → { Toponimo: "PIAZZA", NomeVia: "Garibaldi", Civico: "5/A",
//     CAP: "17100", ComuneRisolto: Savona (SV), ScoreQualità: 0.95 }

// Normalizza per ANPR
string anpr = atlante.Parser.NormalizzaPerANPR("via roma 10, 17025 loano sv");
// → "VIA Roma, 10, 17025 Loano (SV)"

// Confronto intelligente tra due indirizzi
var esito = atlante.Confronto.Confronta("Via Roma 10, Loano SV", "V. Roma, 10 - Loano");
// → { Esito: Uguale, PercentualeTotale: 97.3 }
```

### Validazioni

```csharp
var piva  = atlante.Validazione.ValidaPartitaIVA("06655971007"); // Luhn
var iban  = atlante.Validazione.ValidaIBAN("IT60X0542811101000000123456"); // MOD97
var targa = atlante.Validazione.ValidaTarga("AB123CD"); // formato attuale/storico/speciale
```

### Bonifica Dati Legacy

```csharp
var r = atlante.Bonifica.AnalizzaComune("Corigliano Calabro", "CS");
// → { RequiereCorrezione: true, Tipo: ComuneFuso,
//     ValoreSuggerito: "Corigliano-Rossano", Confidenza: 1.0 }

var report = atlante.Bonifica.ElaboraBatch(records);
Console.WriteLine($"Pulizia: {report.PercentualePulizia:F1}% | Anomalie: {report.RecordConAnomalie}");
```

---

## Dependency Injection (ASP.NET Core)

```csharp
// Program.cs
builder.Services.AddItalyCore();

// Con provider personalizzati
builder.Services.AddItalyCore(opts =>
{
    opts.UsaProviderFestività<MioProviderFestività>();
    opts.UsaProviderGeografico<MioProviderGeo>();
});
```

## Data Annotations

```csharp
public class Anagrafica
{
    [CodiceFiscaleValido]
    public string CodiceFiscale { get; set; }

    [CAPValido]
    public string CAP { get; set; }

    [CodiceBelfioreValido]
    public string ComuneNascita { get; set; }

    [PartitaIVAValida]
    public string? PartitaIVA { get; set; }

    [IBANValido]
    public string? IBAN { get; set; }
}
```

---

## Compatibilità

| Framework | Stato | Note |
|---|---|---|
| .NET 8.0+ | ✅ Completo | Span, Native AOT |
| .NET Framework 4.8 | ✅ Completo | Tutte le funzionalità, shim automatici |
| Blazor WASM | ⚠️ Parziale | SQLite non supportato |
| Unity | ❌ | SQLite non compatibile |

> **Nota net48**: la libreria include shim automatici (PolySharp, `IsExternalInit`, `GetValueOrDefault`) per garantire compatibilità completa con .NET Framework 4.8 senza modifiche al codice client.

---

## Performance

| Operazione | Target | Tecnica |
|---|---|---|
| Cold Start | < 100ms | Estrazione lazy DB embedded |
| Ricerca fuzzy comuni | < 10ms | FTS5 SQLite + Levenshtein in-memory |
| Validazione CF (hot path) | < 1μs | `Span<char>`, zero heap allocation |
| Lookup ATECO/Banche | < 5ms | B-Tree index SQLite |
| DB Read-Only | thread-safe | `Mode=ReadOnly;Immutable=true` |

---

## Pipeline Aggiornamento Dati

```
1° del mese
     │
     ▼
[GitHub Actions: build_atlante.py]
     │
     ├─ Download ISTAT CSV (comuni, variazioni 1991-oggi)
     ├─ Download GeoNames IT (CAP, coordinate WGS84)
     ├─ Download Zone Sismiche (PCM 3274/2003)
     ├─ Download GLEIF BIC-LEI (banche italiane)
     ├─ Download ATECO 2007 aggiorn. 2022 (ISTAT XLSX)
     ├─ Download IndicePA (enti PA, codici SdI)
     ├─ Generazione italy.db (SQLite, FTS5, ~8 MB)
     │
     ▼
[Test Obbligatori]
     │
     ├─ TestIntegritàStorica (comuni soppressi → successori)
     ├─ Test suite completa (89 test, copertura >= 85%)
     │
     ▼
[Release Automatica]
     │
     ├─ Bump versione patch
     ├─ Commit + Tag v1.x.y
     ├─ Pubblica su NuGet.org
     └─ Crea GitHub Release con CHANGELOG
```

**Fonti dati con tracciabilità:**

| Fonte | Dato | Aggiornamento |
|---|---|---|
| ISTAT Open Data | Comuni, variazioni storiche, ATECO 2007 | Mensile |
| GeoNames | CAP, coordinate WGS84 | Mensile |
| GLEIF | BIC/LEI banche italiane | Mensile |
| IndicePA (IPA) | Enti PA, codici SdI per fatturazione B2G | Mensile |
| Protezione Civile / PCM | Classificazione sismica comuni | Annuale |

---

## Struttura Repository

```
Italy.Core/
├── .github/workflows/
│   ├── ci.yml              ← build + test (Linux + Windows)
│   └── update-data.yml    ← 1° del mese: scarica dati → NuGet
├── tools/
│   ├── build_atlante.py   ← script Python: CSV/XLSX → SQLite
│   └── requirements.txt
├── src/
│   └── Italy.Core/
│       ├── Domain/              ← entità immutabili
│       ├── Applicazione/Servizi/ ← ServiziComuni, ServiziAteco,
│       │                           ServiziBanche, ServiziZoneTerritoriali,
│       │                           ServiziPA, ServiziCAP, ecc.
│       ├── Infrastruttura/      ← SQLite, repository, DI, shim net48
│       ├── Validazione/         ← DataAnnotation attributes
│       └── data/italy.db       ← risorsa embedded (~8 MB)
└── tests/
    └── Italy.Core.Tests/
        ├── TestAteco.cs
        ├── TestBanche.cs
        ├── TestZoneTerritoriali.cs
        ├── TestCodiceFiscale.cs
        ├── TestFestività.cs
        ├── TestValidazione.cs
        ├── TestGeo.cs
        ├── TestParserIndirizzi.cs
        ├── TestBonifica.cs
        └── TestIntegritàStorica.cs  ← obbligatorio in ogni build
```

---

## Licenza

MIT — Vedi [LICENSE](LICENSE)

*Dati: ISTAT, GeoNames, GLEIF, IndicePA, Protezione Civile*
*Aggiornamento automatico mensile — Data Pack corrente: 2026.03*
