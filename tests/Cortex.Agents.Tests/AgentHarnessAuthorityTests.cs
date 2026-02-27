using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class AgentHarnessAuthorityTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _registry = new();
    private readonly InMemoryAuthorityProvider _authorityProvider = new();

    private AgentHarness CreateHarness(IAgent agent, IAuthorityProvider? authorityProvider = null) =>
        new(
            agent,
            _bus,
            _registry,
            NullLogger<AgentHarness>.Instance,
            authorityProvider);

    private static MessageEnvelope CreateEnvelope(
        string content,
        string? replyTo = null,
        IReadOnlyList<AuthorityClaim>? claims = null) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo },
            AuthorityClaims = claims ?? []
        };

    [Fact]
    public async Task ValidClaim_ProcessesMessage()
    {
        var received = new TaskCompletionSource<MessageEnvelope>();
        var agent = new CallbackAgent("agent-1", envelope =>
        {
            received.SetResult(envelope);
            // Return a reply so we can verify end-to-end
            return Task.FromResult<MessageEnvelope?>(envelope with
            {
                Message = new TestMessage { Content = "processed" }
            });
        });

        var harness = CreateHarness(agent, _authorityProvider);
        await harness.StartAsync();

        var replyReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("reply-queue", envelope =>
        {
            replyReceived.SetResult(envelope);
            return Task.CompletedTask;
        });

        var claim = new AuthorityClaim
        {
            GrantedBy = "admin",
            GrantedTo = "agent-1",
            Tier = AuthorityTier.JustDoIt,
            GrantedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _bus.PublishAsync(
            CreateEnvelope("hello", replyTo: "reply-queue", claims: [claim]),
            "agent.agent-1");

        var reply = await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var msg = Assert.IsType<TestMessage>(reply.Message);
        Assert.Equal("processed", msg.Content);
    }

    [Fact]
    public async Task ExpiredClaim_DropsMessage()
    {
        var processed = new TaskCompletionSource<bool>();
        var agent = new CallbackAgent("agent-1", _ =>
        {
            processed.SetResult(true);
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var harness = CreateHarness(agent, _authorityProvider);
        await harness.StartAsync();

        var claim = new AuthorityClaim
        {
            GrantedBy = "admin",
            GrantedTo = "agent-1",
            Tier = AuthorityTier.JustDoIt,
            GrantedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) // Already expired
        };

        await _bus.PublishAsync(
            CreateEnvelope("hello", claims: [claim]),
            "agent.agent-1");

        // Give time for the message to be (not) processed
        var completed = await Task.WhenAny(
            processed.Task,
            Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.NotEqual(processed.Task, completed); // Should NOT have been processed
    }

    [Fact]
    public async Task ClaimGrantedToDifferentAgent_DropsMessage()
    {
        var processed = new TaskCompletionSource<bool>();
        var agent = new CallbackAgent("agent-1", _ =>
        {
            processed.SetResult(true);
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var harness = CreateHarness(agent, _authorityProvider);
        await harness.StartAsync();

        var claim = new AuthorityClaim
        {
            GrantedBy = "admin",
            GrantedTo = "agent-2", // Wrong agent
            Tier = AuthorityTier.JustDoIt,
            GrantedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _bus.PublishAsync(
            CreateEnvelope("hello", claims: [claim]),
            "agent.agent-1");

        // Give time for the message to be (not) processed
        var completed = await Task.WhenAny(
            processed.Task,
            Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.NotEqual(processed.Task, completed); // Should NOT have been processed
    }

    [Fact]
    public async Task NoClaims_ProcessesMessage()
    {
        var received = new TaskCompletionSource<MessageEnvelope>();
        var agent = new CallbackAgent("agent-1", envelope =>
        {
            received.SetResult(envelope);
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var harness = CreateHarness(agent, _authorityProvider);
        await harness.StartAsync();

        // No claims at all — should pass through
        await _bus.PublishAsync(
            CreateEnvelope("hello"),
            "agent.agent-1");

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var msg = Assert.IsType<TestMessage>(result.Message);
        Assert.Equal("hello", msg.Content);
    }

    [Fact]
    public async Task NoProvider_ProcessesMessage()
    {
        var received = new TaskCompletionSource<MessageEnvelope>();
        var agent = new CallbackAgent("agent-1", envelope =>
        {
            received.SetResult(envelope);
            return Task.FromResult<MessageEnvelope?>(null);
        });

        // No authority provider — backward compatible, no validation
        var harness = CreateHarness(agent, authorityProvider: null);
        await harness.StartAsync();

        var claim = new AuthorityClaim
        {
            GrantedBy = "admin",
            GrantedTo = "agent-1",
            Tier = AuthorityTier.JustDoIt,
            GrantedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _bus.PublishAsync(
            CreateEnvelope("hello", claims: [claim]),
            "agent.agent-1");

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var msg = Assert.IsType<TestMessage>(result.Message);
        Assert.Equal("hello", msg.Content);
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
