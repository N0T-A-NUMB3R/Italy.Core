namespace Italy.Core.Domain.Entità;

public class AtlanteException : Exception
{
    public AtlanteException(string message) : base(message) { }
    public AtlanteException(string message, Exception inner) : base(message, inner) { }
}

public class CodiceBelfioreNonTrovatoException : AtlanteException
{
    public string CodiceBelfiore { get; }
    public CodiceBelfioreNonTrovatoException(string codiceBelfiore)
        : base($"Codice Belfiore '{codiceBelfiore}' non trovato nel database Atlante.")
    {
        CodiceBelfiore = codiceBelfiore;
    }
}

public class DatabaseAtlanteException : AtlanteException
{
    public DatabaseAtlanteException(string message) : base(message) { }
    public DatabaseAtlanteException(string message, Exception inner) : base(message, inner) { }
}

public class DatabaseNonInizializzatoException : AtlanteException
{
    public DatabaseNonInizializzatoException()
        : base("Il database Atlante non è stato inizializzato. Chiamare Atlante.Inizializza() prima dell'uso.") { }
}
