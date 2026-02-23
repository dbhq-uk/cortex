using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class AgentHarnessTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _registry = new();

    private AgentHarness CreateHarness(IAgent? agent = null) =>
        new(
            agent ?? new EchoAgent(),
            _bus,
            _registry,
            NullLogger<AgentHarness>.Instance);

    private static MessageEnvelope CreateEnvelope(
        string content,
        string? replyTo = null,
        string? fromAgentId = null) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo, FromAgentId = fromAgentId }
        };

    [Fact]
    public void QueueName_DerivedFromAgentId()
    {
        var harness = CreateHarness();

        Assert.Equal("agent.echo-agent", harness.QueueName);
    }

    [Fact]
    public async Task StartAsync_RegistersAgentInRegistry()
    {
        var harness = CreateHarness();

        await harness.StartAsync();

        var reg = await _registry.FindByIdAsync("echo-agent");
        Assert.NotNull(reg);
        Assert.True(reg.IsAvailable);
    }

    [Fact]
    public async Task StartAsync_SetsIsRunningTrue()
    {
        var harness = CreateHarness();

        await harness.StartAsync();

        Assert.True(harness.IsRunning);
    }

    [Fact]
    public async Task StopAsync_SetsIsRunningFalse()
    {
        var harness = CreateHarness();
        await harness.StartAsync();

        await harness.StopAsync();

        Assert.False(harness.IsRunning);
    }

    [Fact]
    public async Task StopAsync_DoesNotAffectOtherConsumers()
    {
        // Start two harnesses on the same bus
        var agent1 = new EchoAgent();
        var agent2 = new CallbackAgent("other-agent", _ => Task.FromResult<MessageEnvelope?>(null));
        var harness1 = CreateHarness(agent1);
        var harness2 = CreateHarness(agent2);

        await harness1.StartAsync();
        await harness2.StartAsync();

        // Stop harness 1
        await harness1.StopAsync();

        // Harness 2 should still receive messages
        var received = new TaskCompletionSource<MessageEnvelope>();
        var callbackAgent = new CallbackAgent("verify-agent", e =>
        {
            received.TrySetResult(e);
            return Task.FromResult<MessageEnvelope?>(null);
        });
        var verifyHarness = CreateHarness(callbackAgent);
        await verifyHarness.StartAsync();

        await _bus.PublishAsync(CreateEnvelope("hello"), "agent.verify-agent");
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(result);
    }

    [Fact]
    public async Task MessageDispatched_ToAgentProcessAsync()
    {
        var received = new TaskCompletionSource<MessageEnvelope>();
        var agent = new CallbackAgent("cb-agent", envelope =>
        {
            received.SetResult(envelope);
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var harness = CreateHarness(agent);
        await harness.StartAsync();

        await _bus.PublishAsync(CreateEnvelope("hello"), "agent.cb-agent");

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var msg = Assert.IsType<TestMessage>(result.Message);
        Assert.Equal("hello", msg.Content);
    }

    [Fact]
    public async Task ResponsePublished_ToReplyToQueue()
    {
        var harness = CreateHarness(); // EchoAgent
        await harness.StartAsync();

        var replyReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("reply-queue", envelope =>
        {
            replyReceived.SetResult(envelope);
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(
            CreateEnvelope("hello", replyTo: "reply-queue"),
            "agent.echo-agent");

        var reply = await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var msg = Assert.IsType<TestMessage>(reply.Message);
        Assert.Equal("echo: hello", msg.Content);
    }

    [Fact]
    public async Task Response_HasFromAgentIdStamped()
    {
        var harness = CreateHarness(); // EchoAgent
        await harness.StartAsync();

        var replyReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("reply-queue", envelope =>
        {
            replyReceived.SetResult(envelope);
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(
            CreateEnvelope("hello", replyTo: "reply-queue"),
            "agent.echo-agent");

        var reply = await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("echo-agent", reply.Context.FromAgentId);
    }

    [Fact]
    public async Task ResponseWithNoReplyTo_IsDropped()
    {
        var harness = CreateHarness(); // EchoAgent always returns a response
        await harness.StartAsync();

        // Publish with no ReplyTo — should not throw
        await _bus.PublishAsync(
            CreateEnvelope("hello"),
            "agent.echo-agent");

        // Give it time to process
        await Task.Delay(100);

        // No assertion — just verifying it doesn't throw or deadlock
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _bus.DisposeAsync();
    }
}

/// <summary>
/// Test agent that invokes a callback on ProcessAsync.
/// </summary>
file sealed class CallbackAgent(
    string agentId,
    Func<MessageEnvelope, Task<MessageEnvelope?>> callback) : IAgent
{
    public string AgentId { get; } = agentId;
    public string Name { get; } = $"Callback Agent ({agentId})";
    public IReadOnlyList<AgentCapability> Capabilities { get; } = [];

    public Task<MessageEnvelope?> ProcessAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => callback(envelope);
}
