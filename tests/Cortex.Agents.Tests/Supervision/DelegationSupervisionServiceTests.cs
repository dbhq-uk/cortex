using Cortex.Agents.Delegation;
using Cortex.Agents.Supervision;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests.Supervision;

public sealed class DelegationSupervisionServiceTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemoryRetryCounter _retryCounter = new();
    private int _sequenceCounter;

    public async ValueTask DisposeAsync()
    {
        await _bus.DisposeAsync();
    }

    private DelegationRecord CreateOverdueRecord(string delegatedTo = "agent-1") => new()
    {
        ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, Interlocked.Increment(ref _sequenceCounter)),
        DelegatedBy = "cos",
        DelegatedTo = delegatedTo,
        Description = "Overdue task",
        Status = DelegationStatus.Assigned,
        AssignedAt = DateTimeOffset.UtcNow.AddHours(-2),
        DueAt = DateTimeOffset.UtcNow.AddHours(-1)
    };

    private DelegationRecord CreateFutureDueRecord(string delegatedTo = "agent-1") => new()
    {
        ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, Interlocked.Increment(ref _sequenceCounter)),
        DelegatedBy = "cos",
        DelegatedTo = delegatedTo,
        Description = "Future task",
        Status = DelegationStatus.Assigned,
        AssignedAt = DateTimeOffset.UtcNow,
        DueAt = DateTimeOffset.UtcNow.AddHours(1)
    };

    private DelegationSupervisionService CreateService(
        IAgentRuntime? agentRuntime = null,
        SupervisionOptions? options = null) =>
        new(
            _delegationTracker,
            _retryCounter,
            _bus,
            NullLogger<DelegationSupervisionService>.Instance,
            options ?? new SupervisionOptions(),
            agentRuntime);

    [Fact]
    public async Task OverdueDelegation_PublishesSupervisionAlert()
    {
        // Arrange
        var record = CreateOverdueRecord();
        await _delegationTracker.DelegateAsync(record);

        MessageEnvelope? received = null;
        var tcs = new TaskCompletionSource();
        await _bus.StartConsumingAsync("agent.cos", envelope =>
        {
            received = envelope;
            tcs.TrySetResult();
            return Task.CompletedTask;
        });

        var sut = CreateService();

        // Act
        await sut.CheckOverdueAsync();

        // Assert
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        var alert = Assert.IsType<SupervisionAlert>(received.Message);
        Assert.Equal(record.ReferenceCode, alert.DelegationReferenceCode);
        Assert.Equal(record.DelegatedTo, alert.DelegatedTo);
        Assert.Equal(record.Description, alert.Description);
        Assert.Equal(1, alert.RetryCount);
        Assert.Equal(record.DueAt, alert.DueAt);
        Assert.True(alert.IsAgentRunning);
        Assert.Equal(record.ReferenceCode, received.ReferenceCode);
        Assert.Equal("supervision-service", received.Context.FromAgentId);
    }

    [Fact]
    public async Task MaxRetriesExceeded_PublishesEscalationAlert()
    {
        // Arrange
        var record = CreateOverdueRecord();
        await _delegationTracker.DelegateAsync(record);

        // Pre-fill retry counter to max (3 increments = max retries)
        await _retryCounter.IncrementAsync(record.ReferenceCode);
        await _retryCounter.IncrementAsync(record.ReferenceCode);
        await _retryCounter.IncrementAsync(record.ReferenceCode);

        MessageEnvelope? received = null;
        var tcs = new TaskCompletionSource();
        await _bus.StartConsumingAsync("agent.founder", envelope =>
        {
            received = envelope;
            tcs.TrySetResult();
            return Task.CompletedTask;
        });

        var sut = CreateService();

        // Act
        await sut.CheckOverdueAsync();

        // Assert — counter is now 4 which is > 3 (MaxRetries), so escalation
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        var alert = Assert.IsType<EscalationAlert>(received.Message);
        Assert.Equal(record.ReferenceCode, alert.DelegationReferenceCode);
        Assert.Equal(record.DelegatedTo, alert.DelegatedTo);
        Assert.Equal(record.Description, alert.Description);
        Assert.Equal(4, alert.RetryCount);
        Assert.Equal("Max retries exceeded", alert.Reason);
        Assert.Equal(record.ReferenceCode, received.ReferenceCode);
        Assert.Equal("supervision-service", received.Context.FromAgentId);
    }

    [Fact]
    public async Task NoOverdueDelegations_PublishesNothing()
    {
        // Arrange — create a record that is due in the future
        var record = CreateFutureDueRecord();
        await _delegationTracker.DelegateAsync(record);

        var published = false;
        await _bus.StartConsumingAsync("agent.cos", _ =>
        {
            published = true;
            return Task.CompletedTask;
        });
        await _bus.StartConsumingAsync("agent.founder", _ =>
        {
            published = true;
            return Task.CompletedTask;
        });

        var sut = CreateService();

        // Act
        await sut.CheckOverdueAsync();

        // Assert — give a small window for any messages to arrive
        await Task.Delay(100);
        Assert.False(published);
    }

    [Fact]
    public async Task IncrementsRetryCounter()
    {
        // Arrange
        var record = CreateOverdueRecord();
        await _delegationTracker.DelegateAsync(record);

        // Consume messages so publish doesn't block
        await _bus.StartConsumingAsync("agent.cos", _ => Task.CompletedTask);

        var sut = CreateService();

        // Act
        await sut.CheckOverdueAsync();
        await sut.CheckOverdueAsync();

        // Assert
        var count = await _retryCounter.GetCountAsync(record.ReferenceCode);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task DeadAgent_AlertIncludesIsAgentRunningFalse()
    {
        // Arrange
        var record = CreateOverdueRecord("agent-dead");
        await _delegationTracker.DelegateAsync(record);

        MessageEnvelope? received = null;
        var tcs = new TaskCompletionSource();
        await _bus.StartConsumingAsync("agent.cos", envelope =>
        {
            received = envelope;
            tcs.TrySetResult();
            return Task.CompletedTask;
        });

        // FakeAgentRuntime with empty list — agent-dead is NOT running
        var runtime = new FakeAgentRuntime([]);
        var sut = CreateService(agentRuntime: runtime);

        // Act
        await sut.CheckOverdueAsync();

        // Assert
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        var alert = Assert.IsType<SupervisionAlert>(received.Message);
        Assert.False(alert.IsAgentRunning);
    }

    [Fact]
    public async Task RunningAgent_AlertIncludesIsAgentRunningTrue()
    {
        // Arrange
        var record = CreateOverdueRecord("agent-alive");
        await _delegationTracker.DelegateAsync(record);

        MessageEnvelope? received = null;
        var tcs = new TaskCompletionSource();
        await _bus.StartConsumingAsync("agent.cos", envelope =>
        {
            received = envelope;
            tcs.TrySetResult();
            return Task.CompletedTask;
        });

        // FakeAgentRuntime with agent-alive in the list
        var runtime = new FakeAgentRuntime(["agent-alive"]);
        var sut = CreateService(agentRuntime: runtime);

        // Act
        await sut.CheckOverdueAsync();

        // Assert
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        var alert = Assert.IsType<SupervisionAlert>(received.Message);
        Assert.True(alert.IsAgentRunning);
    }

    [Fact]
    public async Task NoRuntime_DefaultsToAgentRunningTrue()
    {
        // Arrange
        var record = CreateOverdueRecord();
        await _delegationTracker.DelegateAsync(record);

        MessageEnvelope? received = null;
        var tcs = new TaskCompletionSource();
        await _bus.StartConsumingAsync("agent.cos", envelope =>
        {
            received = envelope;
            tcs.TrySetResult();
            return Task.CompletedTask;
        });

        // No runtime provided (null)
        var sut = CreateService(agentRuntime: null);

        // Act
        await sut.CheckOverdueAsync();

        // Assert
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(received);
        var alert = Assert.IsType<SupervisionAlert>(received.Message);
        Assert.True(alert.IsAgentRunning);
    }
}

/// <summary>
/// Minimal fake for IAgentRuntime used in supervision tests.
/// </summary>
file sealed class FakeAgentRuntime(IReadOnlyList<string> runningAgentIds) : IAgentRuntime
{
    public IReadOnlyList<string> RunningAgentIds { get; } = runningAgentIds;

    public Task<string> StartAgentAsync(IAgent agent, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<string> StartAgentAsync(IAgent agent, string teamId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task StopAgentAsync(string agentId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task StopTeamAsync(string teamId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public IReadOnlyList<string> GetTeamAgentIds(string teamId) =>
        throw new NotImplementedException();
}
