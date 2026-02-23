using Microsoft.Extensions.Hosting;

namespace Cortex.Agents;

/// <summary>
/// Manages all agent harnesses. Implements <see cref="IHostedService"/> for host integration
/// and <see cref="IAgentRuntime"/> for dynamic agent creation by other agents.
/// </summary>
public sealed class AgentRuntime : IHostedService, IAgentRuntime
{
    /// <summary>
    /// Creates a new <see cref="AgentRuntime"/>.
    /// </summary>
    public AgentRuntime(
        Messaging.IMessageBus messageBus,
        IAgentRegistry agentRegistry,
        IEnumerable<IAgent> startupAgents,
        Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
    {
    }

    /// <inheritdoc />
    public IReadOnlyList<string> RunningAgentIds => throw new NotImplementedException();

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    Task<string> IAgentRuntime.StartAgentAsync(IAgent agent, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    Task<string> IAgentRuntime.StartAgentAsync(IAgent agent, string teamId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    Task IAgentRuntime.StopAgentAsync(string agentId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    Task IAgentRuntime.StopTeamAsync(string teamId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    IReadOnlyList<string> IAgentRuntime.GetTeamAgentIds(string teamId)
        => throw new NotImplementedException();
}
