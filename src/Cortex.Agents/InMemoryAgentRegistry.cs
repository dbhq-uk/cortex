namespace Cortex.Agents;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IAgentRegistry"/>.
/// </summary>
public sealed class InMemoryAgentRegistry : IAgentRegistry
{
    /// <inheritdoc />
    public Task RegisterAsync(AgentRegistration registration, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<AgentRegistration?> FindByIdAsync(string agentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
