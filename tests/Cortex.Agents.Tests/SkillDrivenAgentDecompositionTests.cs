// tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Tests.Pipeline;
using Cortex.Agents.Workflows;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

/// <summary>
/// Tests for <see cref="SkillDrivenAgent"/> decomposition result parsing
/// and single-task routing with backward compatibility.
/// </summary>
public sealed class SkillDrivenAgentDecompositionTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly InMemoryWorkflowTracker _workflowTracker = new();
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;

    public SkillDrivenAgentDecompositionTests()
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
            contextProvider: null,
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

    private void SetDecomposeResult(object result)
    {
        var json = JsonSerializer.SerializeToElement(result);
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

    // --- Single-task routing ---

    [Fact]
    public async Task ProcessAsync_SingleTask_RoutesToMatchingAgent()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = new[] { new { capability = "email-drafting", description = "Draft reply", authorityTier = "DoItAndShowMe" } },
            summary = "Draft email reply",
            confidence = 0.9
        });
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
    public async Task ProcessAsync_SingleTask_CreatesDelegationRecord()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = new[] { new { capability = "email-drafting", description = "Draft reply", authorityTier = "DoItAndShowMe" } },
            summary = "Draft email reply",
            confidence = 0.9
        });
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var delegations = await _delegationTracker.GetByAssigneeAsync("email-agent");
        Assert.Single(delegations);
        Assert.Equal("cos", delegations[0].DelegatedBy);
        Assert.Equal("email-agent", delegations[0].DelegatedTo);
        Assert.Equal("Draft reply", delegations[0].Description);
        Assert.Equal(DelegationStatus.Assigned, delegations[0].Status);
    }

    [Fact]
    public async Task ProcessAsync_SingleTask_DoesNotCreateWorkflow()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = new[] { new { capability = "email-drafting", description = "Draft reply", authorityTier = "DoItAndShowMe" } },
            summary = "Draft email reply",
            confidence = 0.9
        });
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

        // Single-task routing should not create a workflow record
        var workflow = await _workflowTracker.GetAsync(routedMsg.ReferenceCode);
        Assert.Null(workflow);
    }

    // --- Escalation ---

    [Fact]
    public async Task ProcessAsync_LowConfidence_Escalates()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = new[] { new { capability = "email-drafting", description = "Draft reply", authorityTier = "DoItAndShowMe" } },
            summary = "Draft email reply",
            confidence = 0.3 // below 0.6 threshold
        });
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
    public async Task ProcessAsync_MalformedResult_Escalates()
    {
        RegisterDecomposeSkill();
        // Set garbage result that cannot be parsed as decomposition
        var garbage = JsonSerializer.SerializeToElement(new { foo = "bar" });
        _fakeExecutor.SetResult("cos-decompose", garbage);

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
    public async Task ProcessAsync_EmptyTasks_Escalates()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = Array.Empty<object>(),
            summary = "Empty",
            confidence = 0.9
        });

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

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}
