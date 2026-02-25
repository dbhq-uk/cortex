namespace Cortex.Agents.Personas;

/// <summary>
/// Parsed persona configuration that defines an agent's identity, capabilities, and skill pipeline.
/// Loaded from a persona markdown file.
/// </summary>
public sealed record PersonaDefinition
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
    /// Agent type: "ai" or "human".
    /// </summary>
    public required string AgentType { get; init; }

    /// <summary>
    /// Capabilities this agent possesses.
    /// </summary>
    public required IReadOnlyList<AgentCapability> Capabilities { get; init; }

    /// <summary>
    /// Ordered list of skill IDs that form this agent's processing pipeline.
    /// </summary>
    public required IReadOnlyList<string> Pipeline { get; init; }

    /// <summary>
    /// Queue name to publish to when a message cannot be routed.
    /// </summary>
    public required string EscalationTarget { get; init; }

    /// <summary>
    /// The model tier for LLM operations: "lightweight", "balanced", or "heavyweight".
    /// </summary>
    public string ModelTier { get; init; } = "balanced";

    /// <summary>
    /// Minimum confidence score required to route a triage result. Below this, escalate.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.6;
}
