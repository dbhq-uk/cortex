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
    private readonly IPendingPlanStore _pendingPlanStore;

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
        IWorkflowTracker? workflowTracker = null,
        IPendingPlanStore? pendingPlanStore = null)
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
        _pendingPlanStore = pendingPlanStore ?? new NullPendingPlanStore();
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

        // Check if this is a plan approval response
        if (envelope.Message is PlanApprovalResponse approval)
        {
            return await HandlePlanApprovalAsync(envelope, approval, cancellationToken);
        }

        // Check if this is a sub-task reply for a pending workflow
        var workflow = await _workflowTracker.FindBySubtaskAsync(envelope.ReferenceCode, cancellationToken);
        if (workflow is not null)
        {
            return await HandleSubtaskReplyAsync(envelope, workflow, cancellationToken);
        }

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

        // AskMeFirst gate: require approval before dispatching
        var maxTier = GetMaxAuthorityTier(envelope);
        if (maxTier >= AuthorityTier.AskMeFirst)
        {
            return await GatePlanForApprovalAsync(envelope, decomposition, cancellationToken);
        }

        if (decomposition.Tasks.Count == 1)
        {
            return await RouteSingleTaskAsync(envelope, decomposition.Tasks[0], cancellationToken);
        }

        return await RouteWorkflowAsync(envelope, decomposition, cancellationToken);
    }

    private async Task<MessageEnvelope?> HandleSubtaskReplyAsync(
        MessageEnvelope subtaskReply,
        WorkflowRecord workflow,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Workflow {ParentRef}: received sub-task result {ChildRef}",
            workflow.ReferenceCode, subtaskReply.ReferenceCode);

        // Store the result
        await _workflowTracker.StoreSubtaskResultAsync(
            subtaskReply.ReferenceCode, subtaskReply, cancellationToken);

        // Update delegation status
        await _delegationTracker.UpdateStatusAsync(
            subtaskReply.ReferenceCode, DelegationStatus.Complete, cancellationToken);

        // Check if all sub-tasks are done
        if (!await _workflowTracker.AllSubtasksCompleteAsync(workflow.ReferenceCode, cancellationToken))
        {
            _logger.LogInformation(
                "Workflow {ParentRef}: waiting for more sub-tasks",
                workflow.ReferenceCode);
            return null;
        }

        // All complete — assemble result
        var results = await _workflowTracker.GetCompletedResultsAsync(
            workflow.ReferenceCode, cancellationToken);

        var assembledContent = AssembleResults(workflow, results);

        var assembledEnvelope = new MessageEnvelope
        {
            Message = new TextMessage(assembledContent),
            ReferenceCode = workflow.ReferenceCode,
            Context = new MessageContext
            {
                ParentMessageId = workflow.OriginalEnvelope.Message.MessageId,
                FromAgentId = AgentId,
                ReplyTo = workflow.OriginalEnvelope.Context.ReplyTo
            }
        };

        // Publish to original requester
        if (workflow.OriginalEnvelope.Context.ReplyTo is not null)
        {
            await _messagePublisher.PublishAsync(
                assembledEnvelope,
                workflow.OriginalEnvelope.Context.ReplyTo,
                cancellationToken);
        }

        // Mark workflow as completed
        await _workflowTracker.UpdateStatusAsync(
            workflow.ReferenceCode, WorkflowStatus.Completed, cancellationToken);

        _logger.LogInformation(
            "Workflow {ParentRef}: completed, assembled result published to {ReplyTo}",
            workflow.ReferenceCode, workflow.OriginalEnvelope.Context.ReplyTo);

        return null;
    }

    private static string AssembleResults(
        WorkflowRecord workflow,
        IReadOnlyDictionary<ReferenceCode, MessageEnvelope> results)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"# {workflow.Summary}");
        builder.AppendLine();

        foreach (var subtaskRef in workflow.SubtaskReferenceCodes)
        {
            if (results.TryGetValue(subtaskRef, out var result))
            {
                var content = ExtractMessageContent(result.Message);
                builder.AppendLine($"## {subtaskRef}");
                builder.AppendLine(content);
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string ExtractMessageContent(IMessage message)
    {
        // Use JSON serialization to extract content from any message type
        return JsonSerializer.Serialize(message, message.GetType());
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

    private async Task<MessageEnvelope?> RouteWorkflowAsync(
        MessageEnvelope envelope,
        DecompositionResult decomposition,
        CancellationToken cancellationToken)
    {
        var maxInbound = GetMaxAuthorityTier(envelope);
        var parentRefCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
        var subtaskRefCodes = new List<ReferenceCode>();

        // Pre-validate: ensure all capabilities have agents before creating any delegations
        foreach (var task in decomposition.Tasks)
        {
            var candidates = await _agentRegistry.FindByCapabilityAsync(task.Capability, cancellationToken);
            var filtered = candidates.Where(a => a.AgentId != AgentId).ToList();

            if (filtered.Count == 0)
            {
                await EscalateAsync(
                    envelope,
                    $"Cannot decompose: no agent with capability '{task.Capability}'",
                    cancellationToken);
                return null;
            }
        }

        // All capabilities valid — create delegations and publish
        foreach (var task in decomposition.Tasks)
        {
            if (!Enum.TryParse<AuthorityTier>(task.AuthorityTier, ignoreCase: true, out var taskAuthority))
            {
                taskAuthority = AuthorityTier.JustDoIt;
            }

            var effectiveTier = (AuthorityTier)Math.Min((int)taskAuthority, (int)maxInbound);

            var candidates = await _agentRegistry.FindByCapabilityAsync(task.Capability, cancellationToken);
            var target = candidates.First(a => a.AgentId != AgentId);

            var childRefCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
            subtaskRefCodes.Add(childRefCode);

            await _delegationTracker.DelegateAsync(new DelegationRecord
            {
                ReferenceCode = childRefCode,
                DelegatedBy = AgentId,
                DelegatedTo = target.AgentId,
                Description = task.Description,
                Status = DelegationStatus.Assigned,
                AssignedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            var childEnvelope = envelope with
            {
                ReferenceCode = childRefCode,
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
                    FromAgentId = AgentId,
                    ReplyTo = $"agent.{AgentId}",
                    OriginalGoal = decomposition.Summary
                }
            };

            await _messagePublisher.PublishAsync(childEnvelope, $"agent.{target.AgentId}", cancellationToken);

            _logger.LogInformation(
                "Workflow {ParentRef}: dispatched {ChildRef} to {Target} (capability: {Capability})",
                parentRefCode, childRefCode, target.AgentId, task.Capability);
        }

        // Create workflow record
        var workflow = new WorkflowRecord
        {
            ReferenceCode = parentRefCode,
            OriginalEnvelope = envelope,
            SubtaskReferenceCodes = subtaskRefCodes,
            Summary = decomposition.Summary
        };
        await _workflowTracker.CreateAsync(workflow, cancellationToken);

        _logger.LogInformation(
            "Created workflow {ParentRef} with {Count} sub-tasks",
            parentRefCode, subtaskRefCodes.Count);

        return null;
    }

    private async Task<MessageEnvelope?> GatePlanForApprovalAsync(
        MessageEnvelope envelope,
        DecompositionResult decomposition,
        CancellationToken cancellationToken)
    {
        var refCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);

        var pendingPlan = new PendingPlan
        {
            OriginalEnvelope = envelope,
            Decomposition = decomposition,
            StoredAt = DateTimeOffset.UtcNow
        };

        await _pendingPlanStore.StoreAsync(refCode, pendingPlan, cancellationToken);

        var proposal = new PlanProposal
        {
            Summary = decomposition.Summary,
            TaskDescriptions = decomposition.Tasks.Select(t => t.Description).ToList(),
            OriginalGoal = envelope.Message is TextMessage text ? text.Content : "Unknown goal",
            WorkflowReferenceCode = refCode
        };

        var proposalEnvelope = new MessageEnvelope
        {
            Message = proposal,
            ReferenceCode = refCode,
            Context = new MessageContext
            {
                ParentMessageId = envelope.Message.MessageId,
                FromAgentId = AgentId,
                ReplyTo = $"agent.{AgentId}"
            }
        };

        await _messagePublisher.PublishAsync(
            proposalEnvelope, _persona.EscalationTarget, cancellationToken);

        _logger.LogInformation(
            "AskMeFirst gate: plan {RefCode} sent to {Target} for approval ({TaskCount} tasks)",
            refCode, _persona.EscalationTarget, decomposition.Tasks.Count);

        return null;
    }

    private async Task<MessageEnvelope?> HandlePlanApprovalAsync(
        MessageEnvelope envelope,
        PlanApprovalResponse approval,
        CancellationToken cancellationToken)
    {
        var plan = await _pendingPlanStore.GetAsync(approval.WorkflowReferenceCode, cancellationToken);

        if (plan is null)
        {
            _logger.LogWarning(
                "Received approval for unknown plan {RefCode}",
                approval.WorkflowReferenceCode);
            return null;
        }

        await _pendingPlanStore.RemoveAsync(approval.WorkflowReferenceCode, cancellationToken);

        if (!approval.IsApproved)
        {
            // Publish rejection notification to original requester
            var replyTo = plan.OriginalEnvelope.Context.ReplyTo;
            if (replyTo is not null)
            {
                var rejectionMessage = new TextMessage(
                    $"Plan rejected: {approval.RejectionReason ?? "No reason given"}");
                var rejectionEnvelope = new MessageEnvelope
                {
                    Message = rejectionMessage,
                    ReferenceCode = approval.WorkflowReferenceCode,
                    Context = new MessageContext
                    {
                        ParentMessageId = envelope.Message.MessageId,
                        FromAgentId = AgentId
                    }
                };
                await _messagePublisher.PublishAsync(rejectionEnvelope, replyTo, cancellationToken);
            }

            _logger.LogInformation(
                "Plan {RefCode} rejected: {Reason}",
                approval.WorkflowReferenceCode, approval.RejectionReason);

            return null;
        }

        // Approved — resume routing with the stored decomposition
        _logger.LogInformation("Plan {RefCode} approved, resuming dispatch", approval.WorkflowReferenceCode);

        var decomposition = plan.Decomposition;
        var originalEnvelope = plan.OriginalEnvelope;

        if (decomposition.Tasks.Count == 1)
        {
            return await RouteSingleTaskAsync(originalEnvelope, decomposition.Tasks[0], cancellationToken);
        }

        return await RouteWorkflowAsync(originalEnvelope, decomposition, cancellationToken);
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
