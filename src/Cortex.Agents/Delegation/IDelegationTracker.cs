using Cortex.Core.References;

namespace Cortex.Agents.Delegation;

/// <summary>
/// Tracks and manages delegated tasks across the system.
/// </summary>
public interface IDelegationTracker
{
    /// <summary>
    /// Records a new delegation.
    /// </summary>
    Task DelegateAsync(DelegationRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of an existing delegation.
    /// </summary>
    Task UpdateStatusAsync(ReferenceCode referenceCode, DelegationStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all delegations assigned to a specific agent.
    /// </summary>
    Task<IReadOnlyList<DelegationRecord>> GetByAssigneeAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all delegations that are past their due date.
    /// </summary>
    Task<IReadOnlyList<DelegationRecord>> GetOverdueAsync(CancellationToken cancellationToken = default);
}
