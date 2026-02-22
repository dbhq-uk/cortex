using Cortex.Core.Messages;

namespace Cortex.Messaging.Tests;

/// <summary>
/// Simple concrete IMessage for unit testing.
/// </summary>
public sealed record TestMessage : IMessage
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Test payload content.
    /// </summary>
    public required string Content { get; init; }
}
