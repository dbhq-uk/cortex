namespace Cortex.Core.Channels;

/// <summary>
/// Represents a communication channel — a context space where interactions occur.
/// </summary>
public interface IChannel
{
    /// <summary>
    /// Unique identifier for this channel.
    /// </summary>
    string ChannelId { get; }

    /// <summary>
    /// Human-readable name for this channel.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The type of channel.
    /// </summary>
    ChannelType Type { get; }

    /// <summary>
    /// Whether this channel is currently open and accepting messages.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// When this channel was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }
}
