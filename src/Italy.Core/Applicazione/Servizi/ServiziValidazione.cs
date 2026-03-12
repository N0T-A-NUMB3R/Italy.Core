using Italy.Core.Domain.Entità;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Validazioni di codici italiani: Partita IVA, IBAN, Targa automobilistica.
/// </summary>
public sealed class ServiziValidazione
{
    // ── Partita IVA ──────────────────────────────────────────────────────────

    /// <summary>
    /// Valida una Partita IVA italiana con algoritmo di Luhn.
    /// </summary>
    public RisultatoPartitaIVA ValidaPartitaIVA(string partitaIVA)
    {
        var anomalie = new List<string>();
        if (string.IsNullOrWhiteSpace(partitaIVA))
            return new RisultatoPartitaIVA { IsValida = false, Anomalie = ["La Partita IVA non può essere vuota."] };

        var piva = partitaIVA.Trim().Replace(" ", "");
        if (piva.Length != 11 || !piva.All(char.IsDigit))
            return new RisultatoPartitaIVA { IsValida = false, Anomalie = [$"Formato non valido: '{piva}' (attesi 11 cifre)."] };

        // Algoritmo Luhn adattato
        var somma = 0;
        for (var i = 0; i < 10; i++)
        {
            var cifra = piva[i] - '0';
            if (i % 2 == 1)
            {
                cifra *= 2;
                if (cifra > 9) cifra -= 9;
            }
            somma += cifra;
        }
        var controllo = (10 - somma % 10) % 10;
        if (controllo != piva[10] - '0')
            anomalie.Add("Cifra di controllo non valida.");

        var codiceComune = piva.Substring(7, 3);
        var siglaProvincia = LookupSiglaDaCodiceCameraCommercio(codiceComune);

        return new RisultatoPartitaIVA
        {
            IsValida = anomalie.Count == 0,
            ProvinciaSede = siglaProvincia,
            Anomalie = anomalie
        };
    }

    // ── IBAN Italiano ────────────────────────────────────────────────────────

    /// <summary>
    /// Valida un IBAN italiano (IT + 2 check digits + 23 BBAN chars).
    /// </summary>
    public RisultatoIBAN ValidaIBAN(string iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return new RisultatoIBAN { IsValido = false, Anomalie = ["IBAN non può essere vuoto."] };

        var ibanNorm = iban.Trim().Replace(" ", "").ToUpperInvariant();

        if (!ibanNorm.StartsWith("IT"))
            return new RisultatoIBAN { IsValido = false, Anomalie = [$"Non è un IBAN italiano (atteso 'IT', trovato '{ibanNorm[..2]}')."] };

        if (ibanNorm.Length != 27)
            return new RisultatoIBAN { IsValido = false, Anomalie = [$"Lunghezza errata: {ibanNorm.Length} (atteso 27 per IBAN IT)."] };

        // Spostamento e verifica mod 97
        var rearranged = ibanNorm[4..] + ibanNorm[..4];
        var numericString = string.Concat(rearranged.Select(c =>
            char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString()));

        if (!VerificaMod97(numericString))
            return new RisultatoIBAN { IsValido = false, Anomalie = ["Verifica MOD 97 fallita."] };

        // Formattazione
        var gruppi = Enumerable.Range(0, ibanNorm.Length / 4 + (ibanNorm.Length % 4 > 0 ? 1 : 0))
            .Select(i => ibanNorm.Substring(i * 4, Math.Min(4, ibanNorm.Length - i * 4)));
        var ibanFormattato = string.Join(" ", gruppi);

        return new RisultatoIBAN
        {
            IsValido = true,
            IBAN_Formattato = ibanFormattato,
            FilialeCodice = ibanNorm.Substring(10, 5)
        };
    }

    // ── Targa Automobilistica ────────────────────────────────────────────────

    public RisultatoTarga ValidaTarga(string targa)
    {
        if (string.IsNullOrWhiteSpace(targa))
            return new RisultatoTarga { IsValida = false };

        var t = targa.Trim().Replace(" ", "").ToUpperInvariant();

        // Formato attuale: 2 lettere + 3 cifre + 2 lettere (post 1994)
        if (t.Length == 7
            && char.IsLetter(t[0]) && char.IsLetter(t[1])
            && char.IsDigit(t[2]) && char.IsDigit(t[3]) && char.IsDigit(t[4])
            && char.IsLetter(t[5]) && char.IsLetter(t[6]))
        {
            return new RisultatoTarga { IsValida = true, Formato = FormatoTarga.Attuale };
        }

        // Formati speciali
        if (t.StartsWith("CD")) return new RisultatoTarga { IsValida = true, Formato = FormatoTarga.CD, Note = "Corpo Diplomatico" };
        if (t.StartsWith("SCV")) return new RisultatoTarga { IsValida = true, Formato = FormatoTarga.SCV, Note = "Stato della Città del Vaticano" };
        if (t.StartsWith("EE")) return new RisultatoTarga { IsValida = true, Formato = FormatoTarga.EE, Note = "Targa straniera temporanea" };

        return new RisultatoTarga { IsValida = false, Note = $"Formato targa non riconosciuto: '{t}'" };
    }

    // ── Helper Privati ────────────────────────────────────────────────────────

    private static bool VerificaMod97(string numerica)
    {
        var resto = 0;
        foreach (var c in numerica)
        {
            resto = (resto * 10 + (c - '0')) % 97;
        }
        return resto == 1;
    }

    private static string? LookupSiglaDaCodiceCameraCommercio(string codice) =>
        // Lookup semplificato - in produzione usare tabella DB
        codice switch
        {
            "001" => "AG", "002" => "AL", "003" => "AN", "004" => "AO",
            "201" => "MI", "401" => "RM", "101" => "NA",
            _ => null
        };
}
