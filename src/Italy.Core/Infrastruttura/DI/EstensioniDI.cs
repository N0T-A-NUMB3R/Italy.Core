using Italy.Core.Applicazione.Servizi;
using Italy.Core.Domain.Interfacce;
using Italy.Core.Infrastruttura;
using Italy.Core.Infrastruttura.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace Italy.Core.Infrastruttura.DI;

/// <summary>
/// Estensioni per la registrazione di Italy.Core nel container DI di ASP.NET Core.
/// </summary>
public static class EstensioniDI
{
    /// <summary>
    /// Registra tutti i servizi Italy.Core nel container DI.
    ///
    /// Utilizzo in Program.cs:
    /// <code>
    /// builder.Services.AddItalyCore();
    /// </code>
    /// </summary>
    public static IServiceCollection AddItalyCore(
        this IServiceCollection services,
        Action<OpzioniItalyCore>? configura = null)
    {
        var opzioni = new OpzioniItalyCore();
        configura?.Invoke(opzioni);

        // Database singleton (costoso da inizializzare, condiviso)
        services.AddSingleton<DatabaseAtlante>();

        // Repository
        services.AddSingleton<IRepositoryComuni, RepositoryComuni>();
        services.AddSingleton<IRepositoryCAP, RepositoryCAP>();
        services.AddSingleton<IProviderTelefonia, RepositoryTelefonia>();

        // Provider con possibilità di override
        if (opzioni.TipoProviderFestività != null)
            services.AddSingleton(typeof(IProviderFestività), opzioni.TipoProviderFestività);
        else
            services.AddSingleton<IProviderFestività, ServiziFestività>();

        if (opzioni.TipoProviderGeografico != null)
            services.AddSingleton(typeof(IProviderGeografico), opzioni.TipoProviderGeografico);
        else
            services.AddSingleton<IProviderGeografico, ServiziGeo>();

        // Servizi applicativi
        services.AddSingleton<ServiziComuni>();
        services.AddSingleton<ServiziCAP>();
        services.AddSingleton<ServiziCodiceFiscale>();
        services.AddSingleton<ServiziCodiceFiscalePG>();
        services.AddSingleton<ServiziValidazione>();
        services.AddSingleton<ServiziFestività>();
        services.AddSingleton<ServiziGeo>();
        services.AddSingleton<ServiziTelefonia>();
        services.AddSingleton<ServiziParserIndirizzi>();
        services.AddSingleton<ServiziBonificaDati>();
        services.AddSingleton<ServiziFrontalieri>();
        services.AddSingleton<ServiziPA>();
        services.AddSingleton<ServiziTimeMachine>();
        services.AddSingleton<ServiziValidazioneCross>();
        services.AddSingleton<ServiziConfrontoIndirizzi>();

        // Entry point principale
        services.AddSingleton<Atlante>();

        return services;
    }

    /// <summary>
    /// Aggiunge i validatori di annotazioni dati (DataAnnotations) per modelli ASP.NET.
    /// </summary>
    public static IServiceCollection AddItalyCoreValidation(this IServiceCollection services)
    {
        // I validatori [CodiceFiscaleValido], [CAPValido], ecc. sono automaticamente
        // riconosciuti da ASP.NET Core tramite il meccanismo di ValidationAttribute.
        // Nessuna registrazione esplicita necessaria.
        return services;
    }
}

/// <summary>Opzioni di configurazione per Italy.Core.</summary>
public sealed class OpzioniItalyCore
{
    internal Type? TipoProviderFestività { get; private set; }
    internal Type? TipoProviderGeografico { get; private set; }

    /// <summary>Sostituisce il provider default per le festività con un'implementazione personalizzata.</summary>
    public OpzioniItalyCore UsaProviderFestività<T>() where T : class, IProviderFestività
    {
        TipoProviderFestività = typeof(T);
        return this;
    }

    /// <summary>Sostituisce il provider default per i servizi geografici.</summary>
    public OpzioniItalyCore UsaProviderGeografico<T>() where T : class, IProviderGeografico
    {
        TipoProviderGeografico = typeof(T);
        return this;
    }
}
