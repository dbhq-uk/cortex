using Cortex.Core.References;

namespace Cortex.Agents.Delegation;

/// <summary>
/// Tracks a delegated task — what's been assigned to whom and its current status.
/// </summary>
public sealed record DelegationRecord
{
    /// <summary>
    /// Reference code for tracking this delegation.
    /// </summary>
    public required ReferenceCode ReferenceCode { get; init; }

    /// <summary>
    /// Agent or user who delegated this task.
    /// </summary>
    public required string DelegatedBy { get; init; }

    /// <summary>
    /// Agent or user this task is delegated to.
    /// </summary>
    public required string DelegatedTo { get; init; }

    /// <summary>
    /// Description of the delegated task.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Current status of this delegation.
    /// </summary>
    public required DelegationStatus Status { get; init; }

    /// <summary>
    /// When this task was assigned.
    /// </summary>
    public required DateTimeOffset AssignedAt { get; init; }

    /// <summary>
    /// When this task is due, if there is a deadline.
    /// </summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>
    /// When this task was completed, if it has been.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }
}
