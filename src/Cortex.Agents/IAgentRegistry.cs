namespace Cortex.Agents;

/// <summary>
/// Registry for discovering and managing agents by their capabilities.
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Registers an agent in the system.
    /// </summary>
    Task RegisterAsync(AgentRegistration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an agent by its unique ID.
    /// </summary>
    Task<AgentRegistration?> FindByIdAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all agents that have a specific capability.
    /// </summary>
    Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default);
}
