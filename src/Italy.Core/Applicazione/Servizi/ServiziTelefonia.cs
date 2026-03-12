using Italy.Core.Domain.Entità;
using Italy.Core.Domain.Interfacce;

namespace Italy.Core.Applicazione.Servizi;

/// <summary>
/// Servizi per i prefissi telefonici italiani (fissi, mobili, speciali).
/// Basato sul Piano Nazionale di Numerazione AGCOM.
/// </summary>
public sealed class ServiziTelefonia : IProviderTelefonia
{
    private readonly IProviderTelefonia _repository;

    public ServiziTelefonia(IProviderTelefonia repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public PrefissoTelefonico? DaPrefisso(string prefisso) =>
        _repository.DaPrefisso(prefisso);

    public string? OttieniPrefisso(string codiceBelfiore) =>
        _repository.OttieniPrefisso(codiceBelfiore);

    public OperatoreMobile? IdentificaOperatore(string numero) =>
        _repository.IdentificaOperatore(numero);

    /// <summary>
    /// Valida e normalizza un numero di telefono italiano in formato E.164.
    /// </summary>
    public RisultatoNumeroTelefonico Valida(string numero)
    {
        if (string.IsNullOrWhiteSpace(numero))
            return new RisultatoNumeroTelefonico { IsValido = false, Anomalie = ["Numero non può essere vuoto."] };

        var pulito = new string(numero.Where(c => char.IsDigit(c) || c == '+').ToArray());

        // Rimuovi prefisso internazionale Italia
        if (pulito.StartsWith("+39")) pulito = pulito[3..];
        else if (pulito.StartsWith("0039")) pulito = pulito[4..];

        if (pulito.Length < 6 || pulito.Length > 11)
            return new RisultatoNumeroTelefonico
            {
                IsValido = false,
                Anomalie = [$"Lunghezza non valida: {pulito.Length} cifre."]
            };

        // Identifica tipo
        TipoPrefisso tipo;
        string? area = null;
        string? operatore = null;

        if (pulito.StartsWith("800") || pulito.StartsWith("803"))
            tipo = TipoPrefisso.TollFree;
        else if (pulito.StartsWith("89") || pulito.StartsWith("166"))
            tipo = TipoPrefisso.Premium;
        else if (pulito.StartsWith("11") || pulito.StartsWith("118"))
            tipo = TipoPrefisso.Emergenza;
        else if (pulito[0] == '3')
        {
            tipo = TipoPrefisso.Mobile;
            var op = _repository.IdentificaOperatore(pulito);
            operatore = op?.NomeOperatore;
        }
        else
        {
            tipo = TipoPrefisso.Geografico;
            var pref = _repository.DaPrefisso(pulito[..2]) ?? _repository.DaPrefisso(pulito[..3]);
            area = pref?.AreaGeografica;
        }

        return new RisultatoNumeroTelefonico
        {
            IsValido = true,
            NumeroNormalizzatoE164 = "+39" + pulito,
            Tipo = tipo,
            AreaGeografica = area,
            NomeOperatore = operatore,
            Prefisso = pulito.Length >= 2 ? pulito[..2] : null
        };
    }
}
