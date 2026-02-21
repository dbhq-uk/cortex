namespace Cortex.Agents.Delegation;

/// <summary>
/// Universal task lifecycle status, regardless of whether the worker is human or AI.
/// </summary>
public enum DelegationStatus
{
    /// <summary>
    /// Task has been assigned but work has not started.
    /// </summary>
    Assigned = 0,

    /// <summary>
    /// Work is actively in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Work is complete and awaiting review or approval.
    /// </summary>
    AwaitingReview = 2,

    /// <summary>
    /// Task is fully complete.
    /// </summary>
    Complete = 3,

    /// <summary>
    /// Task has exceeded its due date.
    /// </summary>
    Overdue = 4
}
