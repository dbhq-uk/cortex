// tests/Cortex.Agents.Tests/SkillDrivenAgentContextTests.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Tests.Pipeline;
using Cortex.Core.Context;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

/// <summary>
/// Tests for <see cref="SkillDrivenAgent"/> integration with <see cref="IContextProvider"/>.
/// </summary>
public sealed class SkillDrivenAgentContextTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;

    public SkillDrivenAgentContextTests()
    {
        _refCodeGenerator = new SequentialReferenceCodeGenerator(
            new InMemorySequenceStore(), TimeProvider.System);
    }

    private SkillDrivenAgent CreateAgent(IContextProvider? contextProvider = null)
    {
        var persona = new PersonaDefinition
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = "triage", Description = "Triage" }],
            Pipeline = ["cos-triage"],
            EscalationTarget = "agent.founder",
            ConfidenceThreshold = 0.6
        };

        var pipelineRunner = new SkillPipelineRunner(
            _skillRegistry,
            [_fakeExecutor],
            NullLogger<SkillPipelineRunner>.Instance);

        return new SkillDrivenAgent(
            persona,
            pipelineRunner,
            _agentRegistry,
            _delegationTracker,
            _refCodeGenerator,
            _bus,
            NullLogger<SkillDrivenAgent>.Instance,
            contextProvider);
    }

    private async Task RegisterTriageSkillAsync()
    {
        await _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "Triage",
            Description = "Test triage",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        });
    }

    [Fact]
    public async Task ProcessAsync_WithContextProvider_InjectsBusinessContext()
    {
        // Arrange
        await RegisterTriageSkillAsync();

        // Set low-confidence triage result so we get escalation (simplest code path)
        _fakeExecutor.SetResult("cos-triage", JsonSerializer.SerializeToElement(new
        {
            capability = "triage",
            authorityTier = "JustDoIt",
            summary = "test",
            confidence = 0.1
        }));

        // Set up escalation consumer so PublishAsync doesn't fail
        await _bus.StartConsumingAsync("agent.founder", _ => Task.CompletedTask);

        // Build the message and envelope first so we know the serialized form.
        // The context query uses the serialized JSON as Keywords, and
        // InMemoryContextProvider does a substring match (entry.Content.Contains(keywords)).
        // We store an entry whose content includes the serialized message JSON.
        var message = new TestMessage { Content = "billing question from Smith" };
        var serializedMessage = JsonSerializer.Serialize(message, message.GetType());

        var contextProvider = new InMemoryContextProvider();
        await contextProvider.StoreAsync(new ContextEntry
        {
            EntryId = "ctx-1",
            Content = $"[VIP] Smith billing context: {serializedMessage}",
            Category = ContextCategory.CustomerNote,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var agent = CreateAgent(contextProvider);

        var envelope = new MessageEnvelope
        {
            Message = message,
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = "reply-queue" }
        };

        // Act
        await agent.ProcessAsync(envelope);

        // Assert — the FakeSkillExecutor captures parameters passed to it
        Assert.Single(_fakeExecutor.Calls);
        var parameters = _fakeExecutor.Calls[0].Parameters;
        Assert.True(parameters.ContainsKey("businessContext"),
            "Expected 'businessContext' key in pipeline parameters");

        var businessContext = (string)parameters["businessContext"];
        Assert.Contains("CustomerNote", businessContext);
        Assert.Contains("Smith billing context", businessContext);
    }

    [Fact]
    public async Task ProcessAsync_WithoutContextProvider_StillWorks()
    {
        // Arrange
        await RegisterTriageSkillAsync();

        // Set low-confidence triage result so we get escalation (simplest code path)
        _fakeExecutor.SetResult("cos-triage", JsonSerializer.SerializeToElement(new
        {
            capability = "triage",
            authorityTier = "JustDoIt",
            summary = "test",
            confidence = 0.1
        }));

        // Set up escalation consumer
        await _bus.StartConsumingAsync("agent.founder", _ => Task.CompletedTask);

        // Create agent without context provider (backward compatibility)
        var agent = CreateAgent(contextProvider: null);

        var envelope = new MessageEnvelope
        {
            Message = new TestMessage { Content = "billing question from Smith" },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = "reply-queue" }
        };

        // Act
        await agent.ProcessAsync(envelope);

        // Assert — agent processes without error and no businessContext key
        Assert.Single(_fakeExecutor.Calls);
        var parameters = _fakeExecutor.Calls[0].Parameters;
        Assert.False(parameters.ContainsKey("businessContext"),
            "Expected no 'businessContext' key when IContextProvider is not supplied");
    }

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}
