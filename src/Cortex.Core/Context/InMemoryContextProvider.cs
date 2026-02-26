using System.Collections.Concurrent;

namespace Cortex.Core.Context;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IContextProvider"/>
/// for unit testing and local development.
/// </summary>
public sealed class InMemoryContextProvider : IContextProvider
{
    private readonly ConcurrentDictionary<string, ContextEntry> _entries = new();

    /// <inheritdoc />
    public Task StoreAsync(ContextEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.EntryId] = entry;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ContextEntry>> QueryAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = _entries.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(query.Keywords))
        {
            results = results.Where(e =>
                e.Content.Contains(query.Keywords, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Category.HasValue)
        {
            results = results.Where(e => e.Category == query.Category.Value);
        }

        if (query.Tags is { Count: > 0 })
        {
            results = results.Where(e =>
                e.Tags.Any(t => query.Tags.Contains(t)));
        }

        if (query.ReferenceCode.HasValue)
        {
            results = results.Where(e =>
                e.ReferenceCode.HasValue && e.ReferenceCode.Value == query.ReferenceCode.Value);
        }

        var ordered = results.OrderByDescending(e => e.CreatedAt);

        IReadOnlyList<ContextEntry> list = query.MaxResults.HasValue
            ? ordered.Take(query.MaxResults.Value).ToList()
            : ordered.ToList();

        return Task.FromResult(list);
    }
}
