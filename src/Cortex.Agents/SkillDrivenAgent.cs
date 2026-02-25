// src/Cortex.Agents/SkillDrivenAgent.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
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
        ILogger<SkillDrivenAgent> logger)
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

        // Run the skill pipeline
        var context = await _pipelineRunner.RunAsync(
            _persona.Pipeline, envelope, parameters, cancellationToken);

        // Extract triage result from pipeline output
        var triageResult = ExtractTriageResult(context);

        if (triageResult is null || triageResult.Confidence < _persona.ConfidenceThreshold)
        {
            var reason = triageResult is null ? "No triage result" : "Low confidence";
            await EscalateAsync(envelope, reason, cancellationToken);
            return null;
        }

        // Find a matching agent (excluding self)
        var candidates = await _agentRegistry.FindByCapabilityAsync(
            triageResult.Capability, cancellationToken);
        var filtered = candidates.Where(a => a.AgentId != AgentId).ToList();

        if (filtered.Count == 0)
        {
            await EscalateAsync(
                envelope,
                $"No agent with capability '{triageResult.Capability}'",
                cancellationToken);
            return null;
        }

        var target = filtered[0];

        // Authority narrowing: outbound never exceeds inbound
        var maxInbound = GetMaxAuthorityTier(envelope);
        var effectiveTier = (AuthorityTier)Math.Min(
            (int)triageResult.AuthorityTier, (int)maxInbound);

        // Track delegation
        var refCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
        await _delegationTracker.DelegateAsync(new DelegationRecord
        {
            ReferenceCode = refCode,
            DelegatedBy = AgentId,
            DelegatedTo = target.AgentId,
            Description = triageResult.Summary,
            Status = DelegationStatus.Assigned,
            AssignedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        // Build and publish the routed envelope
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

        await _messagePublisher.PublishAsync(
            routedEnvelope, $"agent.{target.AgentId}", cancellationToken);

        _logger.LogInformation(
            "Routed {RefCode} to {TargetAgent} (capability: {Capability}, authority: {Authority})",
            refCode, target.AgentId, triageResult.Capability, effectiveTier);

        return null;
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

    private static TriageResult? ExtractTriageResult(SkillPipelineContext context)
    {
        foreach (var result in context.Results.Values)
        {
            if (result is not JsonElement json)
            {
                continue;
            }

            try
            {
                var capability = json.GetProperty("capability").GetString();
                var authorityStr = json.GetProperty("authorityTier").GetString();
                var summary = json.GetProperty("summary").GetString();
                var confidence = json.GetProperty("confidence").GetDouble();

                if (capability is null || authorityStr is null || summary is null)
                {
                    continue;
                }

                if (!Enum.TryParse<AuthorityTier>(authorityStr, ignoreCase: true, out var authorityTier))
                {
                    continue;
                }

                return new TriageResult
                {
                    Capability = capability,
                    AuthorityTier = authorityTier,
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

    private static AuthorityTier GetMaxAuthorityTier(MessageEnvelope envelope)
    {
        if (envelope.AuthorityClaims.Count == 0)
        {
            return AuthorityTier.JustDoIt;
        }

        return envelope.AuthorityClaims.Max(c => c.Tier);
    }
}
