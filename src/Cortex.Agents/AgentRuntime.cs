using System.Collections.Concurrent;
using Cortex.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents;

/// <summary>
/// Manages all agent harnesses. Implements <see cref="IHostedService"/> for host integration
/// and <see cref="IAgentRuntime"/> for dynamic agent creation by other agents.
/// </summary>
public sealed class AgentRuntime : IHostedService, IAgentRuntime
{
    private readonly IMessageBus _messageBus;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IReadOnlyList<IAgent> _startupAgents;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentRuntime> _logger;
    private readonly ConcurrentDictionary<string, AgentHarness> _harnesses = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _teamAgents = new();
    private readonly ConcurrentDictionary<string, string> _agentTeams = new();

    /// <summary>
    /// Creates a new <see cref="AgentRuntime"/>.
    /// </summary>
    public AgentRuntime(
        IMessageBus messageBus,
        IAgentRegistry agentRegistry,
        IEnumerable<IAgent> startupAgents,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(agentRegistry);
        ArgumentNullException.ThrowIfNull(startupAgents);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _messageBus = messageBus;
        _agentRegistry = agentRegistry;
        _startupAgents = startupAgents.ToList();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AgentRuntime>();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> RunningAgentIds =>
        _harnesses.Keys.ToList();

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent runtime starting with {Count} startup agents", _startupAgents.Count);

        foreach (var agent in _startupAgents)
        {
            await ((IAgentRuntime)this).StartAgentAsync(agent, cancellationToken);
        }

        _logger.LogInformation("Agent runtime started");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent runtime stopping, draining {Count} agents", _harnesses.Count);

        var agentIds = _harnesses.Keys.ToList();

        foreach (var agentId in agentIds)
        {
            await ((IAgentRuntime)this).StopAgentAsync(agentId, cancellationToken);
        }

        _logger.LogInformation("Agent runtime stopped");
    }

    /// <inheritdoc />
    Task<string> IAgentRuntime.StartAgentAsync(IAgent agent, CancellationToken cancellationToken) =>
        StartAgentInternalAsync(agent, teamId: null, cancellationToken);

    /// <inheritdoc />
    Task<string> IAgentRuntime.StartAgentAsync(IAgent agent, string teamId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        return StartAgentInternalAsync(agent, teamId, cancellationToken);
    }

    /// <inheritdoc />
    async Task IAgentRuntime.StopAgentAsync(string agentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        if (!_harnesses.TryRemove(agentId, out var harness))
        {
            _logger.LogWarning("Cannot stop agent {AgentId}: not running", agentId);
            return;
        }

        // Remove from team tracking
        if (_agentTeams.TryRemove(agentId, out var teamId))
        {
            if (_teamAgents.TryGetValue(teamId, out var members))
            {
                // ConcurrentBag doesn't support removal — rebuild without this agent
                var remaining = new ConcurrentBag<string>(members.Where(id => id != agentId));
                _teamAgents[teamId] = remaining;
            }
        }

        await harness.StopAsync(cancellationToken);

        _logger.LogInformation("Stopped agent {AgentId}", agentId);
    }

    /// <inheritdoc />
    async Task IAgentRuntime.StopTeamAsync(string teamId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

        var agentIds = ((IAgentRuntime)this).GetTeamAgentIds(teamId);

        foreach (var agentId in agentIds)
        {
            await ((IAgentRuntime)this).StopAgentAsync(agentId, cancellationToken);
        }

        _teamAgents.TryRemove(teamId, out _);

        _logger.LogInformation("Stopped all agents in team {TeamId}", teamId);
    }

    /// <inheritdoc />
    IReadOnlyList<string> IAgentRuntime.GetTeamAgentIds(string teamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

        if (_teamAgents.TryGetValue(teamId, out var members))
        {
            return members.ToList();
        }

        return [];
    }

    private async Task<string> StartAgentInternalAsync(
        IAgent agent, string? teamId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var harness = new AgentHarness(
            agent,
            _messageBus,
            _agentRegistry,
            _loggerFactory.CreateLogger<AgentHarness>());

        if (!_harnesses.TryAdd(agent.AgentId, harness))
        {
            throw new InvalidOperationException($"Agent '{agent.AgentId}' is already running.");
        }

        // Track team membership
        if (teamId is not null)
        {
            _agentTeams[agent.AgentId] = teamId;
            var members = _teamAgents.GetOrAdd(teamId, _ => []);
            members.Add(agent.AgentId);
        }

        await harness.StartAsync(cancellationToken);

        _logger.LogInformation(
            "Started agent {AgentId}{TeamInfo}",
            agent.AgentId,
            teamId is not null ? $" in team {teamId}" : string.Empty);

        return agent.AgentId;
    }
}
