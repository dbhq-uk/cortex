using System.Collections.Concurrent;
using Cortex.Core.References;

namespace Cortex.Agents.Supervision;

/// <summary>
/// In-memory implementation of <see cref="IRetryCounter"/> for testing and local development.
/// </summary>
public sealed class InMemoryRetryCounter : IRetryCounter
{
    private readonly ConcurrentDictionary<string, int> _counts = new();

    /// <inheritdoc />
    public Task<int> IncrementAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        var newCount = _counts.AddOrUpdate(referenceCode.Value, 1, (_, c) => c + 1);
        return Task.FromResult(newCount);
    }

    /// <inheritdoc />
    public Task<int> GetCountAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        var count = _counts.GetValueOrDefault(referenceCode.Value, 0);
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task ResetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        _counts.TryRemove(referenceCode.Value, out _);
        return Task.CompletedTask;
    }
}
