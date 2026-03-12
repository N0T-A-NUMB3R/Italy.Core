// ============================================================
//  Italy.Core — Starter Kit
//  Tutti gli snippet più utili in un unico file interattivo.
// ============================================================

using Italy.Core;
using Italy.Core.Applicazione.Servizi;
using Italy.Core.Domain.Entità;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var atlante = new Atlante();
Menu(atlante);

static void Menu(Atlante a)
{
    Console.WriteLine("╔══════════════════════════════════════════╗");
    Console.WriteLine("║       Italy.Core — Starter Kit           ║");
    Console.WriteLine($"║  DB: {(a.VersioneDati ?? "n/d"),-10}  Aggiorn.: {(a.DataUltimoAggiornamento?.ToString("dd/MM/yyyy") ?? "n/d"),-12}║");
    Console.WriteLine("╚══════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine(" 1. Comuni — ricerca fuzzy e lookup");
    Console.WriteLine(" 2. CAP — ricerca e validazione");
    Console.WriteLine(" 3. Codice Fiscale — calcolo e validazione");
    Console.WriteLine(" 4. Geo — distanze e comuni nel raggio");
    Console.WriteLine(" 5. Regioni e Province + NUTS");
    Console.WriteLine(" 6. ATECO — codici attività");
    Console.WriteLine(" 7. Banche — lookup BIC/ABI");
    Console.WriteLine(" 8. Zone Territoriali — sismica e climatica");
    Console.WriteLine(" 9. Pubblica Amministrazione — IPA/SdI");
    Console.WriteLine("10. Validazione — P.IVA, IBAN, Targa");
    Console.WriteLine("11. Parser e Confronto Indirizzi");
    Console.WriteLine("12. Festività e Calendario");
    Console.WriteLine("13. Telefonia — prefissi e operatori");
    Console.WriteLine("14. Bonifica Dati — pulizia DB legacy");
    Console.WriteLine("15. Frontalieri — zone di confine");
    Console.WriteLine(" 0. Esci");
    Console.WriteLine();
    Console.Write("Scegli: ");

    switch (Console.ReadLine())
    {
        case "1":  SnippetComuni(a); break;
        case "2":  SnippetCAP(a); break;
        case "3":  SnippetCodiceFiscale(a); break;
        case "4":  SnippetGeo(a); break;
        case "5":  SnippetRegioniProvince(a); break;
        case "6":  SnippetATECO(a); break;
        case "7":  SnippetBanche(a); break;
        case "8":  SnippetZoneTerritoriali(a); break;
        case "9":  SnippetPA(a); break;
        case "10": SnippetValidazione(a); break;
        case "11": SnippetParser(a); break;
        case "12": SnippetCalendario(a); break;
        case "13": SnippetTelefonia(a); break;
        case "14": SnippetBonifica(a); break;
        case "15": SnippetFrontalieri(a); break;
        case "0":  return;
        default:   Console.WriteLine("Scelta non valida."); break;
    }

    Console.WriteLine();
    Console.WriteLine("Premi INVIO per tornare al menu...");
    Console.ReadLine();
    Console.Clear();
    Menu(a);
}

// ── 1. COMUNI ────────────────────────────────────────────────
static void SnippetComuni(Atlante a)
{
    H("COMUNI");

    // Ricerca fuzzy — tollera errori di battitura
    var risultati = a.Comuni.Cerca("Mialno");
    P($"Cerca 'Mialno'       → {risultati.FirstOrDefault()?.DenominazioneUfficiale}");

    // Lookup per codice Belfiore
    var milano = a.Comuni.DaCodiceBelfiore("F205");
    P($"F205                 → {milano?.DenominazioneUfficiale} ({milano?.SiglaProvincia})");

    // Comuni di una provincia
    var comuni = a.Comuni.DaProvincia("BG");
    P($"Comuni in BG         → {comuni.Count}");

    // Comune soppresso → successore
    var succ = a.Comuni.OttieniSuccessore("C619");
    P($"Succ. di C619        → {succ?.DenominazioneUfficiale}");

    // Risoluzione codice ISTAT — attivo, soppresso, o inesistente
    Console.WriteLine();
    P("Risoluzione ISTAT:");
    foreach (var istat in new[] { "015146", "076020", "999999" })
    {
        var r = a.Comuni.RisolviCodiceISTATStorico(istat);
        if (!r.Trovato)
            P($"  ISTAT {istat} → NON TROVATO nel DB");
        else if (r.IsAttivo)
            P($"  ISTAT {istat} → ATTIVO: {r.Comune?.DenominazioneUfficiale} ({r.Comune?.SiglaProvincia})");
        else
            P($"  ISTAT {istat} → SOPPRESSO: '{r.Comune?.DenominazioneUfficiale}'" +
              (r.Comune?.DataSoppressione != null ? $" il {r.Comune.DataSoppressione:dd/MM/yyyy}" : "") +
              (r.SuccessoreAttivo != null ? $" → ora: {r.SuccessoreAttivo.DenominazioneUfficiale}" : " (nessun successore)"));
    }

    // Time Machine: esisteva Corigliano Calabro il 1/1/2000?
    Console.WriteLine();
    P($"Corigliano al 2000   → {a.TimeMachine.EsistevaInData("C619", new DateTime(2000, 1, 1))}");
    var snap = a.TimeMachine.OttieniSnapshotInData("C619", new DateTime(2000, 1, 1));
    P($"Snapshot 2000        → {snap?.DenominazioneInData}");
}

// ── 2. CAP ───────────────────────────────────────────────────
static void SnippetCAP(Atlante a)
{
    H("CAP");

    // Zone CAP di un comune
    var zone = a.CAP.OttieniZone("F205");
    P($"CAP Milano (primi 5) → {string.Join(", ", zone.Take(5).Select(z => z.CAP))}");

    // CAP principale
    var capPrinc = a.CAP.CAPPrincipale("F205");
    P($"CAP principale MI    → {capPrinc}");

    // Cerca comuni da CAP
    var perCap = a.CAP.DaCAP("20121");
    P($"CAP 20121            → {perCap.FirstOrDefault()?.DescrizioneZona ?? string.Join(", ", perCap.FirstOrDefault()?.CodiciISTAT ?? [])}");

    // CAP storici
    var storici = a.CAP.CAPStorici("F205");
    P($"CAP storici Milano   → {storici.Count}");
}

// ── 3. CODICE FISCALE ────────────────────────────────────────
static void SnippetCodiceFiscale(Atlante a)
{
    H("CODICE FISCALE");

    // Calcolo CF
    var cf = a.Fiscale.Calcola("Mario", "Rossi", new DateTime(1980, 5, 15), 'M', "F205");
    P($"CF Mario Rossi       → {cf}");

    // Validazione
    var esito = a.Fiscale.Valida("RSSMRA80E15F205Z");
    P($"Valido               → {esito.IsValido}");
    P($"Sesso                → {esito.Sesso}");
    P($"Comune nascita       → {esito.ComuneNascita}");
    P($"Data nascita         → {esito.DataNascita:dd/MM/yyyy}");

    // Lookup comune da CF
    var comune = a.Fiscale.DaCodiceFiscale("RSSMRA80E15F205Z");
    P($"Comune da CF         → {comune?.DenominazioneUfficiale}");

    // Validazione cross CF vs Comune
    var cross = a.ValidazioneCross.ValidaCFvsComune("RSSMRA80E15F205Z", "Milano");
    P($"CF coerente comune   → {cross.IsCoerente} — {cross.Messaggio}");

    // CF Persona Giuridica
    var cfPG = a.FiscalePG.Valida("02120220158");
    P($"CF Pers. Giuridica   → {cfPG.IsValido}");
}

// ── 4. GEO ───────────────────────────────────────────────────
static void SnippetGeo(Atlante a)
{
    H("GEO — DISTANZE E RAGGIO");

    // Distanza Haversine
    var kmMiRo = a.GeoDistanza.DistanzaKm("F205", "H501");
    P($"Milano → Roma        → {kmMiRo:F0} km");

    var kmMiTo = a.GeoDistanza.DistanzaKm("F205", "L219");
    P($"Milano → Torino      → {kmMiTo:F0} km");

    // Coordinate WGS84
    var coord = a.Geo.OttieniCoordinate("F205");
    P($"Coordinate Milano    → {coord?.Latitudine:F4}°N, {coord?.Longitudine:F4}°E");

    // Comuni nel raggio
    var vicini = a.GeoDistanza.ComuniNelRaggio("F205", 20);
    P($"Comuni entro 20km    → {vicini.Count}");
    foreach (var v in vicini.Take(5))
        P($"  {v.Denominazione,-25} {v.DistanzaKm:F1} km");

    // Codici NUTS EU
    var nuts = a.GeoDistanza.OttieniNUTS("F205");
    P($"NUTS Milano          → {nuts?.NUTS1} / {nuts?.NUTS2} / {nuts?.NUTS3}");

    // Comuni per NUTS2 (Lombardia)
    var comuniNuts = a.GeoDistanza.ComuniPerNUTS2("ITC4");
    P($"Comuni NUTS2 ITC4    → {comuniNuts.Count}");
}

// ── 5. REGIONI E PROVINCE ────────────────────────────────────
static void SnippetRegioniProvince(Atlante a)
{
    H("REGIONI E PROVINCE");

    var regioni = a.Regioni.TutteLeRegioni();
    P($"Totale regioni       → {regioni.Count}");
    foreach (var r in regioni.Take(5))
        P($"  {r.Nome,-25} {r.NumeroComuni,5} comuni  NUTS2: {r.CodiceNUTS2}");

    var lom = a.Regioni.DaNome("Lombardia");
    P($"Lombardia            → {lom?.NumeroProvince} province, {lom?.NumeroComuni} comuni");

    var lom2 = a.Regioni.DaCodiceNUTS2("ITC4");
    P($"ITC4                 → {lom2?.Nome}");

    var mi = a.Regioni.DaSigla("MI");
    P($"Prov. MI             → {mi?.Nome}, {mi?.NumeroComuni} comuni, NUTS3: {mi?.CodiceNUTS3}");

    var provLom = a.Regioni.DaRegione("Lombardia");
    P($"Province Lombardia   → {string.Join(", ", provLom.Select(p => p.Sigla))}");
}

// ── 6. ATECO ─────────────────────────────────────────────────
static void SnippetATECO(Atlante a)
{
    H("ATECO 2007 (agg. 2022)");

    var sezioni = a.ATECO.Sezioni();
    P($"Sezioni ATECO        → {sezioni.Count}");
    foreach (var s in sezioni.Take(5))
        P($"  [{s.Codice}] {s.Descrizione}");

    var c6201 = a.ATECO.DaCodice("62.01");
    P($"62.01                → {c6201?.Descrizione}");

    var software = a.ATECO.Cerca("software");
    P($"Cerca 'software'     → {software.Count} risultati");
    foreach (var s in software.Take(3))
        P($"  [{s.Codice}] {s.Descrizione}");

    var ger = a.ATECO.DescrizioneCompleta("62.01");
    P($"Gerarchia 62.01      → {ger}");
}

// ── 7. BANCHE ────────────────────────────────────────────────
static void SnippetBanche(Atlante a)
{
    H("BANCHE — BIC/ABI");

    var banche = a.Banche.Cerca("Intesa");
    P($"Cerca 'Intesa'       → {banche.Count} risultati");
    foreach (var b in banche.Take(3))
        P($"  ABI: {b.CodiceABI}  BIC: {b.CodiceBIC,-12} {b.NomeBanca}");

    var banca = a.Banche.DaBIC("BCITITMM");
    P($"BIC BCITITMM         → {banca?.NomeBanca ?? "non trovata"}");

    P($"BIC 'BCITITMM' ok    → {a.Banche.ValidaBIC("BCITITMM")}");
    P($"BIC 'INVALIDO' ok    → {a.Banche.ValidaBIC("INVALIDO")}");
}

// ── 8. ZONE TERRITORIALI ─────────────────────────────────────
static void SnippetZoneTerritoriali(Atlante a)
{
    H("ZONE TERRITORIALI");

    foreach (var (nome, cb) in new[] { ("Milano", "F205"), ("Roma", "H501"), ("Napoli", "F839"), ("L'Aquila", "A345") })
    {
        var z = a.ZoneTerritoriali.OttieniZone(cb);
        P($"{nome,-12} → Sismica: {z?.ZonaSismica?.ToString() ?? "n/d",-8} Climatica: {z?.ZonaClimatica?.ToString() ?? "n/d"}");
    }

    var zona1 = a.ZoneTerritoriali.ComuniPerZonaSismica(1);
    P($"Comuni zona sis. 1   → {zona1.Count}");

    var zonaF = a.ZoneTerritoriali.ComuniPerZonaClimatica("F");
    P($"Comuni zona clim. F  → {zonaF.Count}");
}

// ── 9. PUBBLICA AMMINISTRAZIONE ──────────────────────────────
static void SnippetPA(Atlante a)
{
    H("PUBBLICA AMMINISTRAZIONE — IPA/SdI");

    var ipa = a.PA.OttieniCodiceIPA("F205");
    P($"Milano IPA           → {ipa?.CodiceIPAUnivoco}");
    P($"Milano SdI           → {ipa?.CodiceSdI}");

    var enti = a.PA.CercaEntePA("Comune di Roma");
    P($"Cerca 'Comune Roma'  → {enti.Count} enti");
    foreach (var e in enti.Take(3))
        P($"  {e.NomeEnte,-35} IPA: {e.CodiceIPAUnivoco}  SdI: {e.CodiceSdI}");

    var (codiceASL, nomeASL) = a.PA.OttieniASL("F205");
    P($"ASL Milano           → {nomeASL ?? codiceASL ?? "non trovata"}");
}

// ── 10. VALIDAZIONE ──────────────────────────────────────────
static void SnippetValidazione(Atlante a)
{
    H("VALIDAZIONE — P.IVA, IBAN, TARGA");

    var piva = a.Validazione.ValidaPartitaIVA("02120220158");
    P($"P.IVA Comune MI      → {piva.IsValida}  ATECO: {piva.CodiceATECO}");

    var pivaFalsa = a.Validazione.ValidaPartitaIVA("12345678901");
    P($"P.IVA falsa          → {pivaFalsa.IsValida}");

    var iban = a.Validazione.ValidaIBAN("IT60X0542811101000000123456");
    P($"IBAN valido          → {iban.IsValido}  BIC: {iban.CodiceBIC}");

    var targa = a.Validazione.ValidaTarga("AB123CD");
    P($"Targa AB123CD        → {targa.IsValida}  Formato: {targa.Formato}");

    // Validazione cross CAP + Comune + Provincia
    var cross = a.ValidazioneCross.ValidaCAPComuneProvincia("20121", "Milano", "MI");
    P($"CAP+Comune+Prov.     → {cross.IsCoerente}");

    // Validazione cross IBAN vs Comune fornitore
    var crossIban = a.ValidazioneCross.ValidaIBANvsComune("IT60X0542811101000000123456", "F205");
    P($"IBAN vs Comune       → {crossIban.IsCoerente} — {crossIban.Messaggio}");
}

// ── 11. PARSER INDIRIZZI ─────────────────────────────────────
static void SnippetParser(Atlante a)
{
    H("PARSER E CONFRONTO INDIRIZZI");

    string[] indirizzi =
    [
        "Via Roma 10, 17025 Loano (SV)",
        "P.ZZA GARIBALDI 5/A - SAVONA 17100",
        "Cso Buenos Aires, 23 Milano MI",
        "V.le Monza 12 - 20127",
    ];

    foreach (var ind in indirizzi)
    {
        var p = a.Parser.Analizza(ind);
        P($"IN:  {ind}");
        P($"OUT: {p.Toponimo} {p.NomeVia} {p.Civico} — {p.CAP} {p.NomeComune} ({p.SiglaProvincia})");
        Console.WriteLine();
    }

    // Confronto indirizzi
    var c1 = a.Confronto.Confronta("Via Roma 10, Loano SV", "V. Roma, 10 - Loano (SV)");
    P($"Confronto simili     → {c1.Esito} ({c1.PercentualeTotale:F1}%)");

    var c2 = a.Confronto.Confronta("Via Roma 10, Milano", "Via Dante 5, Roma");
    P($"Confronto diversi    → {c2.Esito} ({c2.PercentualeTotale:F1}%)");
}

// ── 12. FESTIVITÀ ────────────────────────────────────────────
static void SnippetCalendario(Atlante a)
{
    H("FESTIVITÀ E CALENDARIO");

    var anno = DateTime.Now.Year;

    var pasqua = a.Calendario.CalcolaPasqua(anno);
    P($"Pasqua {anno}          → {pasqua:dd/MM/yyyy}");

    var festivita = a.Calendario.OttieniFestività(anno)
        .Where(f => f.Tipo == TipoFestività.Nazionale).ToList();
    P($"Festività nazionali  → {festivita.Count}");
    foreach (var f in festivita)
        P($"  {f.Data:dd/MM} — {f.Nome}");

    var natale = new DateTime(anno, 12, 25);
    P($"25/12 è festivo      → {a.Calendario.IsFestivo(natale)}");

    var lavorativi = a.Calendario.CalcolaGiorniLavorativi(
        new DateTime(anno, 1, 1), new DateTime(anno, 1, 31));
    P($"Lavorativi gennaio   → {lavorativi}");
}

// ── 13. TELEFONIA ────────────────────────────────────────────
static void SnippetTelefonia(Atlante a)
{
    H("TELEFONIA — PREFISSI E OPERATORI");

    // Prefisso fisso di un comune
    var prefMI = a.Telefonia.OttieniPrefisso("F205");
    P($"Prefisso Milano      → {prefMI ?? "n/d"}");

    var prefNA = a.Telefonia.OttieniPrefisso("F839");
    P($"Prefisso Napoli      → {prefNA ?? "n/d"}");

    // Lookup per prefisso
    var area02 = a.Telefonia.DaPrefisso("02");
    P($"Prefisso 02          → {area02?.AreaGeografica ?? "n/d"}");

    // Validazione e normalizzazione numeri
    Console.WriteLine();
    P("Validazione numeri:");
    string[] numeri =
    [
        "02 1234567",        // fisso Milano
        "+39 333 1234567",   // mobile
        "800 123456",        // toll-free
        "118",               // emergenza
        "abc",               // invalido
    ];

    foreach (var n in numeri)
    {
        var r = a.Telefonia.Valida(n);
        if (r.IsValido)
            P($"  {n,-22} → {r.Tipo,-12} {r.NumeroNormalizzatoE164,-18} {r.AreaGeografica ?? r.NomeOperatore ?? ""}");
        else
            P($"  {n,-22} → NON VALIDO: {string.Join(", ", r.Anomalie ?? [])}");
    }

    // Identificazione operatore mobile
    var op = a.Telefonia.IdentificaOperatore("3331234567");
    P($"Operatore 333...     → {op?.NomeOperatore ?? "sconosciuto"}");
}

// ── 14. BONIFICA DATI ────────────────────────────────────────
static void SnippetBonifica(Atlante a)
{
    H("BONIFICA DATI — PULIZIA DB LEGACY");

    // Comune soppresso → suggerisci successore
    var b1 = a.Bonifica.AnalizzaComune("Corigliano Calabro", "CS");
    Stampa(b1, "Corigliano Calabro/CS");

    // Comune con sigla provincia errata
    var b2 = a.Bonifica.AnalizzaComune("Monza", "MI");
    Stampa(b2, "Monza con prov. MI");

    // Comune con errore ortografico (fuzzy)
    var b3 = a.Bonifica.AnalizzaComune("Miilano");
    Stampa(b3, "Miilano (typo)");

    // Sigla provincia storica soppressa
    Console.WriteLine();
    P("Province storiche soppresse:");
    foreach (var sigla in new[] { "CI", "OG", "OT", "VS" })
    {
        var r = a.Bonifica.VerificaSiglaProvincia(sigla);
        P($"  {sigla} → {(r.RequiereCorrezione ? $"{r.ValoreSuggerito} ({r.Motivazione?.Split('.')[0]})" : "OK")}");
    }

    // Bonifica indirizzo completo (comune + CAP + provincia + CF)
    Console.WriteLine();
    P("Bonifica indirizzo completo:");
    var anomalie = a.Bonifica.AnalizzaIndirizzo(
        nomeComune: "Corigliano Calabro",
        cap: "87064",
        siglaProvincia: "CS",
        codiceFiscale: null);

    if (anomalie.Count == 0)
        P("  Nessuna anomalia rilevata.");
    else
        foreach (var an in anomalie)
            P($"  [{an.Tipo}] {an.CampoProblematico}: {an.Motivazione}");

    // Bonifica batch — simula un elenco di record da DB legacy
    Console.WriteLine();
    P("Elaborazione batch (3 record):");
    var records = new List<RecordDaBonificare>
    {
        new() { NomeComune = "Sesto S. Giovanni", SiglaProvincia = "MI", CAP = "20099" },
        new() { NomeComune = "Corigliano Calabro", SiglaProvincia = "CS", CAP = "87064" },
        new() { NomeComune = "Milano",             SiglaProvincia = "MI", CAP = "20121" },
    };

    var report = a.Bonifica.ElaboraBatch(records);
    P($"  Totale: {report.TotaleRecord}  Puliti: {report.RecordPuliti}  Con anomalie: {report.RecordConAnomalie}");
    foreach (var rec in report.Risultati.Where(r => r.HasAnomalie))
    {
        var orig = rec.DatoOriginale as RecordDaBonificare;
        P($"  Record #{rec.IndiceRecord} — {orig?.NomeComune}:");
        foreach (var c in rec.Correzioni)
            P($"    [{c.Tipo}] {c.CampoProblematico} → suggerito: {c.ValoreSuggerito} (conf. {c.ConfidenzaSuggerimento:P0})");
    }

    static void Stampa(RisultatoBonifica r, string etichetta)
    {
        if (!r.RequiereCorrezione)
            P($"{etichetta,-25} → OK");
        else
            P($"{etichetta,-25} → [{r.Tipo}] suggerito: '{r.ValoreSuggerito}' (conf. {r.ConfidenzaSuggerimento:P0})");
    }
}

// ── 15. FRONTALIERI ──────────────────────────────────────────
static void SnippetFrontalieri(Atlante a)
{
    H("FRONTALIERI — ZONE DI CONFINE");

    // Comuni di confine Svizzera / Francia / Austria / Slovenia
    Console.WriteLine();
    P("Verifica comuni frontalieri:");
    var casi = new[]
    {
        ("Como",          "C933"),   // frontaliero Svizzera
        ("Ventimiglia",   "L741"),   // frontaliero Francia
        ("Tarvisio",      "L057"),   // frontaliero Austria/Slovenia
        ("Milano",        "F205"),   // non frontaliero
        ("Palermo",       "G273"),   // non frontaliero
    };

    foreach (var (nome, cb) in casi)
    {
        var info = a.Frontalieri.OttieniInfoFrontalieri(cb);
        if (info.IsComuneFrontaliero)
            P($"  {nome,-15} → FRONTALIERO {info.StatoConfinante,-10} {info.DistanzaConfineKm:F1} km  Regime: {info.Regime}");
        else
            P($"  {nome,-15} → non frontaliero ({info.DistanzaConfineKm:F1} km dal confine più vicino)");
    }

    // Note normative dettagliate per un comune svizzero
    Console.WriteLine();
    var comoInfo = a.Frontalieri.OttieniInfoFrontalieri("C933");
    if (comoInfo.NoteNormative != null)
    {
        P("Note normative Como:");
        P($"  {comoInfo.NoteNormative}");
        if (comoInfo.DataDecorrenza.HasValue)
            P($"  Decorrenza: {comoInfo.DataDecorrenza:dd/MM/yyyy}");
    }
}

// ── Helper ───────────────────────────────────────────────────
static void H(string testo) =>
    Console.WriteLine($"\n── {testo} {new string('─', Math.Max(0, 38 - testo.Length))}");

static void P(string testo) =>
    Console.WriteLine($"  {testo}");
