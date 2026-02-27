using Cortex.Core.References;

namespace Cortex.Agents.Supervision;

/// <summary>
/// Tracks retry counts for overdue delegations. Separate from DelegationRecord
/// to keep records immutable and counters operational.
/// </summary>
public interface IRetryCounter
{
    /// <summary>
    /// Increments the retry count for the given delegation and returns the new count.
    /// </summary>
    Task<int> IncrementAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current retry count for the given delegation.
    /// </summary>
    Task<int> GetCountAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the retry count for the given delegation to zero.
    /// </summary>
    Task ResetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);
}
