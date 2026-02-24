using System.Collections.Concurrent;

namespace Cortex.Agents;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IAgentRegistry"/>.
/// </summary>
public sealed class InMemoryAgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, AgentRegistration> _agents = new();

    /// <inheritdoc />
    public Task RegisterAsync(AgentRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _agents[registration.AgentId] = registration;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AgentRegistration?> FindByIdAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        _agents.TryGetValue(agentId, out var registration);
        return Task.FromResult(registration);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);

        var matches = _agents.Values
            .Where(a => a.IsAvailable && a.Capabilities.Any(c =>
                string.Equals(c.Name, capabilityName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return Task.FromResult<IReadOnlyList<AgentRegistration>>(matches);
    }
}
