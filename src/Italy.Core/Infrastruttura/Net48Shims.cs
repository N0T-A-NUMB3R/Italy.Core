// Shim per abilitare le funzionalità C# 9+ su .NET Framework 4.8.
// Su net8+ questi tipi sono già presenti nel runtime.
#if NETFRAMEWORK

namespace System.Runtime.CompilerServices
{
    /// <summary>Shim per i record e init accessor C# 9+ su .NET Framework.</summary>
    internal static class IsExternalInit { }
}

/// <summary>
/// Extension methods per colmare il gap API tra net8 e net48.
/// </summary>
internal static class Net48DictionaryExtensions
{
    // Dictionary<K,V>.GetValueOrDefault(key) non esiste in net48
    public static TValue GetValueOrDefault<TKey, TValue>(
        this System.Collections.Generic.Dictionary<TKey, TValue> dict,
        TKey key,
        TValue defaultValue = default!)
        where TKey : notnull
    {
        return dict.TryGetValue(key, out var value) ? value : defaultValue;
    }
}

#endif
