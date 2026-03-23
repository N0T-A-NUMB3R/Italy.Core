# Italy.Core

[![NuGet](https://img.shields.io/nuget/v/Italy.Core.svg)](https://www.nuget.org/packages/Italy.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Data Pack](https://img.shields.io/badge/Data_Pack-2026.03-green.svg)](#pipeline-aggiornamento-dati)
[![Versione](https://img.shields.io/badge/versione-1.0.4-blue.svg)](https://www.nuget.org/packages/Italy.Core)

> **Il coltellino svizzero C# per i dati amministrativi italiani.**
> Auto-aggiornato mensilmente da ISTAT, IndicePA, GeoNames, ISPRA, MIMIT e Wikidata. Pubblicato automaticamente su NuGet.

## Perchè questa follia?
>Tutto è iniziato anni fa, quando, da sviluppatore full-time, ho iniziato a strutturare le prime tabelle dell'Atlante per gestire CAP, comuni e prefissi senza doverli replicare in ogni nuovo progetto. Tuttavia, il lavoro è rimasto a lungo un cantiere aperto.
>La svolta è arrivata su due fronti: da un lato, le continue richieste del mio caro collega Giorgio, che spesso si affidava a me per risolvere le complessità dei codici ISTAT, mi hanno dato l'input necessario per trasformare quelle mie vecchie intuizioni in una struttura solida e riutilizzabile. Dall'altro, sono state le notti insonni passate a prendermi cura di mia figlia appena nata a regalarmi il tempo (e la determinazione) per dare a questa libreria la sua forma definitiva.
>Il mio obiettivo è stato proprio questo: trasformare anni di esperienza (e imprecazioni) in uno strumento per semplificare la vita a ogni sviluppatore che, come noi, si scontra ogni giorno con la complessità del panorama amministrativo italiano


![LogoItalyCore](https://github.com/user-attachments/assets/fcc7a302-6735-43af-8415-1a8805f6602b)


---

## Installazione

```shell
dotnet add package Italy.Core
```

**Una sola libreria, nessuna dipendenza aggiuntiva.** Tutti i dati sono embedded nel pacchetto NuGet.

Pacchetto NuGet: [nuget.org/packages/Italy.Core](https://www.nuget.org/packages/Italy.Core)

---

## Funzionalità Principali

| Modulo | Funzionalità |
|---|---|
| **Comuni** | Fuzzy search, lookup Belfiore/ISTAT, gerarchia, 7.800+ comuni |
| **Storico** | Time Machine, variazioni 1991-oggi, comuni soppressi/fusi |
| **Codice Fiscale** | Validazione, calcolo (con Belfiore o nome comune+provincia), scomposizione segmenti, zero-allocation |
| **ATECO** | Classificazione attività economiche 2007 aggiorn. 2022 |
| **Banche** | Lookup ABI/BIC, validazione BIC italiano, 1.600+ banche |
| **Zone Territoriali** | Zona sismica (PCM 3274/2003), zona climatica (DPR 412/93) con gradi-giorno ufficiali, zona altimetrica ISTAT, proiezioni Gauss-Boaga/UTM |
| **Aree Interne ISTAT** | Classificazione Aree Interne 2021-2027 per comune (Centro, Cintura, Intermedio, Periferico, Ultraperiferico) |
| **NUTS EU** | Codici NUTS1/2/3 per ogni comune (standard Eurostat), lookup e filtro per area |
| **Dati Geografici Comuni** | Superficie km², altitudine del centro, coordinate WGS84, densità abitativa |
| **CAP** | Multi-CAP per comune (11.269 associazioni), ricerca inversa |
| **Pubblica Amministrazione** | Codici IPA/SdI (7.649 comuni), ASL competente, CCIAA competente (7.803 comuni), Tribunale ordinario (7.803 comuni), aggregazioni sovracomunali, ATO rifiuti/acqua, INPS/INAIL, comunità montana |
| **PEC Comuni** | Indirizzo PEC istituzionale per 7.520 comuni (fonte: IndicePA/AgID) |
| **Festività** | Nazionali + patrono locale per 6.052+ comuni con data festa (fonte: santiebeati.it + Wikidata) |
| **Dati Demografici** | Serie storica popolazione 1971-2023 per 7.621 comuni (15.029 record, fonte: Wikidata) |
| **Gestione Rifiuti** | % raccolta differenziata, kg/ab, tonnellate totali/indifferenziato/RD e composizione merceologica (umido, carta, vetro, plastica, verde, legno, metallo, RAEE) per 7.802 comuni (fonte: ISPRA Catasto Rifiuti 2024) |
| **Farmacie** *(opzionale)* | 20.750 farmacie attive con coordinate, indirizzo, tipologia (fonte: Ministero della Salute) — abilitare con `--include-poi` |
| **Impianti Carburante** *(opzionale)* | 23.574 distributori attivi con coordinate e bandiera (fonte: MIMIT) — abilitare con `--include-poi` |
| **Indirizzi** | Parser, normalizzazione ANPR, confronto intelligente |
| **Telefonia** | Prefissi geografici per tutti i 106 comuni italiani, lookup operatori mobili, validazione |
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
// Classificazione sismica, climatica e altimetrica per comune
var zone = atlante.ZoneTerritoriali.OttieniZone("F205");
// → { CodiceBelfiore: "F205",
//     ZonaSismica: Zona3,            // PCM 3274/2003
//     ZonaClimatica: E,              // DPR 412/1993
//     ZonaAltimetrica: Pianura,      // ISTAT
//     Latitudine: 45.46,
//     Longitudine: 9.19 }

// Lista comuni per zona sismica
var zona1 = atlante.ZoneTerritoriali.ComuniPerZonaSismica(1); // alta sismicità
var zona4 = atlante.ZoneTerritoriali.ComuniPerZonaSismica(4); // bassa sismicità

// Lista comuni per zona climatica
var zonaE = atlante.ZoneTerritoriali.ComuniPerZonaClimatica("E"); // più comune in Italia

// Lista comuni per zona altimetrica ISTAT
var pianura   = atlante.ZoneTerritoriali.ComuniPerZonaAltimetrica(ZonaAltimetrica.Pianura);
var montagna  = atlante.ZoneTerritoriali.ComuniPerZonaAltimetrica(ZonaAltimetrica.MontagnaInterna);
var collinaCosta = atlante.ZoneTerritoriali.ComuniPerZonaAltimetrica(ZonaAltimetrica.CollinaLitoranea);

// Zona altimetrica accessibile anche da Comune
var milano = atlante.Comuni.DaCodiceBelfiore("F205");
milano.ZonaAltimetrica;  // → ZonaAltimetrica.Pianura
```

### Proiezioni Cartografiche

```csharp
// WGS84 → Gauss-Boaga (datum Roma40, sistema catasto italiano)
var gb = atlante.Geo.ConvertInGaussBoaga(45.4642, 9.1900);
// → (Est: 1518296.123, Nord: 5034946.512, Fuso: "Ovest")  // EPSG:3003

// Direttamente da codice Belfiore
var gbMilano = atlante.Geo.ConvertComuneInGaussBoaga("F205");
// → (Est: 1518296.123, Nord: 5034946.512, Fuso: "Ovest")

// WGS84 → UTM (WGS84, fuso automatico 32N/33N)
var utm = atlante.Geo.ConvertInUTM(45.4642, 9.1900);
// → (Fuso: 32, Est: 514924.830, Nord: 5034993.210)  // EPSG:32632

var utmRoma = atlante.Geo.ConvertComuneInUTM("H501");
// → (Fuso: 33, Est: 291389.472, Nord: 4640712.331)  // EPSG:32633
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

// Calcolo con codice Belfiore
var calcolato = atlante.Fiscale.Calcola("Rossi", "Mario", new DateTime(1980,1,1), 'M', "F205");

// Calcolo con nome comune + provincia (risolve automaticamente il Belfiore)
var calcolato2 = atlante.Fiscale.Calcola("Rossi", "Mario", new DateTime(1980,1,1), 'M', "Milano", "MI");

// Scomposizione strutturata (analisi segmenti, no lookup DB)
var s = atlante.Fiscale.Scomponi("RSSMRA80A01F205X");
// → s.SegmentoCognome    = "RSS"   (pos 0-2)
// → s.SegmentoNome       = "MRA"   (pos 3-5)
// → s.AnnoEncoded        = "80"    (pos 6-7)
// → s.MeseEncoded        = 'A'     (pos 8, = Gennaio)
// → s.GiornoEncoded      = "01"    (pos 9-10)
// → s.CodiceBelfiore     = "F205"  (pos 11-14)
// → s.CarattereControllo = 'X'     (pos 15)
// → s.DataNascita        = 1980-01-01
// → s.Sesso              = 'M'

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

// ASL e aggregazioni sovracomunali
var (codASL, nomeASL) = atlante.PA.OttieniASL("F205");
var agg = atlante.PA.OttieniAggregazioni("F205");
// → { NomeASL: "ATS Città Metropolitana di Milano",   ← ASL di competenza (join su provincia)
//     ComunityMontana: null,                           ← comunità montana (null se non montano)
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

### PEC e Santo Patrono

```csharp
var milano = atlante.Comuni.DaCodiceBelfiore("F205");

// PEC istituzionale (IndicePA/AgID — 7.658 comuni)
milano.PEC;  // "protocollo@pec.comune.milano.it"

// Santo patrono locale (6.052+ comuni — santiebeati.it + Wikidata)
milano.SantoPatrono;   // "S. Ambrogio di Milano"
milano.PatronoGiorno;  // 7
milano.PatronoMese;    // 12

// Via Calendario: ottieni la festività del patrono nell'anno corrente
var festività = atlante.Calendario.OttieniFestività(2025, "F205");
var patrono = festività.First(f => f.Tipo == TipoFestività.SantoPatrono);
// → { Nome: "S. Ambrogio di Milano", Data: 07/12/2025 }
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


> **Nota net48**: la libreria include shim automatici (PolySharp, `IsExternalInit`, `GetValueOrDefault`) per garantire compatibilità completa con .NET Framework 4.8 senza modifiche al codice client.

---

## Caricamento Database

Il database embedded viene caricato con una strategia a cascata:

```
Avvio Atlante
     │
     ▼
Strategia 1 — File su disco
  %TEMP%\ItalyCore\italy_<version>_<hash>.db
  Riutilizzato tra avvii (hash MD5 garantisce coerenza)
     │
     │  Fallisce? (permessi, container read-only, disco pieno)
     ▼
Strategia 2 — In-Memory (SQLite Backup API)
  Data Source=:memory: condivisa tra connessioni
  Trasparente per il chiamante — stessa API, stesse performance
  Il DB vive in memoria per tutta la durata del processo
```

> **Casi d'uso del fallback**: applicazioni enterprise con policy che vietano scrittura in `%TEMP%`, kiosk e terminali con filesystem read-only, app packaged (MSIX) con sandbox restrittiva.

## Performance

| Operazione | Target | Tecnica |
|---|---|---|
| Cold Start (disco) | < 100ms | Estrazione lazy DB embedded |
| Cold Start (in-memory) | < 500ms | SQLite Backup API da byte array |
| Ricerca fuzzy comuni | < 10ms | FTS5 SQLite + Levenshtein in-memory |
| Validazione CF (hot path) | < 1μs | `Span<char>`, zero heap allocation |
| Lookup ATECO/Banche | < 5ms | B-Tree index SQLite |
| DB Read-Only | thread-safe | `Mode=ReadOnly` / shared memory cache |

---

## Pipeline Aggiornamento Dati

```
1° del mese
     │
     ▼
[GitHub Actions: build_atlante.py]
     │
     ├─ Download ISTAT CSV (comuni, variazioni 1991-oggi)
     ├─ Download GeoNames IT (CAP multipli, coordinate WGS84)
     ├─ Download Zone Sismiche (PCM 3274/2003)
     ├─ Download Zone Climatiche + Gradi-Giorno (ENEA SolarItaly, DPR 412/93)
     ├─ Download ATECO 2007 aggiorn. 2022 (ISTAT XLSX)
     ├─ Download IndicePA enti.xlsx + ou.txt (enti PA, codici SdI 7.649 comuni)
     ├─ Download ISPRA Catasto Rifiuti (% RD, frazioni merceologiche per comune)
     ├─ Wikidata SPARQL: santi patroni 6.052+ comuni + dati demografici 1971-2023
     ├─ Mapping normativi: CCIAA (DL 219/2016) + Tribunali (D.Lgs. 155/2012)
     ├─ [--include-poi] Download MIMIT impianti carburante (mensile)
     ├─ [--include-poi] Download Ministero Salute farmacie (settimanale)
     ├─ Carica tools/pec_comuni.json  (7.520 PEC, aggiorn. periodica)
     ├─ Carica tools/patroni.json     (santi patroni statico, integrato da Wikidata)
     ├─ Carica XLSX manuali (aree interne, comuni_geo, popolazione, ASL)
     ├─ Generazione italy.db (SQLite, FTS5, ~10 MB; ~15 MB con --include-poi)
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

**Aggiornamento dati hardcoded (manuale, periodico):**

| File | Dato | Come aggiornare |
|---|---|---|
| `tools/pec_comuni.json` | PEC istituzionale 7.658 comuni | Scarica nuovo XLSX da IndicePA → `python tools/build_atlante.py` |
| `tools/patroni.json` | Santi patroni da santiebeati.it (integrazione base) | `python tools/scrape_patroni.py` → rebuild (Wikidata arricchisce automaticamente) |

**Fonti dati con tracciabilità:**

| Fonte | Dato | Aggiornamento |
|---|---|---|
| ISTAT Open Data | Comuni, variazioni storiche 1991-oggi, ATECO 2007 agg. 2022 | Mensile (automatico) |
| GeoNames | CAP multipli (11.269), coordinate WGS84 | Mensile (automatico) |
| IndicePA (IPA) enti.xlsx + ou.txt | Enti PA, codici SdI 7.649 comuni, PEC 7.520 comuni | Mensile (automatico) |
| Protezione Civile / PCM | Classificazione sismica comuni (PCM 3274/2003) | Annuale |
| ENEA SolarItaly | Zone climatiche (DPR 412/93) + gradi-giorno ufficiali, 7.803 comuni | Stabile (DPR) |
| ISTAT Aree Interne | Classificazione Aree Interne 2021-2027 per comune | Programmazione EU |
| ISTAT Comuni Geo | Superficie km² per comune | Decennale (censimento) |
| ISTAT Popolazione | Popolazione per comune 2024 | Annuale |
| Ministero della Salute | ASL di competenza (join su provincia, 91 province) | Annuale |
| Wikidata SPARQL | Santi patroni + data festa per 6.052+ comuni | Mensile (automatico) |
| Wikidata SPARQL | Serie storica demografica 1971-2023, 15.029 record | Mensile (automatico) |
| Mapping DL 219/2016 | CCIAA competente per 7.803 comuni (tutte le 105 province) | Stabile (normativo) |
| Mapping D.Lgs. 155/2012 | Tribunale ordinario per 7.803 comuni | Stabile (normativo) |
| AGCOM / Piano Numeri | Prefissi geografici per provincia (106 province) | Stabile |
| santiebeati.it | Santi patroni base (integrazione tramite `scrape_patroni.py`) | Manuale |
| ISPRA Catasto Rifiuti | % RD, kg/ab, tonnellate totali e 8 frazioni merceologiche per 7.802 comuni (2024) | Annuale (automatico) |
| Ministero della Salute *(opzionale)* | 20.750 farmacie attive con coordinate, indirizzo, tipologia | Settimanale (`--include-poi`) |
| MIMIT *(opzionale)* | 23.574 impianti carburante attivi con coordinate e bandiera | Mensile (`--include-poi`) |



```
Italy.Core/
├── .github/workflows/
│   ├── ci.yml              ← build + test (Linux + Windows)
│   └── update-data.yml    ← 1° del mese: scarica dati → NuGet
├── tools/
│   ├── build_atlante.py   ← script Python: CSV/XLSX → SQLite
│   └── requirements.txt
├── src/
│   ├── Italy.Core/
│   │   ├── Domain/              ← entità immutabili
│   │   ├── Applicazione/Servizi/ ← ServiziComuni, ServiziAteco,
│   │   │                           ServiziBanche, ServiziZoneTerritoriali,
│   │   │                           ServiziPA, ServiziCAP, ecc.
│   │   ├── Infrastruttura/      ← SQLite, repository, DI, shim net48
│   │   ├── Validazione/         ← DataAnnotation attributes
│   │   └── data/italy.db       ← risorsa embedded (~8 MB)
└── tests/
    ├── Italy.Core.Tests/
    │   ├── TestAteco.cs
    │   ├── TestBanche.cs
    │   ├── TestZoneTerritoriali.cs
    │   ├── TestCodiceFiscale.cs
    │   ├── TestFestività.cs
    │   ├── TestValidazione.cs
    │   ├── TestGeo.cs
    │   ├── TestParserIndirizzi.cs
    │   ├── TestBonifica.cs
    │   └── TestIntegritàStorica.cs  ← obbligatorio in ogni build
    └── Italy.Core.Extensions.Tests/
        ├── TestServiziPA.cs         ← test integrazione API IndicePA
        └── TestServiziISTAT.cs      ← test integrazione API ISTAT
```

---

## Roadmap

### Italy.Core — dati mancanti

- [x] Prefissi geografici per provincia (106 province, join su `sigla_provincia`) — **completato 2026.03**
- [x] Aree Interne ISTAT 2021-2027 per comune (A/B/C/D/E) — **completato 2026.03**
- [x] Superficie km² per comune (ISTAT Comuni Geo) — **completato 2026.03**
- [x] Popolazione per comune 2024 (ISTAT, Mondo totale) — **completato 2026.03**
- [x] ASL di competenza per comune (join su provincia, 91 province, Ministero Salute 2023) — **completato 2026.03**
- [x] PEC istituzionale per 7.520 comuni (IndicePA/AgID) — **completato 2026.03**
- [x] Santo patrono locale per 6.052+ comuni (santiebeati.it + Wikidata) — **completato 2026.03**
- [x] Zona climatica per tutti i comuni + gradi-giorno ufficiali DPR 412/93 (ENEA) — **completato 2026.03**
- [x] Popolazione storica 1971-2023 per 7.621 comuni (Wikidata, 15.029 record) — **completato 2026.03**
- [x] CCIAA competente per 7.803 comuni (mapping DL 219/2016) — **completato 2026.03**
- [x] Tribunale ordinario per 7.803 comuni (mapping D.Lgs. 155/2012) — **completato 2026.03**
- [x] Codice SDI/IPA per 7.649 comuni (IPA ou.txt) — **completato 2026.03**
- [x] Farmacie attive con coordinate e tipologia (Ministero Salute) — **completato 2026.03** *(opzionale: `--include-poi`)*
- [x] Impianti carburante con coordinate e bandiera (MIMIT) — **completato 2026.03** *(opzionale: `--include-poi`)*
- [x] Rifiuti urbani 2024: % RD, kg/ab, tonnellate totali e 8 frazioni merceologiche per 7.802 comuni (ISPRA) — **completato 2026.03**
- [ ] INPS/INAIL sede per comune (open data previdenziali — nessuna fonte open con codice ISTAT)
- [ ] Comunità montane complete (mappatura province/regioni)



## Licenza

MIT — Vedi [LICENSE](LICENSE)

*Dati: ISTAT, GeoNames, IndicePA, Protezione Civile, ISPRA, Ministero della Salute, MIMIT, ENEA, Wikidata*
*Aggiornamento automatico mensile — Data Pack corrente: 2026.03*
