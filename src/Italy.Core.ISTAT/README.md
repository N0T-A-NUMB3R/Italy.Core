# Italy.Core.ISTAT

> Estensione di [Italy.Core](https://github.com/YOUR_ORG/Italy.Core) per i dati statistici ufficiali ISTAT.

[![NuGet](https://img.shields.io/nuget/v/Italy.Core.ISTAT)](https://www.nuget.org/packages/Italy.Core.ISTAT)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%20Framework%204.8-blueviolet)](https://dotnet.microsoft.com)
[![Licenza](https://img.shields.io/badge/licenza-MIT-green)](LICENSE)

---

## Panoramica

**Italy.Core.ISTAT** aggiunge a Italy.Core l'accesso in tempo reale ai dati statistici
ufficiali dell'Istituto Nazionale di Statistica tramite l'**API SDMX pubblica**.

> **Nessuna API key richiesta.** L'API ISTAT è completamente pubblica e gratuita.
> Fonte: `sdmx.istat.it/SDMXWS/rest/` (standard SDMX 2.1, formato JSON).

A differenza di Italy.Core — che usa dati embedded in SQLite — questo modulo
esegue **chiamate HTTP** verso i server ISTAT per garantire dati sempre aggiornati.
Per questo motivo è un pacchetto separato: chi non ha bisogno di dati in tempo reale
non porta il peso della dipendenza HTTP.

```
Italy.Core              ←  dati embedded (7.800 comuni, province, CF, IBAN...)
    └── Italy.Core.ISTAT  ←  dati live via API SDMX (popolazione, inflazione, PIL...)
```

---

## Installazione

```bash
dotnet add package Italy.Core.ISTAT
```

Richiede `Italy.Core` come dipendenza (installata automaticamente da NuGet).

---

## Avvio rapido

```csharp
var atlante = new Atlante();
var istat   = new ServiziISTAT(atlante.Comuni);

// Popolazione di Milano per codice ISTAT
var pop = await istat.GetPopolazioneAsync("015146");
Console.WriteLine(pop.Totale);    // 1.352.000
Console.WriteLine(pop.Maschi);    // 641.000
Console.WriteLine(pop.Anno);      // 2023

// Oppure direttamente da codice Belfiore (Italy.Core lo risolve)
var pop2 = await istat.GetPopolazioneDaBelfioreAsync("F205");  // Milano
```

### Con ASP.NET Core e IHttpClientFactory

```csharp
// Program.cs
builder.Services.AddItalyCore();
builder.Services.AddHttpClient<ServiziISTAT>();
builder.Services.AddScoped(sp =>
    new ServiziISTAT(
        sp.GetRequiredService<Atlante>().Comuni,
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ServiziISTAT))));
```

---

## Dataset disponibili

### Popolazione per comune

```csharp
var pop = await istat.GetPopolazioneAsync("015146");  // Milano
pop.CodiceISTAT;  // "015146"
pop.Anno;         // 2023
pop.Totale;       // 1.352.000
pop.Maschi;       // 641.000
pop.Femmine;      // 711.000

// Da codice Belfiore (Italy.Core risolve il codice ISTAT automaticamente)
var pop = await istat.GetPopolazioneDaBelfioreAsync("F205");
```

Dataset ISTAT: `DCIS_POPRES1` — Popolazione residente al 1° gennaio.

---

### Natalità e mortalità per regione

```csharp
var nm = await istat.GetNatalitaMortalitaAsync("03");  // Lombardia
nm.CodiceRegioneISTAT;  // "03"
nm.Anno;                // 2023
nm.TassoNatalita;       // 6.8  (nati vivi per 1.000 abitanti)
nm.TassoMortalita;      // 11.2 (morti per 1.000 abitanti)
```

Dataset ISTAT: `DCIS_NATALMORTA`.

**Codici regione ISTAT:**

| Codice | Regione | Codice | Regione |
|---|---|---|---|
| `01` | Piemonte | `12` | Lazio |
| `02` | Valle d'Aosta | `13` | Abruzzo |
| `03` | Lombardia | `14` | Molise |
| `04` | Trentino-Alto Adige | `15` | Campania |
| `05` | Veneto | `16` | Puglia |
| `06` | Friuli-Venezia Giulia | `17` | Basilicata |
| `07` | Liguria | `18` | Calabria |
| `08` | Emilia-Romagna | `19` | Sicilia |
| `09` | Toscana | `20` | Sardegna |
| `10` | Umbria | | |
| `11` | Marche | | |

---

### Inflazione (NIC / FOI / IPCA)

```csharp
var inf = await istat.GetInflazioneAsync();
inf.Periodo;     // "2026-02"
inf.IndiceNIC;   // 1.2   (variazione % annua — per l'intera collettività)
inf.IndiceFOI;   // 1.1   (per famiglie operai e impiegati — base scala mobile)
inf.IndiceIPCA;  // 1.3   (armonizzato EU — comparabile con altri paesi)
```

Dataset ISTAT: `DCSP_PREZZI` — aggiornato mensilmente.

| Indice | Uso tipico |
|---|---|
| **NIC** | Inflazione generale per tutti i consumatori |
| **FOI** | Base per la scala mobile degli affitti e alcuni contratti |
| **IPCA** | Standard europeo Eurostat, comparabile tra paesi EU |

---

### PIL regionale

```csharp
var pil = await istat.GetPILRegioneAsync("03");  // Lombardia
pil.CodiceRegioneISTAT;  // "03"
pil.Anno;                // 2022  (i dati PIL hanno ~2 anni di ritardo)
pil.PILProCapite;        // 38_400  (euro)
pil.PILTotale;           // 412_000 (milioni di euro)
```

Dataset ISTAT: `DCSC_PILPROCAP`.

> I dati PIL regionali sono disponibili con circa **2 anni di ritardo** rispetto all'anno corrente.
> Il default del parametro `anno` è automaticamente impostato a `annoCorrente - 2`.

---

### Mercato del lavoro per provincia

```csharp
var lav = await istat.GetMercatoLavoroAsync("MI");  // Milano
lav.SiglaProvincia;      // "MI"
lav.Anno;                // 2023
lav.TassoOccupazione;    // 68.4  (% occupati su pop. 15-64)
lav.TassoDisoccupazione; // 4.2   (% disoccupati su forze lavoro)
lav.TassoAttivita;       // 71.4  (% forze lavoro su pop. 15-64)
```

Dataset ISTAT: `DCIS_OCUPATDIS`.

---

### Famiglie per comune

```csharp
var fam = await istat.GetFamiglieAsync("015146");  // Milano
fam.CodiceISTAT;      // "015146"
fam.Anno;             // 2023
fam.NumeroFamiglie;   // 698_000
fam.ComponentoMedio;  // 1.94  (componenti medi per famiglia)
```

Dataset ISTAT: `DCIS_FAMIGLIE`.

---

## Gestione errori

```csharp
try
{
    var pop = await istat.GetPopolazioneAsync("015146");
}
catch (ISTATException ex)
{
    // API ISTAT irraggiungibile o errore HTTP
    Console.WriteLine(ex.Message);
    Console.WriteLine(ex.InnerException?.Message);
}
```

`ISTATException` wrappa `HttpRequestException` e viene lanciata in caso di:
- Server ISTAT irraggiungibile
- Timeout (default: 30 secondi)
- Risposta HTTP non valida

---

## Architettura

```
Italy.Core.ISTAT/
├── ServiziISTAT.cs   # Client SDMX: 6 dataset, parsing JSON, gestione errori
├── Domain.cs         # Model: PopolazioneComune, IndiciInflazione, PILRegione, ecc.
└── README.md
```

### Perché un pacchetto separato da Italy.Core?

| | Italy.Core | Italy.Core.ISTAT |
|---|---|---|
| **Dati** | Embedded SQLite | Live via HTTP |
| **Aggiornamento** | Con ogni release NuGet | Ogni chiamata API |
| **Dipendenze** | Solo `Microsoft.Data.Sqlite` | `Microsoft.Extensions.Http` |
| **Offline** | Funziona sempre | Richiede connessione |
| **Latenza** | Zero (in-process) | ~200-500ms per chiamata |

---

## Compatibilità

| Framework | Linguaggio | Note |
|---|---|---|
| `.NET 8.0` | C# 12 | Feature set completo |
| `.NET Framework 4.8` | C# 7.3 | Sintassi compatibile, PolySharp backport |

---

## Dipendenze

| Pacchetto | net8 | net48 | Scopo |
|---|---|---|---|
| `Italy.Core` | ≥1.0.0 | ≥1.0.0 | Risoluzione Belfiore → ISTAT, ServiziComuni |
| `Microsoft.Extensions.Http` | 8.0.0 | 6.0.0 | HttpClient factory e gestione connessioni |
| `System.Text.Json` | 8.0.5 | 6.0.10 | Parsing SDMX JSON |
| `PolySharp` | — | 1.14.1 | Backport API moderne su net48 |

---

## Ecosistema Italy.Core

| Pacchetto | Descrizione |
|---|---|
| `Italy.Core` | Comuni, province, CF, P.IVA, IBAN, ATECO, banche — dati embedded |
| `Italy.Automotive` | Targhe, RCA, revisioni, bollo, Fringe Benefit ACI |
| **`Italy.Core.ISTAT`** | **Popolazione, inflazione, PIL, lavoro — dati live ISTAT** |
| `Italy.Core.PA` | Catalogo IPA, SdI fatturazione B2G, PEC ufficiali — live IndicePA |

---

## Licenza

MIT — vedi [LICENSE](LICENSE).
