using Cortex.Core.Authority;
using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Wraps any message with routing metadata, authority claims, and context.
/// Every message in the Cortex system travels inside an envelope.
/// </summary>
public sealed record MessageEnvelope
{
    /// <summary>
    /// The message payload.
    /// </summary>
    public required IMessage Message { get; init; }

    /// <summary>
    /// Unique traceable reference code for this message thread.
    /// </summary>
    public required ReferenceCode ReferenceCode { get; init; }

    /// <summary>
    /// Authority claims attached to this message.
    /// </summary>
    public IReadOnlyList<AuthorityClaim> AuthorityClaims { get; init; } = [];

    /// <summary>
    /// Contextual information for routing and tracking.
    /// </summary>
    public MessageContext Context { get; init; } = new();

    /// <summary>
    /// Processing priority.
    /// </summary>
    public MessagePriority Priority { get; init; } = MessagePriority.Normal;

    /// <summary>
    /// Time limit before this message should escalate.
    /// </summary>
    public TimeSpan? Sla { get; init; }
}
