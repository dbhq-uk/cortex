namespace Cortex.Agents;

/// <summary>
/// Describes a capability that an agent possesses, mapped to skills in the registry.
/// </summary>
public sealed record AgentCapability
{
    /// <summary>
    /// Name of this capability.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of what this capability does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// IDs of skills that implement this capability.
    /// </summary>
    public IReadOnlyList<string> SkillIds { get; init; } = [];
}
