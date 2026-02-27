using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Alert published when a delegation is overdue. Sent to the CoS for re-dispatch.
/// </summary>
public sealed record SupervisionAlert : IMessage
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

    /// <summary>Description of the overdue task.</summary>
    public required string Description { get; init; }

    /// <summary>Number of times this delegation has been retried.</summary>
    public required int RetryCount { get; init; }

    /// <summary>When the delegation was due.</summary>
    public required DateTimeOffset DueAt { get; init; }

    /// <summary>Whether the delegated agent is currently running.</summary>
    public required bool IsAgentRunning { get; init; }
}
