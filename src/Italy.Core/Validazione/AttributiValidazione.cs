using System.ComponentModel.DataAnnotations;
using Italy.Core.Applicazione.Servizi;

namespace Italy.Core.Validazione;

/// <summary>Valida un Codice Fiscale italiano tramite l'algoritmo ufficiale ADE.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CodiceFiscaleValidoAttribute : ValidationAttribute
{
    private static readonly ServiziCodiceFiscale _servizi =
        new(new Infrastruttura.Repository.RepositoryComuni(new Infrastruttura.DatabaseAtlante()));

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string cf || string.IsNullOrWhiteSpace(cf))
            return new ValidationResult("Il Codice Fiscale non può essere vuoto.");

        var risultato = _servizi.Valida(cf);
        return risultato.IsValido
            ? ValidationResult.Success
            : new ValidationResult($"Codice Fiscale non valido: {string.Join("; ", risultato.Anomalie)}");
    }
}

/// <summary>Valida un CAP italiano (5 cifre).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CAPValidoAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string cap) return ValidationResult.Success;
        if (cap.Length != 5 || !cap.All(char.IsDigit))
            return new ValidationResult($"'{cap}' non è un CAP valido. Formato atteso: 5 cifre.");
        return ValidationResult.Success;
    }
}

/// <summary>Valida un Codice Belfiore (4 caratteri: 1 lettera + 3 cifre).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CodiceBelfioreValidoAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string cb || string.IsNullOrWhiteSpace(cb))
            return ValidationResult.Success;

        if (cb.Length != 4 || !char.IsLetter(cb[0]) || !cb.Skip(1).All(char.IsDigit))
            return new ValidationResult($"'{cb}' non è un Codice Belfiore valido. Formato atteso: 1 lettera + 3 cifre (es. F205).");

        return ValidationResult.Success;
    }
}

/// <summary>Valida una Partita IVA italiana (11 cifre, algoritmo Luhn).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PartitaIVAValidaAttribute : ValidationAttribute
{
    private static readonly ServiziValidazione _servizi = new();

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string piva || string.IsNullOrWhiteSpace(piva))
            return ValidationResult.Success;

        var risultato = _servizi.ValidaPartitaIVA(piva);
        return risultato.IsValida
            ? ValidationResult.Success
            : new ValidationResult($"Partita IVA non valida: {string.Join("; ", risultato.Anomalie)}");
    }
}

/// <summary>Valida un IBAN italiano.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class IBANValidoAttribute : ValidationAttribute
{
    private static readonly ServiziValidazione _servizi = new();

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string iban || string.IsNullOrWhiteSpace(iban))
            return ValidationResult.Success;

        var risultato = _servizi.ValidaIBAN(iban);
        return risultato.IsValido
            ? ValidationResult.Success
            : new ValidationResult($"IBAN non valido: {string.Join("; ", risultato.Anomalie)}");
    }
}

/// <summary>Valida una targa automobilistica italiana.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TargaValidaAttribute : ValidationAttribute
{
    private static readonly ServiziValidazione _servizi = new();

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not string targa || string.IsNullOrWhiteSpace(targa))
            return ValidationResult.Success;

        var risultato = _servizi.ValidaTarga(targa);
        return risultato.IsValida
            ? ValidationResult.Success
            : new ValidationResult(risultato.Note ?? "Targa non valida.");
    }
}
