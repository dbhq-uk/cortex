using System.Collections.Concurrent;

namespace Cortex.Core.Email;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IEmailDeduplicationStore"/>
/// for unit testing and local development.
/// </summary>
public sealed class InMemoryEmailDeduplicationStore : IEmailDeduplicationStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var exists = _seen.ContainsKey(externalId);
        return Task.FromResult(exists);
    }

    /// <inheritdoc />
    public Task MarkSeenAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        _seen.TryAdd(externalId, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
