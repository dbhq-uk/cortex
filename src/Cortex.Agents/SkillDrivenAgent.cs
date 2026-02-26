// src/Cortex.Agents/SkillDrivenAgent.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Workflows;
using Cortex.Core.Authority;
using Cortex.Core.Context;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;
using Cortex.Messaging;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents;

/// <summary>
/// Generic agent that processes messages through a configurable skill pipeline.
/// Identity, capabilities, and pipeline are defined by a <see cref="PersonaDefinition"/>.
/// Any persona (CoS, analyst, drafter) is an instance of this class with different config.
/// </summary>
public sealed class SkillDrivenAgent : IAgent, IAgentTypeProvider
{
    private readonly PersonaDefinition _persona;
    private readonly SkillPipelineRunner _pipelineRunner;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IDelegationTracker _delegationTracker;
    private readonly IReferenceCodeGenerator _referenceCodeGenerator;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<SkillDrivenAgent> _logger;
    private readonly IContextProvider? _contextProvider;
    private readonly IWorkflowTracker _workflowTracker;

    /// <summary>
    /// Creates a new <see cref="SkillDrivenAgent"/> with the given persona and dependencies.
    /// </summary>
    public SkillDrivenAgent(
        PersonaDefinition persona,
        SkillPipelineRunner pipelineRunner,
        IAgentRegistry agentRegistry,
        IDelegationTracker delegationTracker,
        IReferenceCodeGenerator referenceCodeGenerator,
        IMessagePublisher messagePublisher,
        ILogger<SkillDrivenAgent> logger,
        IContextProvider? contextProvider = null,
        IWorkflowTracker? workflowTracker = null)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(pipelineRunner);
        ArgumentNullException.ThrowIfNull(agentRegistry);
        ArgumentNullException.ThrowIfNull(delegationTracker);
        ArgumentNullException.ThrowIfNull(referenceCodeGenerator);
        ArgumentNullException.ThrowIfNull(messagePublisher);
        ArgumentNullException.ThrowIfNull(logger);

