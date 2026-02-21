namespace Cortex.Core.Messages;

/// <summary>
/// Contextual information that travels with a message through the system.
/// </summary>
public sealed record MessageContext
{
    /// <summary>
    /// The message ID of the parent message that spawned this one.
    /// </summary>
    public string? ParentMessageId { get; init; }

    /// <summary>
    /// The original goal or objective this message chain is working towards.
    /// </summary>
    public string? OriginalGoal { get; init; }

    /// <summary>
    /// The team working on this message, if any.
    /// </summary>
    public string? TeamId { get; init; }

    /// <summary>
    /// The channel this message belongs to.
    /// </summary>
    public string? ChannelId { get; init; }
}
