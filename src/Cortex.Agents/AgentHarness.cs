using Cortex.Core.Messages;
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
    private readonly IAgent _agent;
    private readonly IMessageBus _messageBus;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<AgentHarness> _logger;
    private IAsyncDisposable? _consumerHandle;

    /// <summary>
    /// Creates a new <see cref="AgentHarness"/> for the specified agent.
    /// </summary>
    public AgentHarness(
        IAgent agent,
        IMessageBus messageBus,
        IAgentRegistry agentRegistry,
        ILogger<AgentHarness> logger)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(agentRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _agent = agent;
        _messageBus = messageBus;
        _agentRegistry = agentRegistry;
        _logger = logger;
    }

    /// <summary>
    /// The queue name this harness consumes from.
    /// </summary>
    public string QueueName => $"agent.{_agent.AgentId}";

    /// <summary>
    /// Whether this harness is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Starts the harness: registers the agent and begins consuming messages.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var registration = new AgentRegistration
        {
            AgentId = _agent.AgentId,
            Name = _agent.Name,
            AgentType = _agent is IAgentTypeProvider typed ? typed.AgentType : "unknown",
            Capabilities = _agent.Capabilities.ToList(),
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = true
        };

        await _agentRegistry.RegisterAsync(registration, cancellationToken);

        _consumerHandle = await _messageBus.StartConsumingAsync(
            QueueName, HandleMessageAsync, cancellationToken);

        IsRunning = true;

        _logger.LogInformation(
            "Agent {AgentId} started, consuming from {QueueName}",
            _agent.AgentId, QueueName);
    }

    /// <summary>
    /// Stops the harness: disposes consumer handle and marks the agent as unavailable.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_consumerHandle is not null)
        {
            await _consumerHandle.DisposeAsync();
            _consumerHandle = null;
        }

        // Mark agent as unavailable
        var registration = await _agentRegistry.FindByIdAsync(_agent.AgentId, cancellationToken);
        if (registration is not null)
        {
            await _agentRegistry.RegisterAsync(
                registration with { IsAvailable = false },
                cancellationToken);
        }

        IsRunning = false;

        _logger.LogInformation("Agent {AgentId} stopped", _agent.AgentId);
    }

    private async Task HandleMessageAsync(MessageEnvelope envelope)
    {
        _logger.LogDebug(
            "Agent {AgentId} processing message {MessageId}",
            _agent.AgentId, envelope.Message.MessageId);

        var response = await _agent.ProcessAsync(envelope);

        if (response is null)
        {
            return;
        }

        var replyTo = envelope.Context.ReplyTo;

        if (string.IsNullOrWhiteSpace(replyTo))
        {
            _logger.LogWarning(
                "Agent {AgentId} returned a response but message has no ReplyTo — dropping response",
                _agent.AgentId);
            return;
        }

        // Carry forward reference code, set parent message ID, and stamp sender identity
        var replyEnvelope = response with
        {
            ReferenceCode = envelope.ReferenceCode,
            Context = response.Context with
            {
                ParentMessageId = envelope.Message.MessageId,
                FromAgentId = _agent.AgentId
            }
        };

        await _messageBus.PublishAsync(replyEnvelope, replyTo);

        _logger.LogDebug(
            "Agent {AgentId} published reply to {ReplyTo}",
            _agent.AgentId, replyTo);
    }
}
