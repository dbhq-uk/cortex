using System.Collections.Concurrent;
using Cortex.Core.References;

namespace Cortex.Agents;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IPendingPlanStore"/>.
/// </summary>
public sealed class InMemoryPendingPlanStore : IPendingPlanStore
{
    private readonly ConcurrentDictionary<string, PendingPlan> _plans = new();

    /// <inheritdoc />
    public Task StoreAsync(ReferenceCode referenceCode, PendingPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _plans[referenceCode.Value] = plan;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PendingPlan?> GetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        _plans.TryGetValue(referenceCode.Value, out var plan);
        return Task.FromResult(plan);
    }

    /// <inheritdoc />
    public Task RemoveAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        _plans.TryRemove(referenceCode.Value, out _);
        return Task.CompletedTask;
    }
}
