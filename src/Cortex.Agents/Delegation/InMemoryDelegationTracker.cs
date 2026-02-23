using Cortex.Core.References;

namespace Cortex.Agents.Delegation;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IDelegationTracker"/>.
/// </summary>
public sealed class InMemoryDelegationTracker : IDelegationTracker
{
    /// <inheritdoc />
    public Task DelegateAsync(DelegationRecord record, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task UpdateStatusAsync(ReferenceCode referenceCode, DelegationStatus status, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<DelegationRecord>> GetByAssigneeAsync(string agentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<DelegationRecord>> GetOverdueAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
