using System.Runtime.CompilerServices;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Validazione Codice Fiscale zero-allocation tramite Span&lt;char&gt; e ReadOnlySpan&lt;T&gt;.
/// Compatibile net8.0 con Native AOT. Fallback su net48 con path classico.
///
/// Usare questo validatore nei hot-path ad alta frequenza (migliaia/secondo).
/// Per uso generale con lookup comuni usare <see cref="ServiziCodiceFiscale"/>.
/// </summary>
public static class ValidatoreCFSpan
{
    // Tabella valori dispari precomputata (ASCII index 48='0' ... 90='Z')
    private static ReadOnlySpan<byte> ValoriDispari => new byte[]
    {
        //  0   1   2   3   4   5   6   7   8   9
            1,  0,  5,  7,  9, 13, 15, 17, 19, 21,  // '0'-'9' (idx 48-57)
        //  :   ;   <   =   >   ?   @
            0,  0,  0,  0,  0,  0,  0,              // (idx 58-64)
        //  A   B   C   D   E   F   G   H   I   J   K   L   M   N   O   P   Q   R   S   T   U   V   W   X   Y   Z
            1,  0,  5,  7,  9, 13, 15, 17, 19, 21,  2,  4, 18, 20, 11,  3,  6,  8, 12, 14, 16, 10, 22, 25, 24, 23
    };

    /// <summary>
    /// Verifica il carattere di controllo di un Codice Fiscale senza allocare stringhe.
    /// Gestisce sia CF di 16 caratteri che omocodici.
    ///
    /// Complessità: O(16) — zero heap allocation.
    /// </summary>
#if NET8_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public static bool IsValidoRapido(ReadOnlySpan<char> codiceFiscale)
    {
        if (codiceFiscale.Length != 16) return false;

        var somma = 0;
        var tabella = ValoriDispari;

        for (var i = 0; i < 15; i++)
        {
            var c = char.ToUpperInvariant(codiceFiscale[i]);
            int valore;

            if (i % 2 == 0) // posizioni dispari (1,3,5... → indice 0,2,4...)
            {
                // Tabella lookup: offset da '0' (48) o da 'A' (65)
                var idx = c >= 'A' ? c - 'A' + 10 : c - '0';
                if (idx < 0 || idx >= tabella.Length) return false;

#if NET8_0_OR_GREATER
                valore = Unsafe.Add(ref System.Runtime.InteropServices.MemoryMarshal
                    .GetReference(tabella), idx);
#else
                valore = tabella[idx];
#endif
            }
            else // posizioni pari
            {
                valore = c >= 'A' ? c - 'A' : c - '0';
            }

            somma += valore;
        }

        return (char)('A' + somma % 26) == char.ToUpperInvariant(codiceFiscale[15]);
    }

    /// <summary>
    /// Estrae il Codice Belfiore (4 char, posizione 11-14) senza allocazioni.
    /// Restituisce uno slice del ReadOnlySpan originale.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> EstraiBelfiore(ReadOnlySpan<char> codiceFiscale)
    {
        if (codiceFiscale.Length != 16) return ReadOnlySpan<char>.Empty;
        return codiceFiscale.Slice(11, 4);
    }

    /// <summary>
    /// Verifica rapidamente se un CF appartiene a una persona nata all'estero
    /// (il Belfiore inizia con 'Z').
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNatoAllEstero(ReadOnlySpan<char> codiceFiscale)
    {
        if (codiceFiscale.Length != 16) return false;
        return char.ToUpperInvariant(codiceFiscale[11]) == 'Z';
    }

    /// <summary>
    /// Normalizza un CF in-place su un buffer pre-allocato.
    /// Ideale per pipeline di bonifica a bassa latenza.
    /// </summary>
    public static bool TentaNormalizza(ReadOnlySpan<char> input, Span<char> output)
    {
        if (output.Length < 16) return false;

        var j = 0;
        for (var i = 0; i < input.Length && j < 16; i++)
        {
            var c = input[i];
            if (c == ' ' || c == '-') continue;
            output[j++] = char.ToUpperInvariant(c);
        }

        return j == 16;
    }

    /// <summary>
    /// Versione compatibile net48 (stringa classica).
    /// Nel compilatore il JIT inline-verifica la condizione a compile-time.
    /// </summary>
    public static bool IsValido(string codiceFiscale)
    {
#if NET8_0_OR_GREATER
        return IsValidoRapido(codiceFiscale.AsSpan());
#else
        if (codiceFiscale == null || codiceFiscale.Length != 16) return false;
        return IsValidoRapido(codiceFiscale.AsSpan());
#endif
    }
}
