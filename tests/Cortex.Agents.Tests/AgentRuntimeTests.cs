using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class AgentRuntimeTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _registry = new();
    private readonly AgentRuntime _runtime;

    public AgentRuntimeTests()
    {
        _runtime = new AgentRuntime(
            _bus,
            _registry,
            [],  // no startup agents
            NullLoggerFactory.Instance);
    }

    private static MessageEnvelope CreateEnvelope(string content, string? replyTo = null) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo }
        };

    [Fact]
    public async Task StartAsync_WithStartupAgents_StartsAll()
    {
        var agent = new EchoAgent();
        var runtime = new AgentRuntime(
            _bus,
            _registry,
            [agent],
            NullLoggerFactory.Instance);

        await runtime.StartAsync(CancellationToken.None);

        Assert.Contains("echo-agent", runtime.RunningAgentIds);

        await runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAgentAsync_DynamicAgent_ReturnsAgentId()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        var agentId = await rt.StartAgentAsync(new EchoAgent());

        Assert.Equal("echo-agent", agentId);
        Assert.Contains("echo-agent", _runtime.RunningAgentIds);

        await _runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAgentAsync_RemovesFromRunning()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        await rt.StartAgentAsync(new EchoAgent());
        await rt.StopAgentAsync("echo-agent");

        Assert.DoesNotContain("echo-agent", _runtime.RunningAgentIds);

        await _runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_StopsAllAgents()
    {
        var runtime = new AgentRuntime(
            _bus,
            _registry,
            [new EchoAgent()],
            NullLoggerFactory.Instance);

        await runtime.StartAsync(CancellationToken.None);
        Assert.Single(runtime.RunningAgentIds);

        await runtime.StopAsync(CancellationToken.None);

        Assert.Empty(runtime.RunningAgentIds);
    }

    [Fact]
    public async Task DynamicAgent_ReceivesAndReplies()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        await rt.StartAgentAsync(new EchoAgent());

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

        await _runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAgentAsync_WithTeamId_TracksTeamMembership()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        await rt.StartAgentAsync(new EchoAgent(), "team-alpha");

        var teamAgents = rt.GetTeamAgentIds("team-alpha");
        Assert.Single(teamAgents);
        Assert.Equal("echo-agent", teamAgents[0]);

        await _runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopTeamAsync_StopsAllTeamAgents()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        await rt.StartAgentAsync(new EchoAgent(), "team-alpha");

        await rt.StopTeamAsync("team-alpha");

        Assert.Empty(rt.GetTeamAgentIds("team-alpha"));
        Assert.DoesNotContain("echo-agent", _runtime.RunningAgentIds);

        await _runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GetTeamAgentIds_UnknownTeam_ReturnsEmpty()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        var teamAgents = rt.GetTeamAgentIds("nonexistent");

        Assert.Empty(teamAgents);

        await _runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAgentAsync_RemovesFromTeamTracking()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        await rt.StartAgentAsync(new EchoAgent(), "team-alpha");

        await rt.StopAgentAsync("echo-agent");

        Assert.Empty(rt.GetTeamAgentIds("team-alpha"));

        await _runtime.StopAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _bus.DisposeAsync();
    }
}
