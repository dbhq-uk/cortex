namespace Cortex.Agents;

/// <summary>
/// Registration record for an agent in the system registry.
/// </summary>
public sealed record AgentRegistration
{
    /// <summary>
    /// Unique identifier for this agent.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The type of agent: "human" or "ai".
    /// </summary>
    public required string AgentType { get; init; }

    /// <summary>
    /// Capabilities this agent possesses.
    /// </summary>
    public IReadOnlyList<AgentCapability> Capabilities { get; init; } = [];

    /// <summary>
    /// When this agent was registered.
    /// </summary>
    public required DateTimeOffset RegisteredAt { get; init; }

    /// <summary>
    /// Whether this agent is currently available to receive work.
    /// </summary>
    public bool IsAvailable { get; init; } = true;
}
