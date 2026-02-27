namespace Cortex.Core.Messages;

/// <summary>
/// Simple text-based message for general communication.
/// </summary>
public sealed record TextMessage(string Content) : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }
}
