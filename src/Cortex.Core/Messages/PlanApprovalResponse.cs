using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Human response to a PlanProposal. Approves or rejects the proposed plan.
/// </summary>
public sealed record PlanApprovalResponse : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Whether the plan was approved.
    /// </summary>
    public required bool IsApproved { get; init; }

    /// <summary>
    /// Reason for rejection, if not approved.
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// Reference code of the workflow whose plan is being responded to.
    /// </summary>
    public required ReferenceCode WorkflowReferenceCode { get; init; }
}
