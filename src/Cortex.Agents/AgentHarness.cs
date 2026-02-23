using Cortex.Messaging;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents;

/// <summary>
/// Connects a single <see cref="IAgent"/> to its message queue.
/// Handles message dispatch, reply routing, FromAgentId stamping, and lifecycle management.
/// Stores a per-consumer <see cref="IAsyncDisposable"/> handle so stopping this harness
/// does not affect other consumers on the shared message bus.
/// </summary>
public sealed class AgentHarness
{
    /// <summary>
    /// Creates a new <see cref="AgentHarness"/> for the specified agent.
    /// </summary>
    public AgentHarness(
        IAgent agent,
        IMessageBus messageBus,
        IAgentRegistry agentRegistry,
        ILogger<AgentHarness> logger)
    {
    }

    /// <summary>
    /// The queue name this harness consumes from.
    /// </summary>
    public string QueueName => throw new NotImplementedException();

    /// <summary>
    /// Whether this harness is currently running.
    /// </summary>
    public bool IsRunning => throw new NotImplementedException();

    /// <summary>
    /// Starts the harness: registers the agent and begins consuming messages.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <summary>
    /// Stops the harness: disposes consumer handle and marks the agent as unavailable.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
