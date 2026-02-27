using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Core.Workflows;

/// <summary>
/// Tracks a decomposed goal as a coordinated unit of work.
/// The parent reference code identifies the workflow; each sub-task has its own reference code.
/// </summary>
public sealed record WorkflowRecord
{
    /// <summary>
    /// Parent reference code for the entire workflow.
    /// </summary>
    public required ReferenceCode ReferenceCode { get; init; }

    /// <summary>
    /// The original inbound envelope, preserved for ReplyTo and context.
    /// </summary>
    public required MessageEnvelope OriginalEnvelope { get; init; }

    /// <summary>
    /// Reference codes for each sub-task in the workflow.
    /// </summary>
    public required IReadOnlyList<ReferenceCode> SubtaskReferenceCodes { get; init; }

    /// <summary>
    /// Human-readable summary of the decomposed goal.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Current status of the workflow.
    /// </summary>
    public WorkflowStatus Status { get; init; } = WorkflowStatus.InProgress;

    /// <summary>
    /// When the workflow was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the workflow completed, if applicable.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }
}
