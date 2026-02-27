// tests/Cortex.Agents.Tests/SkillDrivenAgentApprovalTests.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Tests.Pipeline;
using Cortex.Agents.Workflows;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

/// <summary>
/// Tests for <see cref="SkillDrivenAgent"/> AskMeFirst plan approval gating.
/// </summary>
public sealed class SkillDrivenAgentApprovalTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;
    private readonly InMemoryPendingPlanStore _pendingPlanStore = new();
    private readonly InMemoryWorkflowTracker _workflowTracker = new();

    public SkillDrivenAgentApprovalTests()
    {
        _refCodeGenerator = new SequentialReferenceCodeGenerator(
            new InMemorySequenceStore(), TimeProvider.System);
    }

    private SkillDrivenAgent CreateAgent(PersonaDefinition? persona = null)
    {
        var p = persona ?? CreateDefaultPersona();
        var pipelineRunner = new SkillPipelineRunner(
            _skillRegistry,
            [_fakeExecutor],
            NullLogger<SkillPipelineRunner>.Instance);

        return new SkillDrivenAgent(
            p,
            pipelineRunner,
            _agentRegistry,
            _delegationTracker,
            _refCodeGenerator,
            _bus,
            NullLogger<SkillDrivenAgent>.Instance,
            pendingPlanStore: _pendingPlanStore,
            workflowTracker: _workflowTracker);
    }

    private static PersonaDefinition CreateDefaultPersona() => new()
    {
        AgentId = "cos",
        Name = "Chief of Staff",
        AgentType = "ai",
        Capabilities =
        [
            new AgentCapability { Name = "triage", Description = "Triage" }
        ],
        Pipeline = ["cos-decompose"],
        EscalationTarget = "agent.founder",
        ConfidenceThreshold = 0.6
    };

    private static MessageEnvelope CreateEnvelope(
        string content = "test",
        string? replyTo = null,
        IReadOnlyList<AuthorityClaim>? claims = null) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo },
            AuthorityClaims = claims ?? []
        };

    private void RegisterDecomposeSkill()
    {
        _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-decompose",
            Name = "CoS Decompose",
            Description = "Decompose",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        }).GetAwaiter().GetResult();
    }

    private void SetDecomposeResult(
        string capability = "email-drafting",
        string authorityTier = "AskMeFirst",
        double confidence = 0.9,
        string description = "Test task",
        string summary = "Test plan")
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            tasks = new[] { new { capability, description, authorityTier } },
            summary,
            confidence
        });
        _fakeExecutor.SetResult("cos-decompose", json);
    }

    private async Task RegisterSpecialistAgent(string agentId, string capabilityName)
    {
        await _agentRegistry.RegisterAsync(new AgentRegistration
        {
            AgentId = agentId,
            Name = $"Agent {agentId}",
            AgentType = "ai",
            Capabilities =
            [
                new AgentCapability { Name = capabilityName, Description = capabilityName }
            ],
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = true
        });
    }

    private static AuthorityClaim CreateClaim(AuthorityTier tier) => new()
    {
        GrantedBy = "founder",
        GrantedTo = "cos",
        Tier = tier,
        GrantedAt = DateTimeOffset.UtcNow
    };

    // --- AskMeFirst gating ---

    [Fact]
    public async Task AskMeFirst_PublishesPlanProposal()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        var envelope = CreateEnvelope(
            "Send email to client",
            replyTo: "agent.requester",
            claims: [CreateClaim(AuthorityTier.AskMeFirst)]);

        await agent.ProcessAsync(envelope);

        var received = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = Assert.IsType<PlanProposal>(received.Message);
        Assert.Equal("Test plan", proposal.Summary);
        Assert.Single(proposal.TaskDescriptions);
        Assert.Equal("Test task", proposal.TaskDescriptions[0]);
    }

    [Fact]
    public async Task AskMeFirst_StoresPendingPlan()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        var envelope = CreateEnvelope(
            "Send email",
            claims: [CreateClaim(AuthorityTier.AskMeFirst)]);

        await agent.ProcessAsync(envelope);

        var received = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = Assert.IsType<PlanProposal>(received.Message);

        // Retrieve the plan from the store using the workflow reference code
        var plan = await _pendingPlanStore.GetAsync(proposal.WorkflowReferenceCode);
        Assert.NotNull(plan);
        Assert.Equal("Test plan", plan.Decomposition.Summary);
    }

    [Fact]
    public async Task AskMeFirst_DoesNotDispatchTasks()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var specialistReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            specialistReceived.SetResult(e);
            return Task.CompletedTask;
        });

        // Also consume from escalation target to capture proposal
        await _bus.StartConsumingAsync("agent.founder", _ => Task.CompletedTask);

        var agent = CreateAgent();
        var envelope = CreateEnvelope(
            "Send email",
            claims: [CreateClaim(AuthorityTier.AskMeFirst)]);

        await agent.ProcessAsync(envelope);

        // Specialist should NOT receive any message — wait briefly and verify timeout
        var completed = await Task.WhenAny(
            specialistReceived.Task,
            Task.Delay(TimeSpan.FromMilliseconds(200)));
        Assert.NotEqual(specialistReceived.Task, completed);
    }

    [Fact]
    public async Task JustDoIt_DispatchesWithoutGating()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(authorityTier: "JustDoIt");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var specialistReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            specialistReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        // No authority claims = defaults to JustDoIt
        var envelope = CreateEnvelope("Draft email");

        await agent.ProcessAsync(envelope);

        var received = await specialistReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        // Verify it's NOT a PlanProposal
        Assert.IsNotType<PlanProposal>(received.Message);
    }

    [Fact]
    public async Task DoItAndShowMe_DispatchesWithoutGating()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(authorityTier: "DoItAndShowMe");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var specialistReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            specialistReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        var envelope = CreateEnvelope(
            "Draft email",
            claims: [CreateClaim(AuthorityTier.DoItAndShowMe)]);

        await agent.ProcessAsync(envelope);

        var received = await specialistReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        Assert.IsNotType<PlanProposal>(received.Message);
    }

    // --- Approval response ---

    [Fact]
    public async Task ApprovalResponse_ResumesDispatch()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        // Step 1: Gate the message
        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        var envelope = CreateEnvelope(
            "Send email",
            replyTo: "agent.requester",
            claims: [CreateClaim(AuthorityTier.AskMeFirst)]);

        await agent.ProcessAsync(envelope);

        var proposalMsg = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = Assert.IsType<PlanProposal>(proposalMsg.Message);

        // Step 2: Send approval
        var specialistReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            specialistReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var approvalResponse = new PlanApprovalResponse
        {
            IsApproved = true,
            WorkflowReferenceCode = proposal.WorkflowReferenceCode
        };
        var approvalEnvelope = new MessageEnvelope
        {
            Message = approvalResponse,
            ReferenceCode = proposal.WorkflowReferenceCode,
            Context = new MessageContext { FromAgentId = "founder" }
        };

        await agent.ProcessAsync(approvalEnvelope);

        var dispatched = await specialistReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(dispatched);
    }

    [Fact]
    public async Task RejectionResponse_DoesNotDispatch()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        // Step 1: Gate
        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        var envelope = CreateEnvelope(
            "Send email",
            replyTo: "agent.requester",
            claims: [CreateClaim(AuthorityTier.AskMeFirst)]);

        await agent.ProcessAsync(envelope);

        var proposalMsg = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = Assert.IsType<PlanProposal>(proposalMsg.Message);

        // Step 2: Reject
        var rejectionReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.requester", e =>
        {
            rejectionReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var rejectionResponse = new PlanApprovalResponse
        {
            IsApproved = false,
            RejectionReason = "Too risky",
            WorkflowReferenceCode = proposal.WorkflowReferenceCode
        };
        var rejectionEnvelope = new MessageEnvelope
        {
            Message = rejectionResponse,
            ReferenceCode = proposal.WorkflowReferenceCode,
            Context = new MessageContext { FromAgentId = "founder" }
        };

        await agent.ProcessAsync(rejectionEnvelope);

        var rejection = await rejectionReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var textMsg = Assert.IsType<TextMessage>(rejection.Message);
        Assert.Contains("Too risky", textMsg.Content);
    }

    [Fact]
    public async Task RejectionResponse_RemovesPendingPlan()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        // Step 1: Gate
        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        var envelope = CreateEnvelope(
            "Send email",
            replyTo: "agent.requester",
            claims: [CreateClaim(AuthorityTier.AskMeFirst)]);

        await agent.ProcessAsync(envelope);

        var proposalMsg = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = Assert.IsType<PlanProposal>(proposalMsg.Message);

        // Verify plan exists before rejection
        var planBefore = await _pendingPlanStore.GetAsync(proposal.WorkflowReferenceCode);
        Assert.NotNull(planBefore);

        // Step 2: Reject
        await _bus.StartConsumingAsync("agent.requester", _ => Task.CompletedTask);

        var rejectionResponse = new PlanApprovalResponse
        {
            IsApproved = false,
            RejectionReason = "Not needed",
            WorkflowReferenceCode = proposal.WorkflowReferenceCode
        };
        var rejectionEnvelope = new MessageEnvelope
        {
            Message = rejectionResponse,
            ReferenceCode = proposal.WorkflowReferenceCode,
            Context = new MessageContext { FromAgentId = "founder" }
        };

        await agent.ProcessAsync(rejectionEnvelope);

        // Plan should be removed after rejection
        var planAfter = await _pendingPlanStore.GetAsync(proposal.WorkflowReferenceCode);
        Assert.Null(planAfter);
    }

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}
