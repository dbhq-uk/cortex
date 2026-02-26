namespace Cortex.Core.Context;

/// <summary>
/// Provides access to business context — the accumulated wisdom store
/// (customer history, meeting notes, past decisions, lessons learned)
/// that enriches orchestration and triage.
/// </summary>
public interface IContextProvider
{
    /// <summary>
    /// Queries the context store using structured filters.
    /// </summary>
    /// <param name="query">The query filters to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching context entries, ordered by creation date descending.</returns>
    Task<IReadOnlyList<ContextEntry>> QueryAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new context entry.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreAsync(
        ContextEntry entry,
        CancellationToken cancellationToken = default);
}
