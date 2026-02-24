namespace Cortex.Agents;

/// <summary>
/// Runtime for managing agent harnesses. Supports both static (DI-registered)
/// and dynamic (on-demand) agent lifecycle management, including team-scoped operations.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Starts an agent and connects it to its message queue.
    /// Returns the agent's ID.
    /// </summary>
    Task<string> StartAgentAsync(IAgent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an agent as part of a team and connects it to its message queue.
    /// Returns the agent's ID.
    /// </summary>
    Task<string> StartAgentAsync(IAgent agent, string teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running agent and disconnects it from its queue.
    /// </summary>
    Task StopAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all agents belonging to a team.
    /// </summary>
    Task StopTeamAsync(string teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// IDs of all currently running agents.
    /// </summary>
    IReadOnlyList<string> RunningAgentIds { get; }

    /// <summary>
    /// Returns the IDs of all running agents belonging to the specified team.
    /// </summary>
    IReadOnlyList<string> GetTeamAgentIds(string teamId);
}
