// tests/Cortex.Agents.Tests/SkillDrivenAgentTests.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Tests.Pipeline;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class SkillDrivenAgentTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;

    public SkillDrivenAgentTests()
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
            NullLogger<SkillDrivenAgent>.Instance);
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
        Pipeline = ["cos-triage"],
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

    private void RegisterTriageSkill()
    {
        _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "CoS Triage",
            Description = "Triage",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        }).GetAwaiter().GetResult();
    }

    private void SetTriageResult(
        string capability,
        string authorityTier = "DoItAndShowMe",
        double confidence = 0.9,
        string summary = "Test task")
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            capability,
            authorityTier,
            summary,
            confidence
        });
        _fakeExecutor.SetResult("cos-triage", json);
    }

    private async Task RegisterSpecialistAgent(
        string agentId,
        string capabilityName)
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

    // --- Routing happy path ---

    [Fact]
    public async Task ProcessAsync_RoutesToMatchingAgent()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        var result = await agent.ProcessAsync(CreateEnvelope("Draft reply to John"));

        Assert.Null(result);

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(routedMsg);
    }

    [Fact]
    public async Task ProcessAsync_StampsFromAgentId()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("cos", routedMsg.Context.FromAgentId);
    }

    [Fact]
    public async Task ProcessAsync_PreservesReplyTo()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test", replyTo: "agent.human-user"));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("agent.human-user", routedMsg.Context.ReplyTo);
    }

    [Fact]
    public async Task ProcessAsync_CreatesDelegationRecord()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting", summary: "Draft reply");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var delegations = await _delegationTracker.GetByAssigneeAsync("email-agent");
        Assert.Single(delegations);
        Assert.Equal("cos", delegations[0].DelegatedBy);
        Assert.Equal("email-agent", delegations[0].DelegatedTo);
        Assert.Equal("Draft reply", delegations[0].Description);
        Assert.Equal(DelegationStatus.Assigned, delegations[0].Status);
    }

    [Fact]
    public async Task ProcessAsync_ExcludesSelfFromRouting()
    {
        RegisterTriageSkill();
        SetTriageResult("triage");

        // Register the CoS itself with the "triage" capability and another agent
        await _agentRegistry.RegisterAsync(new AgentRegistration
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = "triage", Description = "Triage" }],
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = true
        });
        await RegisterSpecialistAgent("other-agent", "triage");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.other-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(routedMsg);
    }

    // --- Escalation ---

    [Fact]
    public async Task ProcessAsync_NoTriageResult_EscalatesToFounder()
    {
        RegisterTriageSkill();
        // Don't set any triage result — executor returns null

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    [Fact]
    public async Task ProcessAsync_LowConfidence_EscalatesToFounder()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting", confidence: 0.3); // below 0.6 threshold
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    [Fact]
    public async Task ProcessAsync_NoMatchingCapability_EscalatesToFounder()
    {
        RegisterTriageSkill();
        SetTriageResult("nonexistent-capability");
        // Don't register any agent with that capability

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    [Fact]
    public async Task ProcessAsync_Escalation_CreatesDelegationRecord()
    {
        RegisterTriageSkill();
        SetTriageResult("nonexistent-capability");

        await _bus.StartConsumingAsync("agent.founder", _ => Task.CompletedTask);

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var delegations = await _delegationTracker.GetByAssigneeAsync("agent.founder");
        Assert.Single(delegations);
        Assert.Contains("Escalated", delegations[0].Description);
    }

    // --- Authority narrowing ---

    [Fact]
    public async Task ProcessAsync_AuthorityNarrowing_NeverExceedsInbound()
    {
        RegisterTriageSkill();
        // Triage suggests AskMeFirst, but inbound only has DoItAndShowMe
        SetTriageResult("email-drafting", authorityTier: "AskMeFirst");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var inboundClaim = new AuthorityClaim
        {
            GrantedBy = "founder",
            GrantedTo = "cos",
            Tier = AuthorityTier.DoItAndShowMe,
            GrantedAt = DateTimeOffset.UtcNow
        };

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test", claims: [inboundClaim]));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var outboundClaim = Assert.Single(routedMsg.AuthorityClaims);
        Assert.Equal(AuthorityTier.DoItAndShowMe, outboundClaim.Tier);
    }

    [Fact]
    public async Task ProcessAsync_NoInboundClaims_DefaultsToJustDoIt()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting", authorityTier: "AskMeFirst");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test", claims: []));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var outboundClaim = Assert.Single(routedMsg.AuthorityClaims);
        Assert.Equal(AuthorityTier.JustDoIt, outboundClaim.Tier);
    }

    // --- Agent identity ---

    [Fact]
    public void AgentId_ComesFromPersona()
    {
        var agent = CreateAgent();

        Assert.Equal("cos", agent.AgentId);
    }

    [Fact]
    public void Name_ComesFromPersona()
    {
        var agent = CreateAgent();

        Assert.Equal("Chief of Staff", agent.Name);
    }

    [Fact]
    public void Capabilities_ComesFromPersona()
    {
        var agent = CreateAgent();

        Assert.Single(agent.Capabilities);
        Assert.Equal("triage", agent.Capabilities[0].Name);
    }

    [Fact]
    public void AgentType_ComesFromPersona()
    {
        var agent = CreateAgent();
        var typed = Assert.IsAssignableFrom<IAgentTypeProvider>(agent);

        Assert.Equal("ai", typed.AgentType);
    }

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}
