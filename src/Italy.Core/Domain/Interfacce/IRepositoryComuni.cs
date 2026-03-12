using Italy.Core.Domain.Entità;

namespace Italy.Core.Domain.Interfacce;

/// <summary>
/// Contratto per l'accesso ai dati dei comuni italiani.
/// Tutte le operazioni sono read-only e thread-safe.
/// </summary>
public interface IRepositoryComuni
{
    // ── Ricerca ──────────────────────────────────────────────────────────────
    Comune? DaCodiceBelfiore(string codiceBelfiore);
    Comune? DaCodiceISTAT(string codiceISTAT);
    IReadOnlyList<Comune> Cerca(string testo, int massimo = 10, double sogliaLevenshtein = 0.7);
    IReadOnlyList<Comune> TuttiAttivi();
    IReadOnlyList<Comune> TuttiInclusiSoppressi();

    // ── Gerarchia ────────────────────────────────────────────────────────────
    IReadOnlyList<Comune> DaProvincia(string siglaProvincia);
    IReadOnlyList<Comune> DaRegione(string nomeRegione);
    IReadOnlyList<Comune> DaRipartizione(RipartizioneGeografica ripartizione);

    // ── Risoluzione Storica ─────────────────────────────────────────────────
    Comune? OttieniSuccessore(string codiceBelfiore);
    Comune? OttieniDatiStorici(string codiceBelfiore, DateTime data);
    IReadOnlyList<VariazioneStorica> OttieniStorico(string codiceBelfiore);

    // ── Paginazione ──────────────────────────────────────────────────────────
    IReadOnlyList<Comune> OttieniPagina(int pagina, int dimensione = 100);
    int ContaTotale();
}
