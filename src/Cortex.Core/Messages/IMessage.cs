namespace Cortex.Core.Messages;

/// <summary>
/// Base contract for all messages in the Cortex system.
/// </summary>
public interface IMessage
{
    /// <summary>
    /// Unique identifier for this message instance.
    /// </summary>
    string MessageId { get; }

    /// <summary>
    /// When this message was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Optional correlation ID linking related messages.
    /// </summary>
    string? CorrelationId { get; }
}
