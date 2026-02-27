using System.Collections.Concurrent;

namespace Cortex.Core.Email;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ISubscriptionStore"/>
/// for unit testing and local development.
/// </summary>
public sealed class InMemorySubscriptionStore : ISubscriptionStore
{
    private readonly ConcurrentDictionary<string, SubscriptionRecord> _subscriptions = new();

    /// <inheritdoc />
    public Task StoreAsync(SubscriptionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        _subscriptions[record.SubscriptionId] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SubscriptionRecord>> GetExpiringAsync(TimeSpan withinWindow, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.Add(withinWindow);
        var expiring = _subscriptions.Values
            .Where(r => r.ExpiresAt <= cutoff)
            .ToList();

        return Task.FromResult<IReadOnlyList<SubscriptionRecord>>(expiring);
    }

    /// <inheritdoc />
    public Task UpdateExpiryAsync(string subscriptionId, DateTimeOffset newExpiry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        if (_subscriptions.TryGetValue(subscriptionId, out var existing))
        {
            _subscriptions[subscriptionId] = existing with { ExpiresAt = newExpiry };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        _subscriptions.TryRemove(subscriptionId, out _);
        return Task.CompletedTask;
    }
}
