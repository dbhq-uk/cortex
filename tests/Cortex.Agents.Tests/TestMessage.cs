using Cortex.Core.Messages;

namespace Cortex.Agents.Tests;

/// <summary>
/// Simple message type for testing agent harness behaviour.
/// </summary>
public sealed record TestMessage : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Test content payload.
    /// </summary>
    public required string Content { get; init; }
}
