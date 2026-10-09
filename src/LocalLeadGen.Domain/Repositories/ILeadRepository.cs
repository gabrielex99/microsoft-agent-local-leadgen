using LocalLeadGen.Domain.Entities;

namespace LocalLeadGen.Domain.Repositories;

/// <summary>
/// Contratto di repository per la persistenza e deduplicazione delle entità Lead.
/// </summary>
public interface ILeadRepository
{
    /// <summary>
    /// Verifica se un lead con lo stesso nome e indirizzo/città è già stato analizzato nella finestra temporale indicata.
    /// </summary>
    Task<bool> ExistsAsync(string businessName, string city, TimeSpan duplicateWindow, CancellationToken ct = default);

    /// <summary>
    /// Recupera un lead tramite identificativo.
    /// </summary>
    Task<Lead?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Recupera i lead più recenti ordinati decrescente per data creazione.
    /// </summary>
    Task<IReadOnlyList<Lead>> GetRecentLeadsAsync(int count, CancellationToken ct = default);

    /// <summary>
    /// Aggiunge un nuovo lead al repository.
    /// </summary>
    Task AddAsync(Lead lead, CancellationToken ct = default);

    /// <summary>
    /// Aggiorna un lead esistente nel repository.
    /// </summary>
    Task UpdateAsync(Lead lead, CancellationToken ct = default);

    /// <summary>
    /// Salva le modifiche pendenti sul supporto di persistenza.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
