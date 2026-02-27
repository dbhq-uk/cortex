using Cortex.Core.References;

namespace Cortex.Agents;

/// <summary>
/// No-op pending plan store for backward compatibility when approval gating is not needed.
/// </summary>
internal sealed class NullPendingPlanStore : IPendingPlanStore
{
    /// <inheritdoc />
    public Task StoreAsync(ReferenceCode referenceCode, PendingPlan plan, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task<PendingPlan?> GetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<PendingPlan?>(null);

    /// <inheritdoc />
    public Task RemoveAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
