using Cortex.Core.Messages;

namespace Cortex.Messaging;

/// <summary>
/// Maps a queue name to its routing rules.
/// </summary>
public sealed record QueueBinding
{
    /// <summary>
    /// The name of the queue.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// Routing pattern for message matching.
    /// </summary>
    public required string RoutingPattern { get; init; }

    /// <summary>
    /// Optional channel filter — only messages from this channel are routed here.
    /// </summary>
    public string? ChannelId { get; init; }

    /// <summary>
    /// Optional agent filter — only messages for this agent are routed here.
    /// </summary>
    public string? AgentId { get; init; }

    /// <summary>
    /// Priority of this binding when multiple bindings match.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;
}
