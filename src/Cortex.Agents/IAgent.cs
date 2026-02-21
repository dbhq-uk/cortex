using Cortex.Core.Messages;

namespace Cortex.Agents;

/// <summary>
/// Core agent contract. Both human and AI agents implement this interface.
/// The system does not fundamentally distinguish between them at the routing layer.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Unique identifier for this agent.
    /// </summary>
    string AgentId { get; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Capabilities this agent possesses.
    /// </summary>
    IReadOnlyList<AgentCapability> Capabilities { get; }

    /// <summary>
    /// Processes a message and optionally returns a response message.
    /// </summary>
    Task<MessageEnvelope?> ProcessAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);
}
