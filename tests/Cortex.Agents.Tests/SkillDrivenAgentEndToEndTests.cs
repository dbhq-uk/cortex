// tests/Cortex.Agents.Tests/SkillDrivenAgentEndToEndTests.cs
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

/// <summary>
/// End-to-end test: CoS agent receives a message, triages via mocked LLM skill,
/// routes to specialist, and tracks delegation. Uses real InMemoryMessageBus,
/// real AgentHarness, real delegation tracker — only the LLM is faked.
/// </summary>
public sealed class SkillDrivenAgentEndToEndTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;

    public SkillDrivenAgentEndToEndTests()
    {
        _refCodeGenerator = new SequentialReferenceCodeGenerator(
            new InMemorySequenceStore(), TimeProvider.System);
    }

    [Fact]
    public async Task FullFlow_MessageRoutedThroughCosToSpecialist()
    {
        // --- Arrange ---

        // Register the triage skill
        await _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "CoS Triage",
            Description = "Triage",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        });

        // Configure triage result
        var triageJson = JsonSerializer.SerializeToElement(new
        {
            capability = "email-drafting",
            authorityTier = "DoItAndShowMe",
            summary = "Draft reply to client email",
            confidence = 0.92
        });
        _fakeExecutor.SetResult("cos-triage", triageJson);

        // Create and start the CoS agent via harness
        var persona = new PersonaDefinition
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

        var pipelineRunner = new SkillPipelineRunner(
            _skillRegistry,
            [_fakeExecutor],
            NullLogger<SkillPipelineRunner>.Instance);

        var cosAgent = new SkillDrivenAgent(
            persona,
            pipelineRunner,
            _agentRegistry,
            _delegationTracker,
            _refCodeGenerator,
            _bus,
            NullLogger<SkillDrivenAgent>.Instance);

        var cosHarness = new AgentHarness(
            cosAgent,
            _bus,
            _agentRegistry,
            NullLogger<AgentHarness>.Instance);

        await cosHarness.StartAsync();

        // Create and start a specialist agent (echo agent standing in)
        var specialistReceived = new TaskCompletionSource<MessageEnvelope>();
        var specialist = new CallbackAgent("email-agent", "email-drafting", envelope =>
        {
            specialistReceived.SetResult(envelope);
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var specialistHarness = new AgentHarness(
            specialist,
            _bus,
            _agentRegistry,
            NullLogger<AgentHarness>.Instance);

        await specialistHarness.StartAsync();

        // --- Act ---

        // Send a message to the CoS
        var envelope = new MessageEnvelope
        {
            Message = new TestMessage { Content = "Please draft a reply to John's email about the Q1 report" },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = "agent.human-user" },
            AuthorityClaims =
            [
                new AuthorityClaim
                {
                    GrantedBy = "founder",
                    GrantedTo = "cos",
                    Tier = AuthorityTier.DoItAndShowMe,
                    GrantedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        await _bus.PublishAsync(envelope, "agent.cos");

        // --- Assert ---

        // Specialist received the routed message
        var routedMsg = await specialistReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(routedMsg);
        Assert.Equal("cos", routedMsg.Context.FromAgentId);
        Assert.Equal("agent.human-user", routedMsg.Context.ReplyTo);

        // Authority was set correctly
        var outboundClaim = Assert.Single(routedMsg.AuthorityClaims);
        Assert.Equal(AuthorityTier.DoItAndShowMe, outboundClaim.Tier);
        Assert.Equal("cos", outboundClaim.GrantedBy);
        Assert.Equal("email-agent", outboundClaim.GrantedTo);

        // Delegation was tracked
        var delegations = await _delegationTracker.GetByAssigneeAsync("email-agent");
        Assert.Single(delegations);
        Assert.Equal("cos", delegations[0].DelegatedBy);
        Assert.Equal("Draft reply to client email", delegations[0].Description);
        Assert.Equal(DelegationStatus.Assigned, delegations[0].Status);

        // --- Cleanup ---
        await cosHarness.StopAsync();
        await specialistHarness.StopAsync();
    }

    [Fact]
    public async Task FullFlow_UnroutableMessage_EscalatesToFounder()
    {
        // Register the triage skill
        await _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "CoS Triage",
            Description = "Triage",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        });

        // Triage returns a capability no one has
        var triageJson = JsonSerializer.SerializeToElement(new
        {
            capability = "quantum-physics",
            authorityTier = "JustDoIt",
            summary = "Solve quantum equations",
            confidence = 0.95
        });
        _fakeExecutor.SetResult("cos-triage", triageJson);

        // Create CoS
        var persona = new PersonaDefinition
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

        var pipelineRunner = new SkillPipelineRunner(
            _skillRegistry,
            [_fakeExecutor],
            NullLogger<SkillPipelineRunner>.Instance);

        var cosAgent = new SkillDrivenAgent(
            persona,
            pipelineRunner,
            _agentRegistry,
            _delegationTracker,
            _refCodeGenerator,
            _bus,
            NullLogger<SkillDrivenAgent>.Instance);

        var cosHarness = new AgentHarness(
            cosAgent,
            _bus,
            _agentRegistry,
            NullLogger<AgentHarness>.Instance);

        await cosHarness.StartAsync();

        // Listen on founder queue
        var founderReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            founderReceived.SetResult(e);
            return Task.CompletedTask;
        });

        // Send a message
        var envelope = new MessageEnvelope
        {
            Message = new TestMessage { Content = "Solve the Schrodinger equation" },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = "agent.human-user" }
        };

        await _bus.PublishAsync(envelope, "agent.cos");

        // Founder received the escalation
        var escalatedMsg = await founderReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(escalatedMsg);
        Assert.Equal("cos", escalatedMsg.Context.FromAgentId);

        // Delegation tracked
        var delegations = await _delegationTracker.GetByAssigneeAsync("agent.founder");
        Assert.Single(delegations);
        Assert.Contains("Escalated", delegations[0].Description);

        await cosHarness.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}

/// <summary>
/// Test agent with configurable callback and a specific capability.
/// </summary>
file sealed class CallbackAgent(
    string agentId,
    string capabilityName,
    Func<MessageEnvelope, Task<MessageEnvelope?>> callback) : IAgent
{
    public string AgentId { get; } = agentId;
    public string Name { get; } = $"Agent {agentId}";
    public IReadOnlyList<AgentCapability> Capabilities { get; } =
    [
        new AgentCapability { Name = capabilityName, Description = capabilityName }
    ];

    public Task<MessageEnvelope?> ProcessAsync(
        MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => callback(envelope);
}
