using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Proposes a decomposition plan for human approval. Sent when AskMeFirst authority is in effect.
/// </summary>
public sealed record PlanProposal : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Brief summary of the proposed plan.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Descriptions of each proposed task in the plan.
    /// </summary>
    public required IReadOnlyList<string> TaskDescriptions { get; init; }

    /// <summary>
    /// The original goal that triggered this plan.
    /// </summary>
    public required string OriginalGoal { get; init; }

    /// <summary>
    /// Reference code for the workflow this plan belongs to.
    /// </summary>
    public required ReferenceCode WorkflowReferenceCode { get; init; }
}
