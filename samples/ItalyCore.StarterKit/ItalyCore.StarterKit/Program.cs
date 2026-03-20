// ============================================================
//  Italy.Core — Starter Kit
//  Tutti gli snippet più utili in un unico file interattivo.
//  Compatibile net48 e net8.0
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Italy.Core;
using Italy.Core.Applicazione.Servizi;
using Italy.Core.Domain.Entità;

internal static class Program
{
    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var atlante = new Atlante();
        Menu(atlante);
    }

    static void Menu(Atlante a)
    {
        Console.Clear();
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║       Italy.Core — Starter Kit           ║");
        Console.WriteLine(string.Format("║  DB: {0,-10}  Aggiorn.: {1,-12}║",
            a.VersioneDati ?? "n/d",
            a.DataUltimoAggiornamento.HasValue ? a.DataUltimoAggiornamento.Value.ToString("dd/MM/yyyy") : "n/d"));
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

        Menu(a);
    }

    // ── 1. COMUNI ────────────────────────────────────────────────
    // ── Helper input ─────────────────────────────────────────────
    static string Chiedi(string descrizione, string esempio, string defaultVal = null)
    {
        if (defaultVal != null)
            Console.Write("  " + descrizione + " (es. " + esempio + ") [INVIO = " + defaultVal + "]: ");
        else
            Console.Write("  " + descrizione + " (es. " + esempio + "): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input) && defaultVal != null)
            return defaultVal;
        return string.IsNullOrEmpty(input) ? esempio : input;
    }

    // ── 1. COMUNI ────────────────────────────────────────────────
    static void SnippetComuni(Atlante a)
    {
        H("COMUNI");
        Console.WriteLine("  a. Ricerca per nome (+ codice Belfiore e ISTAT)");
        Console.WriteLine("  b. Lookup per codice Belfiore");
        Console.WriteLine("  c. Elenco comuni di una provincia");
        Console.WriteLine("  d. Risoluzione codice ISTAT");
        Console.WriteLine("  e. Time Machine — stato storico");
        Console.WriteLine("  f. Successore di comune soppresso");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": ComuniCercaFuzzy(a); break;
            case "b": ComuniDaBelfiore(a); break;
            case "c": ComuniPerProvincia(a); break;
            case "d": ComuniISTAT(a); break;
            case "e": ComuniTimeMachine(a); break;
            case "f": ComuniSuccessore(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Comuni...");
        Console.ReadLine();
        SnippetComuni(a);
    }

    static void ComuniCercaFuzzy(Atlante a)
    {
        H("COMUNI — Ricerca per nome");
        Console.WriteLine("  Cerca un comune per nome (fuzzy: tollera errori di battitura).");
        Console.WriteLine("  Restituisce nome, sigla provincia, codice Belfiore e codice ISTAT.");
        var nome = Chiedi("Nome comune (anche con errori)", "Milano", "Milano");
        var risultati = a.Comuni.Cerca(nome);
        if (risultati.Count == 0) { P("Nessun risultato trovato."); return; }
        P("Risultati trovati    → " + risultati.Count);
        foreach (var c in risultati.Take(8))
            P("  " + c.DenominazioneUfficiale.PadRight(30)
              + " (" + c.SiglaProvincia + ")"
              + "  Belfiore: " + c.CodiceBelfiore
              + "  ISTAT: " + c.CodiceISTAT);
    }

    static void ComuniDaBelfiore(Atlante a)
    {
        H("COMUNI — Lookup codice Belfiore");
        Console.WriteLine("  Es: F205 = Milano, H501 = Roma, L219 = Torino, C619 = Corigliano Calabro (soppresso).");
        var cb = Chiedi("Codice Belfiore", "F205", "F205");
        var comune = a.Comuni.DaCodiceBelfiore(cb.ToUpper());
        if (comune == null) { P("Comune non trovato per il codice " + cb.ToUpper()); return; }
        P("Comune               → " + comune.DenominazioneUfficiale);
        P("Provincia            → " + comune.SiglaProvincia);
        P("Codice ISTAT         → " + comune.CodiceISTAT);
        P("Attivo               → " + (comune.DataSoppressione == null ? "Sì" : "No — soppresso il " + comune.DataSoppressione.Value.ToString("dd/MM/yyyy")));
    }

    static void ComuniPerProvincia(Atlante a)
    {
        H("COMUNI — Elenco per provincia");
        var prov = Chiedi("Sigla provincia (2 lettere)", "BG", "BG");
        var comuni = a.Comuni.DaProvincia(prov.ToUpper());
        P("Comuni in " + prov.ToUpper() + "          → " + comuni.Count);
        foreach (var c in comuni.Take(10))
            P("  " + c.DenominazioneUfficiale.PadRight(30) + " ISTAT: " + c.CodiceISTAT);
        if (comuni.Count > 10) P("  ... e altri " + (comuni.Count - 10));
    }

    static void ComuniISTAT(Atlante a)
    {
        H("COMUNI — Risoluzione codice ISTAT");
        Console.WriteLine("  Es: 015146 = Milano (attivo), 076020 = Corigliano Calabro (soppresso).");
        var istat = Chiedi("Codice ISTAT (6 cifre)", "015146", "015146");
        var r = a.Comuni.RisolviCodiceISTATStorico(istat);
        if (!r.Trovato) { P("Risultato            → NON TROVATO nel DB"); return; }
        if (r.IsAttivo)
        {
            P("Risultato            → ATTIVO");
            P("Comune               → " + r.Comune?.DenominazioneUfficiale + " (" + r.Comune?.SiglaProvincia + ")");
        }
        else
        {
            P("Risultato            → SOPPRESSO");
            P("Comune originale     → " + r.Comune?.DenominazioneUfficiale);
            if (r.Comune?.DataSoppressione != null)
                P("Data soppressione    → " + r.Comune.DataSoppressione.Value.ToString("dd/MM/yyyy"));
            if (r.SuccessoreAttivo != null)
                P("Successore attivo    → " + r.SuccessoreAttivo.DenominazioneUfficiale + " (" + r.SuccessoreAttivo.SiglaProvincia + ")");
            else
                P("Successore           → nessuno");
        }
    }

    static void ComuniTimeMachine(Atlante a)
    {
        H("COMUNI — Time Machine");
        Console.WriteLine("  Verifica lo stato di un comune in una data storica.");
        var cb = Chiedi("Codice Belfiore", "C619", "C619");
        var dataStr = Chiedi("Data storica (dd/MM/yyyy)", "01/01/2000", "01/01/2000");
        DateTime dt;
        if (!DateTime.TryParseExact(dataStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out dt))
            dt = new DateTime(2000, 1, 1);
        P("Esisteva il " + dt.ToString("dd/MM/yyyy") + "  → " + a.TimeMachine.EsistevaInData(cb.ToUpper(), dt));
        var snap = a.TimeMachine.OttieniSnapshotInData(cb.ToUpper(), dt);
        P("Nome in quella data  → " + (snap?.DenominazioneInData ?? "n/d"));
    }

    static void ComuniSuccessore(Atlante a)
    {
        H("COMUNI — Successore di comune soppresso");
        Console.WriteLine("  Es: C619 = Corigliano Calabro → successore = Corigliano-Rossano.");
        var cb = Chiedi("Codice Belfiore comune soppresso", "C619", "C619");
        var succ = a.Comuni.OttieniSuccessore(cb.ToUpper());
        if (succ == null)
            P("Nessun successore trovato (comune attivo o codice inesistente).");
        else
        {
            P("Successore           → " + succ.DenominazioneUfficiale);
            P("Provincia            → " + succ.SiglaProvincia);
            P("Codice Belfiore      → " + succ.CodiceBelfiore);
        }
    }

    // ── 2. CAP ───────────────────────────────────────────────────
    static void SnippetCAP(Atlante a)
    {
        H("CAP");
        Console.WriteLine("  a. CAP di un comune (da codice Belfiore)");
        Console.WriteLine("  b. Comune da CAP");
        Console.WriteLine("  c. CAP storici di un comune");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": CAPPerComune(a); break;
            case "b": ComuneDaCAP(a); break;
            case "c": CAPStorici(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu CAP...");
        Console.ReadLine();
        SnippetCAP(a);
    }

    static void CAPPerComune(Atlante a)
    {
        H("CAP — CAP di un comune");
        var cb = Chiedi("Codice Belfiore", "F205", "F205");
        var zone = a.CAP.OttieniZone(cb.ToUpper());
        P("CAP trovati          → " + zone.Count);
        foreach (var z in zone.Take(10))
            P("  " + z.CAP + (z.DescrizioneZona != null ? "  (" + z.DescrizioneZona + ")" : ""));
        if (zone.Count > 10) P("  ... e altri " + (zone.Count - 10));
        P("CAP principale       → " + (a.CAP.CAPPrincipale(cb.ToUpper()) ?? "n/d"));
    }

    static void ComuneDaCAP(Atlante a)
    {
        H("CAP — Comune da CAP");
        var cap = Chiedi("CAP (5 cifre)", "20121", "20121");
        var perCap = a.CAP.DaCAP(cap);
        if (perCap.Count == 0) { P("Nessun comune trovato per CAP " + cap); return; }
        P("Risultati            → " + perCap.Count);
        foreach (var z in perCap.Take(8))
            P("  " + (z.DescrizioneZona ?? string.Join(", ", z.CodiciISTAT ?? new List<string>())));
    }

    static void CAPStorici(Atlante a)
    {
        H("CAP — CAP storici");
        var cb = Chiedi("Codice Belfiore", "F205", "F205");
        var storici = a.CAP.CAPStorici(cb.ToUpper());
        P("CAP storici trovati  → " + storici.Count);
        foreach (var z in storici.Take(10))
            P("  " + z.CAP);
    }

    // ── 3. CODICE FISCALE ────────────────────────────────────────
    static void SnippetCodiceFiscale(Atlante a)
    {
        H("CODICE FISCALE");
        Console.WriteLine("  a. Calcola codice fiscale");
        Console.WriteLine("  b. Valida e decodifica un CF");
        Console.WriteLine("  c. Verifica CF vs comune di nascita");
        Console.WriteLine("  d. Valida CF / P.IVA persona giuridica");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": CFCalcola(a); break;
            case "b": CFValida(a); break;
            case "c": CFvsComune(a); break;
            case "d": CFPG(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Codice Fiscale...");
        Console.ReadLine();
        SnippetCodiceFiscale(a);
    }

    static void CFCalcola(Atlante a)
    {
        H("CODICE FISCALE — Calcolo");
        var nome = Chiedi("Nome", "Mario", "Mario");
        var cognome = Chiedi("Cognome", "Rossi", "Rossi");
        var dataNascita = Chiedi("Data di nascita (dd/MM/yyyy)", "15/05/1980", "15/05/1980");
        var sesso = Chiedi("Sesso (M/F)", "M", "M");
        var cb = Chiedi("Codice Belfiore comune nascita", "F205", "F205");
        DateTime dn;
        if (!DateTime.TryParseExact(dataNascita, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out dn))
            dn = new DateTime(1980, 5, 15);
        var cf = a.Fiscale.Calcola(nome, cognome, dn, sesso.ToUpper() == "F" ? 'F' : 'M', cb.ToUpper());
        P("Codice Fiscale       → " + cf);
    }

    static void CFValida(Atlante a)
    {
        H("CODICE FISCALE — Validazione e decodifica");
        var cf = Chiedi("Codice Fiscale (16 caratteri)", "RSSMRA80E15F205Z", "RSSMRA80E15F205Z");
        var esito = a.Fiscale.Valida(cf.ToUpper());
        P("Valido               → " + esito.IsValido);
        if (esito.IsValido)
        {
            P("Sesso                → " + esito.Sesso);
            P("Data nascita         → " + (esito.DataNascita?.ToString("dd/MM/yyyy") ?? "n/d"));
            P("Comune nascita       → " + (esito.ComuneNascita ?? "n/d"));
        }
        else
            P("Motivo               → formato non valido");
    }

    static void CFvsComune(Atlante a)
    {
        H("CODICE FISCALE — Verifica CF vs comune");
        var cf = Chiedi("Codice Fiscale", "RSSMRA80E15F205Z", "RSSMRA80E15F205Z");
        var comune = Chiedi("Nome comune di nascita", "Milano", "Milano");
        var cross = a.ValidazioneCross.ValidaCFvsComune(cf.ToUpper(), comune);
        P("Coerente             → " + cross.IsCoerente);
        P("Messaggio            → " + cross.Messaggio);
    }

    static void CFPG(Atlante a)
    {
        H("CODICE FISCALE — Persona Giuridica");
        var cfpg = Chiedi("CF / P.IVA persona giuridica", "02120220158", "02120220158");
        var res = a.FiscalePG.Valida(cfpg);
        P("Valido               → " + res.IsValido);
    }

    // ── 4. GEO ───────────────────────────────────────────────────
    static void SnippetGeo(Atlante a)
    {
        H("GEO — DISTANZE E RAGGIO");
        Console.WriteLine("  a. Distanza tra due comuni");
        Console.WriteLine("  b. Coordinate WGS84 di un comune");
        Console.WriteLine("  c. Comuni nel raggio di X km");
        Console.WriteLine("  d. Codici NUTS europei");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": GeoDistanza(a); break;
            case "b": GeoCoordinate(a); break;
            case "c": GeoRaggio(a); break;
            case "d": GeoNUTS(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Geo...");
        Console.ReadLine();
        SnippetGeo(a);
    }

    static void GeoDistanza(Atlante a)
    {
        H("GEO — Distanza tra comuni");
        var cb1 = Chiedi("Codice Belfiore comune A", "F205", "F205");
        var cb2 = Chiedi("Codice Belfiore comune B", "H501", "H501");
        var km = a.GeoDistanza.DistanzaKm(cb1.ToUpper(), cb2.ToUpper());
        P("Distanza             → " + (km.HasValue ? km.Value.ToString("F1") + " km" : "n/d (coordinate mancanti)"));
    }

    static void GeoCoordinate(Atlante a)
    {
        H("GEO — Coordinate WGS84");
        var cb = Chiedi("Codice Belfiore", "F205", "F205");
        var coord = a.Geo.OttieniCoordinate(cb.ToUpper());
        if (coord == null) { P("Coordinate non disponibili."); return; }
        P("Latitudine           → " + coord.Latitudine.ToString("F6") + "°N");
        P("Longitudine          → " + coord.Longitudine.ToString("F6") + "°E");
    }

    static void GeoRaggio(Atlante a)
    {
        H("GEO — Comuni nel raggio");
        var cb = Chiedi("Codice Belfiore comune centrale", "F205", "F205");
        var raggioStr = Chiedi("Raggio in km", "20", "20");
        double raggio;
        if (!double.TryParse(raggioStr, out raggio)) raggio = 20;
        var vicini = a.GeoDistanza.ComuniNelRaggio(cb.ToUpper(), raggio);
        P("Comuni trovati       → " + vicini.Count);
        foreach (var v in vicini.Take(10))
            P("  " + v.Denominazione.PadRight(28) + " " + v.DistanzaKm.ToString("F1") + " km");
        if (vicini.Count > 10) P("  ... e altri " + (vicini.Count - 10));
    }

    static void GeoNUTS(Atlante a)
    {
        H("GEO — Codici NUTS");
        Console.WriteLine("  NUTS1 = macroregione, NUTS2 = regione, NUTS3 = provincia.");
        var cb = Chiedi("Codice Belfiore", "F205", "F205");
        var nuts = a.GeoDistanza.OttieniNUTS(cb.ToUpper());
        if (!nuts.HasValue) { P("Dati NUTS non disponibili."); return; }
        P("NUTS1 (macroregione) → " + (nuts.Value.NUTS1 ?? "n/d"));
        P("NUTS2 (regione)      → " + (nuts.Value.NUTS2 ?? "n/d"));
        P("NUTS3 (provincia)    → " + (nuts.Value.NUTS3 ?? "n/d"));
    }

    // ── 5. REGIONI E PROVINCE ────────────────────────────────────
    static void SnippetRegioniProvince(Atlante a)
    {
        H("REGIONI E PROVINCE");
        Console.WriteLine("  a. Elenco tutte le regioni");
        Console.WriteLine("  b. Dettaglio regione per nome");
        Console.WriteLine("  c. Dettaglio provincia per sigla");
        Console.WriteLine("  d. Lookup regione da codice NUTS2");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": RegioniElenco(a); break;
            case "b": RegioniDettaglio(a); break;
            case "c": ProvinciaDettaglio(a); break;
            case "d": RegioniNUTS2(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Regioni...");
        Console.ReadLine();
        SnippetRegioniProvince(a);
    }

    static void RegioniElenco(Atlante a)
    {
        H("REGIONI — Elenco completo");
        var regioni = a.Regioni.TutteLeRegioni();
        P("Totale regioni       → " + regioni.Count);
        foreach (var r in regioni)
            P("  " + r.Nome.PadRight(25) + " " + r.NumeroComuni.ToString().PadLeft(5) + " comuni  NUTS2: " + r.CodiceNUTS2);
    }

    static void RegioniDettaglio(Atlante a)
    {
        H("REGIONI — Dettaglio regione");
        var nomeReg = Chiedi("Nome regione", "Lombardia", "Lombardia");
        var reg = a.Regioni.DaNome(nomeReg);
        if (reg == null) { P("Regione non trovata."); return; }
        P("Regione              → " + reg.Nome);
        P("Province             → " + reg.NumeroProvince);
        P("Comuni               → " + reg.NumeroComuni);
        P("NUTS2                → " + reg.CodiceNUTS2);
        var prov = a.Regioni.DaRegione(nomeReg);
        P("Sigle province       → " + string.Join(", ", prov.Select(p => p.Sigla)));
    }

    static void ProvinciaDettaglio(Atlante a)
    {
        H("PROVINCE — Dettaglio provincia");
        var sigla = Chiedi("Sigla provincia", "MI", "MI");
        var prov = a.Regioni.DaSigla(sigla.ToUpper());
        if (prov == null) { P("Provincia non trovata."); return; }
        P("Provincia            → " + prov.Nome);
        P("Comuni               → " + prov.NumeroComuni);
        P("NUTS3                → " + prov.CodiceNUTS3);
    }

    static void RegioniNUTS2(Atlante a)
    {
        H("REGIONI — Lookup da codice NUTS2");
        var nuts2 = Chiedi("Codice NUTS2", "ITC4", "ITC4");
        var reg = a.Regioni.DaCodiceNUTS2(nuts2.ToUpper());
        if (reg == null) { P("Regione non trovata."); return; }
        P("Regione              → " + reg.Nome);
        P("Province             → " + reg.NumeroProvince);
        P("Comuni               → " + reg.NumeroComuni);
    }

    // ── 6. ATECO ─────────────────────────────────────────────────
    static void SnippetATECO(Atlante a)
    {
        H("ATECO 2007 (agg. 2022)");
        Console.WriteLine("  a. Cerca per parola chiave");
        Console.WriteLine("  b. Lookup codice ATECO");
        Console.WriteLine("  c. Elenco sezioni ATECO");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": ATECOCerca(a); break;
            case "b": ATECOLookup(a); break;
            case "c": ATECOSezioni(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu ATECO...");
        Console.ReadLine();
        SnippetATECO(a);
    }

    static void ATECOCerca(Atlante a)
    {
        H("ATECO — Ricerca per parola chiave");
        var kw = Chiedi("Parola chiave", "software", "software");
        var risultati = a.ATECO.Cerca(kw);
        P("Risultati trovati    → " + risultati.Count);
        foreach (var s in risultati.Take(10))
            P("  [" + s.Codice + "] " + s.Descrizione);
        if (risultati.Count > 10) P("  ... e altri " + (risultati.Count - 10));
    }

    static void ATECOLookup(Atlante a)
    {
        H("ATECO — Lookup codice");
        var codice = Chiedi("Codice ATECO", "62.01", "62.01");
        var voce = a.ATECO.DaCodice(codice);
        if (voce == null) { P("Codice non trovato."); return; }
        P("Descrizione          → " + voce.Descrizione);
        P("Gerarchia            → " + a.ATECO.DescrizioneCompleta(codice));
    }

    static void ATECOSezioni(Atlante a)
    {
        H("ATECO — Sezioni");
        var sezioni = a.ATECO.Sezioni();
        P("Totale sezioni       → " + sezioni.Count);
        foreach (var s in sezioni)
            P("  [" + s.Codice + "] " + s.Descrizione);
    }

    // ── 7. BANCHE ────────────────────────────────────────────────
    static void SnippetBanche(Atlante a)
    {
        H("BANCHE — BIC/ABI");
        Console.WriteLine("  a. Cerca banca per nome");
        Console.WriteLine("  b. Lookup codice BIC/SWIFT");
        Console.WriteLine("  c. Valida codice BIC");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": BancheCerca(a); break;
            case "b": BancheLookupBIC(a); break;
            case "c": BancheValidaBIC(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Banche...");
        Console.ReadLine();
        SnippetBanche(a);
    }

    static void BancheCerca(Atlante a)
    {
        H("BANCHE — Cerca per nome");
        Console.WriteLine("  ABI = codice banca italiano (5 cifre), BIC = codice internazionale SWIFT.");
        var nomeBanca = Chiedi("Nome banca (anche parziale)", "Intesa", "Intesa");
        var banche = a.Banche.Cerca(nomeBanca);
        P("Risultati trovati    → " + banche.Count);
        foreach (var b in banche.Take(8))
            P("  ABI: " + b.CodiceABI + "  BIC: " + b.CodiceBIC.PadRight(12) + " " + b.NomeBanca);
        if (banche.Count > 8) P("  ... e altri " + (banche.Count - 8));
    }

    static void BancheLookupBIC(Atlante a)
    {
        H("BANCHE — Lookup BIC");
        Console.WriteLine("  Es: BCITITMM = Intesa Sanpaolo, UNCRITMM = UniCredit.");
        var bic = Chiedi("Codice BIC/SWIFT (8-11 caratteri)", "BCITITMM", "BCITITMM");
        var banca = a.Banche.DaBIC(bic.ToUpper());
        if (banca == null) { P("Banca non trovata per BIC: " + bic.ToUpper()); return; }
        P("Nome banca           → " + banca.NomeBanca);
        P("Codice ABI           → " + banca.CodiceABI);
        P("Codice BIC           → " + banca.CodiceBIC);
    }

    static void BancheValidaBIC(Atlante a)
    {
        H("BANCHE — Valida BIC");
        var bic = Chiedi("Codice BIC/SWIFT", "BCITITMM", "BCITITMM");
        P("BIC valido           → " + a.Banche.ValidaBIC(bic.ToUpper()));
    }

    // ── 8. ZONE TERRITORIALI ─────────────────────────────────────
    static void SnippetZoneTerritoriali(Atlante a)
    {
        H("ZONE TERRITORIALI");
        Console.WriteLine("  a. Zone di un comune (sismica + climatica)");
        Console.WriteLine("  b. Comuni per zona sismica");
        Console.WriteLine("  c. Comuni per zona climatica");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": ZoneComune(a); break;
            case "b": ZoneSismica(a); break;
            case "c": ZoneClimatica(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Zone Territoriali...");
        Console.ReadLine();
        SnippetZoneTerritoriali(a);
    }

    static void ZoneComune(Atlante a)
    {
        H("ZONE — Zone di un comune");
        Console.WriteLine("  Zona sismica: 1 = alta pericolosità, 4 = bassa. Zona climatica: A = calda, F = fredda.");
        var cb = Chiedi("Codice Belfiore", "F205", "F205");
        var z = a.ZoneTerritoriali.OttieniZone(cb);
        if (z == null) { P("Dati non disponibili per questo comune."); return; }
        P("Zona sismica         → " + (z.ZonaSismica?.ToString() ?? "n/d"));
        P("Zona climatica       → " + (z.ZonaClimatica?.ToString() ?? "n/d"));
    }

    static void ZoneSismica(Atlante a)
    {
        H("ZONE — Comuni per zona sismica");
        Console.WriteLine("  Zona 1 = massima pericolosità, zona 4 = minima pericolosità.");
        var zonaStr = Chiedi("Zona sismica (1-4)", "1", "1");
        int zonaNum;
        if (!int.TryParse(zonaStr, out zonaNum)) zonaNum = 1;
        var lista = a.ZoneTerritoriali.ComuniPerZonaSismica(zonaNum);
        P("Comuni in zona " + zonaNum + "     → " + lista.Count);
        foreach (var cb in lista.Take(10))
            P("  " + cb);
        if (lista.Count > 10) P("  ... e altri " + (lista.Count - 10));
    }

    static void ZoneClimatica(Atlante a)
    {
        H("ZONE — Comuni per zona climatica");
        Console.WriteLine("  Zona A = zone più calde (es. isole), zona F = zone montane più fredde.");
        var zonaClim = Chiedi("Zona climatica (A-F)", "F", "F");
        var lista = a.ZoneTerritoriali.ComuniPerZonaClimatica(zonaClim.ToUpper());
        P("Comuni in zona " + zonaClim.ToUpper() + "     → " + lista.Count);
        foreach (var cb in lista.Take(10))
            P("  " + cb);
        if (lista.Count > 10) P("  ... e altri " + (lista.Count - 10));
    }

    // ── 9. PUBBLICA AMMINISTRAZIONE ──────────────────────────────
    static void SnippetPA(Atlante a)
    {
        H("PUBBLICA AMMINISTRAZIONE — IPA/SdI");
        Console.WriteLine("  a. Codice IPA/SdI di un comune");
        Console.WriteLine("  b. Cerca ente della PA");
        Console.WriteLine("  c. ASL di competenza di un comune");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": PACodiceIPA(a); break;
            case "b": PACercaEnte(a); break;
            case "c": PAASL(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu PA...");
        Console.ReadLine();
        SnippetPA(a);
    }

    static void PACodiceIPA(Atlante a)
    {
        H("PA — Codice IPA/SdI");
        Console.WriteLine("  IPA = Indice PA, SdI = codice destinatario fattura elettronica.");
        var cb = Chiedi("Codice Belfiore comune", "F205", "F205");
        var ipa = a.PA.OttieniCodiceIPA(cb);
        P("Codice IPA           → " + (ipa?.CodiceIPAUnivoco ?? "n/d"));
        P("Codice SdI           → " + (ipa?.CodiceSdI ?? "n/d"));
    }

    static void PACercaEnte(Atlante a)
    {
        H("PA — Cerca ente PA");
        var nomeEnte = Chiedi("Nome ente (anche parziale)", "Comune di Roma", "Comune di Roma");
        var enti = a.PA.CercaEntePA(nomeEnte);
        P("Enti trovati         → " + enti.Count);
        foreach (var e in enti.Take(8))
            P("  " + e.NomeEnte.PadRight(40) + " IPA: " + e.CodiceIPAUnivoco + "  SdI: " + e.CodiceSdI);
        if (enti.Count > 8) P("  ... e altri " + (enti.Count - 8));
    }

    static void PAASL(Atlante a)
    {
        H("PA — ASL di competenza");
        var cbAsl = Chiedi("Codice Belfiore comune", "F205", "F205");
        var asl = a.PA.OttieniASL(cbAsl);
        P("ASL                  → " + (asl.Item2 ?? asl.Item1 ?? "non trovata"));
    }

    // ── 10. VALIDAZIONE ──────────────────────────────────────────
    static void SnippetValidazione(Atlante a)
    {
        H("VALIDAZIONE");
        Console.WriteLine("  a. Valida Partita IVA");
        Console.WriteLine("  b. Valida IBAN");
        Console.WriteLine("  c. Valida Targa");
        Console.WriteLine("  d. Valida coerenza CAP + Comune + Provincia");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": ValidazionePIVA(a); break;
            case "b": ValidazioneIBAN(a); break;
            case "c": ValidazioneTarga(a); break;
            case "d": ValidazioneCAPComuneProv(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Validazione...");
        Console.ReadLine();
        SnippetValidazione(a);
    }

    static void ValidazionePIVA(Atlante a)
    {
        H("VALIDAZIONE — Partita IVA");
        var piva = Chiedi("Partita IVA (11 cifre)", "02120220158", "02120220158");
        var res = a.Validazione.ValidaPartitaIVA(piva);
        P("Valida               → " + res.IsValida);
        if (res.IsValida) P("Codice ATECO         → " + res.CodiceATECO);
    }

    static void ValidazioneIBAN(Atlante a)
    {
        H("VALIDAZIONE — IBAN");
        var iban = Chiedi("IBAN (es. IT60X...)", "IT60X0542811101000000123456", "IT60X0542811101000000123456");
        var res = a.Validazione.ValidaIBAN(iban);
        P("Valido               → " + res.IsValido);
        if (res.IsValido) P("BIC banca            → " + res.CodiceBIC);
    }

    static void ValidazioneTarga(Atlante a)
    {
        H("VALIDAZIONE — Targa");
        var targa = Chiedi("Targa", "AB123CD", "AB123CD");
        var res = a.Validazione.ValidaTarga(targa.ToUpper());
        P("Valida               → " + res.IsValida);
        if (res.IsValida) P("Formato              → " + res.Formato);
    }

    static void ValidazioneCAPComuneProv(Atlante a)
    {
        H("VALIDAZIONE — CAP + Comune + Provincia");
        var capV = Chiedi("CAP (5 cifre)", "20121", "20121");
        var comuneV = Chiedi("Nome comune", "Milano", "Milano");
        var provV = Chiedi("Sigla provincia (2 lettere)", "MI", "MI");
        var res = a.ValidazioneCross.ValidaCAPComuneProvincia(capV, comuneV, provV.ToUpper());
        P("Coerente             → " + res.IsCoerente);
        if (!res.IsCoerente) P("Motivo               → " + res.Messaggio);
    }

    // ── 11. PARSER INDIRIZZI ─────────────────────────────────────
    static void SnippetParser(Atlante a)
    {
        H("PARSER E CONFRONTO INDIRIZZI");
        Console.WriteLine("  a. Analizza indirizzo (parsing)");
        Console.WriteLine("  b. Confronta due indirizzi");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": ParserAnalizza(a); break;
            case "b": ParserConfronta(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Parser...");
        Console.ReadLine();
        SnippetParser(a);
    }

    static void ParserAnalizza(Atlante a)
    {
        H("PARSER — Analizza indirizzo");
        var ind = Chiedi("Indirizzo da analizzare", "Via Roma 10, 17025 Loano (SV)", "Via Roma 10, 17025 Loano (SV)");
        var parsed = a.Parser.Analizza(ind);
        P("Toponimo             → " + (parsed.Toponimo ?? "n/d"));
        P("Nome via             → " + (parsed.NomeVia ?? "n/d"));
        P("Civico               → " + (parsed.Civico ?? "n/d"));
        P("CAP                  → " + (parsed.CAP ?? "n/d"));
        P("Comune               → " + (parsed.NomeComune ?? "n/d"));
        P("Provincia            → " + (parsed.SiglaProvincia ?? "n/d"));
    }

    static void ParserConfronta(Atlante a)
    {
        H("PARSER — Confronta indirizzi");
        var ind1 = Chiedi("Primo indirizzo", "Via Roma 10, Loano SV", "Via Roma 10, Loano SV");
        var ind2 = Chiedi("Secondo indirizzo", "V. Roma, 10 - Loano (SV)", "V. Roma, 10 - Loano (SV)");
        var conf = a.Confronto.Confronta(ind1, ind2);
        P("Esito                → " + conf.Esito);
        P("Somiglianza          → " + conf.PercentualeTotale.ToString("F1") + "%");
    }

    // ── 12. FESTIVITÀ ────────────────────────────────────────────
    static void SnippetCalendario(Atlante a)
    {
        H("FESTIVITÀ E CALENDARIO");
        Console.WriteLine("  a. Festività nazionali di un anno");
        Console.WriteLine("  b. Verifica se una data è festiva");
        Console.WriteLine("  c. Calcola giorni lavorativi tra due date");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": CalendarioFestivita(a); break;
            case "b": CalendarioIsFestivo(a); break;
            case "c": CalendarioGiorniLavorativi(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Calendario...");
        Console.ReadLine();
        SnippetCalendario(a);
    }

    static void CalendarioFestivita(Atlante a)
    {
        H("CALENDARIO — Festività nazionali");
        var annoStr = Chiedi("Anno", DateTime.Now.Year.ToString(), DateTime.Now.Year.ToString());
        int anno;
        if (!int.TryParse(annoStr, out anno)) anno = DateTime.Now.Year;
        var pasqua = a.Calendario.CalcolaPasqua(anno);
        P("Pasqua " + anno + "          → " + pasqua.ToString("dd/MM/yyyy"));
        var festivita = a.Calendario.OttieniFestività(anno)
            .Where(f => f.Tipo == TipoFestività.Nazionale).ToList();
        P("Festività nazionali  → " + festivita.Count);
        foreach (var f in festivita)
            P("  " + f.Data.ToString("dd/MM") + " — " + f.Nome);
    }

    static void CalendarioIsFestivo(Atlante a)
    {
        H("CALENDARIO — Verifica se una data è festiva");
        var annoCorr = DateTime.Now.Year.ToString();
        var dataStr = Chiedi("Data (dd/MM/yyyy)", "25/12/" + annoCorr, "25/12/" + annoCorr);
        DateTime dataFest;
        if (!DateTime.TryParseExact(dataStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out dataFest))
            dataFest = new DateTime(DateTime.Now.Year, 12, 25);
        P(dataFest.ToString("dd/MM/yyyy") + " è festivo → " + a.Calendario.IsFestivo(dataFest));
    }

    static void CalendarioGiorniLavorativi(Atlante a)
    {
        H("CALENDARIO — Giorni lavorativi");
        var annoCorr = DateTime.Now.Year.ToString();
        var dal = Chiedi("Data inizio (dd/MM/yyyy)", "01/01/" + annoCorr, "01/01/" + annoCorr);
        var al = Chiedi("Data fine   (dd/MM/yyyy)", "31/01/" + annoCorr, "31/01/" + annoCorr);
        DateTime d1, d2;
        if (!DateTime.TryParseExact(dal, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out d1))
            d1 = new DateTime(DateTime.Now.Year, 1, 1);
        if (!DateTime.TryParseExact(al, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out d2))
            d2 = new DateTime(DateTime.Now.Year, 1, 31);
        P("Giorni lavorativi    → " + a.Calendario.CalcolaGiorniLavorativi(d1, d2));
    }

    // ── 13. TELEFONIA ────────────────────────────────────────────
    static void SnippetTelefonia(Atlante a)
    {
        H("TELEFONIA — PREFISSI E OPERATORI");
        Console.WriteLine("  a. Prefisso fisso di un comune");
        Console.WriteLine("  b. Lookup area geografica da prefisso");
        Console.WriteLine("  c. Valida numero telefonico");
        Console.WriteLine("  d. Identifica operatore mobile");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": TelefoniaPrefissoComune(a); break;
            case "b": TelefoniaLookupPrefisso(a); break;
            case "c": TelefoniaValidaNumero(a); break;
            case "d": TelefoniaOperatore(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Telefonia...");
        Console.ReadLine();
        SnippetTelefonia(a);
    }

    static void TelefoniaPrefissoComune(Atlante a)
    {
        H("TELEFONIA — Prefisso fisso di un comune");
        var cb = Chiedi("Codice Belfiore", "F205", "F205");
        var pref = a.Telefonia.OttieniPrefisso(cb);
        P("Prefisso fisso       → " + (pref ?? "n/d"));
    }

    static void TelefoniaLookupPrefisso(Atlante a)
    {
        H("TELEFONIA — Lookup prefisso");
        var prefLook = Chiedi("Prefisso (es. 02, 06, 081)", "02", "02");
        var area = a.Telefonia.DaPrefisso(prefLook);
        P("Area geografica      → " + (area != null ? area.AreaGeografica ?? "n/d" : "non trovato"));
    }

    static void TelefoniaValidaNumero(Atlante a)
    {
        H("TELEFONIA — Valida numero");
        var num = Chiedi("Numero telefonico", "02 1234567", "02 1234567");
        var res = a.Telefonia.Valida(num);
        P("Valido               → " + res.IsValido);
        if (res.IsValido)
        {
            P("Tipo                 → " + (res.Tipo.HasValue ? res.Tipo.Value.ToString() : "n/d"));
            P("Formato E.164        → " + (res.NumeroNormalizzatoE164 ?? "n/d"));
            P("Area / Operatore     → " + (res.AreaGeografica ?? res.NomeOperatore ?? "n/d"));
        }
        else
            P("Anomalie             → " + (res.Anomalie != null ? string.Join(", ", res.Anomalie) : "n/d"));
    }

    static void TelefoniaOperatore(Atlante a)
    {
        H("TELEFONIA — Identifica operatore mobile");
        var mob = Chiedi("Numero mobile (senza +39)", "3331234567", "3331234567");
        var op = a.Telefonia.IdentificaOperatore(mob);
        P("Operatore            → " + (op?.NomeOperatore ?? "sconosciuto"));
    }

    // ── 14. BONIFICA DATI ────────────────────────────────────────
    static void SnippetBonifica(Atlante a)
    {
        H("BONIFICA DATI — PULIZIA DB LEGACY");
        Console.WriteLine("  a. Analizza comune (attivo/soppresso/provincia errata)");
        Console.WriteLine("  b. Verifica sigla provincia");
        Console.WriteLine("  c. Bonifica indirizzo completo");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": BonificaComune(a); break;
            case "b": BonificaSigla(a); break;
            case "c": BonificaIndirizzo(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Bonifica...");
        Console.ReadLine();
        SnippetBonifica(a);
    }

    static void BonificaComune(Atlante a)
    {
        H("BONIFICA — Analizza comune");
        var nomeComune = Chiedi("Nome comune", "Corigliano Calabro", "Corigliano Calabro");
        var siglaProv = Chiedi("Sigla provincia (opzionale, INVIO per saltare)", "CS", "");
        var res = string.IsNullOrEmpty(siglaProv)
            ? a.Bonifica.AnalizzaComune(nomeComune)
            : a.Bonifica.AnalizzaComune(nomeComune, siglaProv.ToUpper());
        if (!res.RequiereCorrezione)
            P("Risultato            → OK, nessuna anomalia");
        else
            P("Risultato            → [" + res.Tipo + "] suggerito: '" + res.ValoreSuggerito + "' (conf. " + res.ConfidenzaSuggerimento.ToString("P0") + ")");
    }

    static void BonificaSigla(Atlante a)
    {
        H("BONIFICA — Verifica sigla provincia");
        Console.WriteLine("  Es. CI = Carbonia-Iglesias, soppressa nel 2016.");
        var sigla = Chiedi("Sigla provincia (2 lettere)", "CI", "CI");
        var res = a.Bonifica.VerificaSiglaProvincia(sigla.ToUpper());
        if (!res.RequiereCorrezione)
            P(sigla.ToUpper() + " → OK, provincia valida");
        else
            P(sigla.ToUpper() + " → soppressa, usare: " + res.ValoreSuggerito + " (" + (res.Motivazione?.Split('.')[0] ?? "") + ")");
    }

    static void BonificaIndirizzo(Atlante a)
    {
        H("BONIFICA — Bonifica indirizzo");
        var bcComune = Chiedi("Comune", "Corigliano Calabro", "Corigliano Calabro");
        var bcCAP = Chiedi("CAP", "87064", "87064");
        var bcProv = Chiedi("Sigla provincia", "CS", "CS");
        var anomalie = a.Bonifica.AnalizzaIndirizzo(bcComune, bcCAP, bcProv.ToUpper(), null);
        if (anomalie.Count == 0)
            P("Risultato            → Nessuna anomalia rilevata");
        else
            foreach (var an in anomalie)
                P("  [" + an.Tipo + "] " + an.CampoProblematico + ": " + an.Motivazione);
    }

    static void StampaBonifica(RisultatoBonifica r, string etichetta)
    {
        if (!r.RequiereCorrezione)
            P(etichetta.PadRight(25) + " → OK");
        else
            P(etichetta.PadRight(25) + " → [" + r.Tipo + "] suggerito: '" + r.ValoreSuggerito + "' (conf. " + r.ConfidenzaSuggerimento.ToString("P0") + ")");
    }

    // ── 15. FRONTALIERI ──────────────────────────────────────────
    static void SnippetFrontalieri(Atlante a)
    {
        H("FRONTALIERI — ZONE DI CONFINE");
        Console.WriteLine("  a. Info frontaliero di un comune");
        Console.WriteLine("  b. Elenco comuni frontalieri per stato");
        Console.WriteLine("  0. Torna al menu principale");
        Console.WriteLine();
        Console.Write("  Scegli: ");
        switch (Console.ReadLine()?.Trim().ToLower())
        {
            case "a": FrontalieriInfo(a); break;
            case "b": FrontalieriPerStato(a); break;
            case "0": return;
            default: P("Scelta non valida."); break;
        }
        Console.WriteLine();
        Console.WriteLine("Premi INVIO per tornare al sotto-menu Frontalieri...");
        Console.ReadLine();
        SnippetFrontalieri(a);
    }

    static void FrontalieriInfo(Atlante a)
    {
        H("FRONTALIERI — Info comune");
        Console.WriteLine("  Rilevante per D.Lgs. 209/2023 (Svizzera/Francia/Austria/Slovenia).");
        var cb = Chiedi("Codice Belfiore", "C933", "C933");
        var info = a.Frontalieri.OttieniInfoFrontalieri(cb);
        var distStr = info.DistanzaConfineKm.HasValue ? info.DistanzaConfineKm.Value.ToString("F1") + " km" : "n/d";
        if (info.IsComuneFrontaliero)
        {
            P("Frontaliero          → SÌ");
            P("Stato confinante     → " + (info.StatoConfinante ?? "n/d"));
            P("Distanza confine     → " + distStr);
            P("Regime fiscale       → " + info.Regime);
            if (info.NoteNormative != null)
            {
                P("Note normative       → " + info.NoteNormative);
                if (info.DataDecorrenza.HasValue)
                    P("Decorrenza           → " + info.DataDecorrenza.Value.ToString("dd/MM/yyyy"));
            }
        }
        else
        {
            P("Frontaliero          → NO");
            P("Distanza confine più vicino → " + distStr);
        }
    }

    static void FrontalieriPerStato(Atlante a)
    {
        H("FRONTALIERI — Comuni per stato confinante");
        Console.WriteLine("  Stati disponibili: Svizzera, Francia, Austria, Slovenia.");
        var stato = Chiedi("Stato confinante", "Svizzera", "Svizzera");
        P("Ricerca in corso (calcolo distanze per tutti i comuni)...");
        var lista = a.Frontalieri.TuttiComuniFrontalieri(stato);
        P("Comuni trovati       → " + lista.Count);
        foreach (var c in lista.Take(15))
        {
            var dist = c.DistanzaConfineKm.HasValue ? c.DistanzaConfineKm.Value.ToString("F1") + " km" : "n/d";
            P("  " + (c.NomeComune ?? c.CodiceBelfiore).PadRight(30) + "  dist: " + dist);
        }
        if (lista.Count > 15) P("  ... e altri " + (lista.Count - 15));
    }

    // ── Helper ───────────────────────────────────────────────────
    static void H(string testo) =>
        Console.WriteLine("\n── " + testo + " " + new string('─', Math.Max(0, 38 - testo.Length)));

    static void P(string testo) =>
        Console.WriteLine("  " + testo);
}