        _persona = persona;
        _pipelineRunner = pipelineRunner;
        _agentRegistry = agentRegistry;
        _delegationTracker = delegationTracker;
        _referenceCodeGenerator = referenceCodeGenerator;
        _messagePublisher = messagePublisher;
        _logger = logger;
        _contextProvider = contextProvider;
        _workflowTracker = workflowTracker ?? new NullWorkflowTracker();
    }

    /// <inheritdoc />
    public string AgentId => _persona.AgentId;

    /// <inheritdoc />
    public string Name => _persona.Name;

    /// <inheritdoc />
    public IReadOnlyList<AgentCapability> Capabilities => _persona.Capabilities;

    /// <inheritdoc />
    public string AgentType => _persona.AgentType;

    /// <inheritdoc />
    public async Task<MessageEnvelope?> ProcessAsync(
        MessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Agent {AgentId} processing message {MessageId}",
            AgentId, envelope.Message.MessageId);

        // Build parameters for the pipeline
        var capabilityNames = await GetAvailableCapabilitiesAsync(cancellationToken);
        var messageContent = JsonSerializer.Serialize(
            envelope.Message, envelope.Message.GetType());
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = messageContent,
            ["availableCapabilities"] = string.Join(", ", capabilityNames)
        };

        if (_contextProvider is not null)
        {
            var contextResults = await _contextProvider.QueryAsync(
                new ContextQuery { Keywords = messageContent, MaxResults = 5 },
                cancellationToken);

            if (contextResults.Count > 0)
            {
                var contextSummary = string.Join("\n",
                    contextResults.Select(c => $"[{c.Category}] {c.Content}"));
                parameters["businessContext"] = contextSummary;
            }
        }

        // Run the skill pipeline
        var context = await _pipelineRunner.RunAsync(
            _persona.Pipeline, envelope, parameters, cancellationToken);

        // Extract decomposition result from pipeline output
        var decomposition = ExtractDecompositionResult(context);

        if (decomposition is null || decomposition.Confidence < _persona.ConfidenceThreshold)
        {
            var reason = decomposition is null ? "No triage result" : "Low confidence";
            await EscalateAsync(envelope, reason, cancellationToken);
            return null;
        }

        if (decomposition.Tasks.Count == 0)
        {
            await EscalateAsync(envelope, "No tasks in decomposition", cancellationToken);
            return null;
        }

        if (decomposition.Tasks.Count == 1)
        {
            return await RouteSingleTaskAsync(envelope, decomposition.Tasks[0], cancellationToken);
        }

        return await RouteWorkflowAsync(envelope, decomposition, cancellationToken);
    }

    private async Task<MessageEnvelope?> RouteSingleTaskAsync(
        MessageEnvelope envelope,
        DecompositionTask task,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AuthorityTier>(task.AuthorityTier, ignoreCase: true, out var taskAuthority))
        {
            taskAuthority = AuthorityTier.JustDoIt;
        }

        var candidates = await _agentRegistry.FindByCapabilityAsync(task.Capability, cancellationToken);
        var filtered = candidates.Where(a => a.AgentId != AgentId).ToList();

        if (filtered.Count == 0)
        {
            await EscalateAsync(envelope, $"No agent with capability '{task.Capability}'", cancellationToken);
            return null;
        }

        var target = filtered[0];
        var maxInbound = GetMaxAuthorityTier(envelope);
        var effectiveTier = (AuthorityTier)Math.Min((int)taskAuthority, (int)maxInbound);

        var refCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
        await _delegationTracker.DelegateAsync(new DelegationRecord
        {
            ReferenceCode = refCode,
            DelegatedBy = AgentId,
            DelegatedTo = target.AgentId,
            Description = task.Description,
            Status = DelegationStatus.Assigned,
            AssignedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        var routedEnvelope = envelope with
        {
            ReferenceCode = refCode,
            AuthorityClaims =
            [
                new AuthorityClaim
                {
                    GrantedBy = AgentId,
                    GrantedTo = target.AgentId,
                    Tier = effectiveTier,
                    GrantedAt = DateTimeOffset.UtcNow
                }
            ],
            Context = envelope.Context with
            {
                ParentMessageId = envelope.Message.MessageId,
                FromAgentId = AgentId
            }
        };

        await _messagePublisher.PublishAsync(routedEnvelope, $"agent.{target.AgentId}", cancellationToken);

        _logger.LogInformation(
            "Routed {RefCode} to {TargetAgent} (capability: {Capability}, authority: {Authority})",
            refCode, target.AgentId, task.Capability, effectiveTier);

        return null;
    }

    private Task<MessageEnvelope?> RouteWorkflowAsync(
        MessageEnvelope envelope,
        DecompositionResult decomposition,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Workflow routing not yet implemented");
    }

    private async Task EscalateAsync(
        MessageEnvelope envelope,
        string reason,
        CancellationToken cancellationToken)
    {
        var refCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);

        await _delegationTracker.DelegateAsync(new DelegationRecord
        {
            ReferenceCode = refCode,
            DelegatedBy = AgentId,
            DelegatedTo = _persona.EscalationTarget,
            Description = $"Escalated: {reason}",
            Status = DelegationStatus.Assigned,
            AssignedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        var escalatedEnvelope = envelope with
        {
            ReferenceCode = refCode,
            Context = envelope.Context with
            {
                ParentMessageId = envelope.Message.MessageId,
                FromAgentId = AgentId
            }
        };

        await _messagePublisher.PublishAsync(
            escalatedEnvelope, _persona.EscalationTarget, cancellationToken);

        _logger.LogWarning(
            "Escalated {RefCode} to {Target}: {Reason}",
            refCode, _persona.EscalationTarget, reason);
    }

    private async Task<IReadOnlyList<string>> GetAvailableCapabilitiesAsync(
        CancellationToken cancellationToken)
    {
        // Query all capabilities from all known agents, excluding self
        var agents = new List<AgentRegistration>();

        // FindByCapabilityAsync filters by specific capability; we need all capabilities.
        // Use a broad search: check each known capability.
        // For Phase 1, collect from all running agents.
        // This is a workaround until IAgentRegistry exposes GetAllAsync.
        foreach (var cap in _persona.Capabilities)
        {
            var matches = await _agentRegistry.FindByCapabilityAsync(cap.Name, cancellationToken);
            agents.AddRange(matches);
        }

        // Also query commonly-known capabilities — in Phase 1, we rely on
        // the triage skill to determine capability from message content.
        // The available capabilities list is informational for the LLM prompt.
        return agents
            .Where(a => a.AgentId != AgentId)
            .SelectMany(a => a.Capabilities)
            .Select(c => c.Name)
            .Distinct()
            .ToList();
    }

    private static DecompositionResult? ExtractDecompositionResult(SkillPipelineContext context)
    {
        foreach (var result in context.Results.Values)
        {
            if (result is not JsonElement json)
            {
                continue;
            }

            try
            {
                if (!json.TryGetProperty("tasks", out var tasksElement)
                    || tasksElement.ValueKind != JsonValueKind.Array)
                {
                    return ExtractFromLegacyTriageFormat(json);
                }

                var tasks = new List<DecompositionTask>();
                foreach (var taskElement in tasksElement.EnumerateArray())
                {
                    var capability = taskElement.GetProperty("capability").GetString();
                    var description = taskElement.GetProperty("description").GetString();
                    var authorityTier = taskElement.GetProperty("authorityTier").GetString();

                    if (capability is null || description is null || authorityTier is null)
                    {
                        continue;
                    }

                    tasks.Add(new DecompositionTask
                    {
                        Capability = capability,
                        Description = description,
                        AuthorityTier = authorityTier
                    });
                }

                var summary = json.GetProperty("summary").GetString();
                var confidence = json.GetProperty("confidence").GetDouble();

                if (tasks.Count == 0 || summary is null)
                {
                    continue;
                }

                return new DecompositionResult
                {
                    Tasks = tasks,
                    Summary = summary,
                    Confidence = confidence
                };
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
            {
                continue;
            }
        }

        return null;
    }

    private static DecompositionResult? ExtractFromLegacyTriageFormat(JsonElement json)
    {
        try
        {
            var capability = json.GetProperty("capability").GetString();
            var authorityStr = json.GetProperty("authorityTier").GetString();
            var summary = json.GetProperty("summary").GetString();
            var confidence = json.GetProperty("confidence").GetDouble();

            if (capability is null || authorityStr is null || summary is null)
            {
                return null;
            }

            return new DecompositionResult
            {
                Tasks =
                [
                    new DecompositionTask
                    {
                        Capability = capability,
                        Description = summary,
                        AuthorityTier = authorityStr
                    }
                ],
                Summary = summary,
                Confidence = confidence
            };
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static AuthorityTier GetMaxAuthorityTier(MessageEnvelope envelope)
    {
        if (envelope.AuthorityClaims.Count == 0)
        {
            return AuthorityTier.JustDoIt;
        }

        return envelope.AuthorityClaims.Max(c => c.Tier);
    }
}
