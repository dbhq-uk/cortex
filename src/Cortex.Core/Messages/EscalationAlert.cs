using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Alert published when a delegation has exceeded maximum retry attempts. Requires human intervention.
/// </summary>
public sealed record EscalationAlert : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>Reference code of the overdue delegation.</summary>
    public required ReferenceCode DelegationReferenceCode { get; init; }

    /// <summary>Agent the task was delegated to.</summary>
    public required string DelegatedTo { get; init; }

    /// <summary>Description of the escalated task.</summary>
    public required string Description { get; init; }

    /// <summary>Number of times this delegation was retried before escalation.</summary>
    public required int RetryCount { get; init; }

    /// <summary>Reason for escalation.</summary>
    public required string Reason { get; init; }
}
