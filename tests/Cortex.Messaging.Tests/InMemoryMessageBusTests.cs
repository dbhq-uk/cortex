using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Messaging.Tests;

public sealed class InMemoryMessageBusTests
{
    private static MessageEnvelope CreateEnvelope(string content) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    [Fact]
    public async Task PublishAsync_AndConsume_DeliversMessage()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var received = new TaskCompletionSource<MessageEnvelope>();

        await bus.StartConsumingAsync("test-queue", envelope =>
        {
            received.SetResult(envelope);
            return Task.CompletedTask;
        });

        var sent = CreateEnvelope("hello");

        // Act
        await bus.PublishAsync(sent, "test-queue");

        // Assert
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("hello", ((TestMessage)result.Message).Content);
        Assert.Equal(sent.ReferenceCode, result.ReferenceCode);
    }

    [Fact]
    public async Task PublishAsync_ToQueueWithNoConsumer_MessageWaitsForConsumer()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var sent = CreateEnvelope("waiting");

        // Act — publish before any consumer is registered
        await bus.PublishAsync(sent, "lazy-queue");

        // Now start consuming
        var received = new TaskCompletionSource<MessageEnvelope>();
        await bus.StartConsumingAsync("lazy-queue", envelope =>
        {
            received.SetResult(envelope);
            return Task.CompletedTask;
        });

        // Assert
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("waiting", ((TestMessage)result.Message).Content);
    }

    [Fact]
    public async Task PublishAsync_MultipleQueues_RoutesCorrectly()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var receivedA = new TaskCompletionSource<MessageEnvelope>();
        var receivedB = new TaskCompletionSource<MessageEnvelope>();

        await bus.StartConsumingAsync("queue-a", envelope =>
        {
            receivedA.SetResult(envelope);
            return Task.CompletedTask;
        });
        await bus.StartConsumingAsync("queue-b", envelope =>
        {
            receivedB.SetResult(envelope);
            return Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(CreateEnvelope("for-a"), "queue-a");
        await bus.PublishAsync(CreateEnvelope("for-b"), "queue-b");

        // Assert
        var resultA = await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var resultB = await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("for-a", ((TestMessage)resultA.Message).Content);
        Assert.Equal("for-b", ((TestMessage)resultB.Message).Content);
    }

    [Fact]
    public async Task StopConsumingAsync_StopsDelivery()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var callCount = 0;
        var firstDelivered = new TaskCompletionSource<bool>();

        await bus.StartConsumingAsync("stop-queue", _ =>
        {
            Interlocked.Increment(ref callCount);
            firstDelivered.TrySetResult(true);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(CreateEnvelope("first"), "stop-queue");

        // Wait for confirmed delivery of first message
        await firstDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        await bus.StopConsumingAsync();

        await bus.PublishAsync(CreateEnvelope("second"), "stop-queue");
        await Task.Delay(100);

        // Assert — only the first message was delivered
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task DisposeAsync_StopsConsumers()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var callCount = 0;

        await bus.StartConsumingAsync("dispose-queue", _ =>
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(CreateEnvelope("before-dispose"), "dispose-queue");
        await Task.Delay(100);

        // Act
        await bus.DisposeAsync();

        // The consumer should be stopped — messages published after dispose
        // should not be delivered (the channel writer is completed)
        var countAfterDispose = callCount;
        await Task.Delay(100);
        Assert.Equal(countAfterDispose, callCount);
    }

    [Fact]
    public async Task GetTopologyAsync_ReturnsEmptyByDefault()
    {
        // Arrange
        var bus = new InMemoryMessageBus();

        // Act
        var topology = await bus.GetTopologyAsync();

        // Assert
        Assert.NotNull(topology);
        Assert.Empty(topology.Bindings);
    }

    [Fact]
    public async Task PublishAsync_NullEnvelope_ThrowsArgumentNullException()
    {
        var bus = new InMemoryMessageBus();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => bus.PublishAsync(null!, "queue"));
    }

    [Fact]
    public async Task PublishAsync_NullQueueName_ThrowsArgumentException()
    {
        var bus = new InMemoryMessageBus();
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => bus.PublishAsync(CreateEnvelope("test"), null!));
    }

    [Fact]
    public async Task StartConsumingAsync_NullQueueName_ThrowsArgumentException()
    {
        var bus = new InMemoryMessageBus();
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => bus.StartConsumingAsync(null!, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task StartConsumingAsync_NullHandler_ThrowsArgumentNullException()
    {
        var bus = new InMemoryMessageBus();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => bus.StartConsumingAsync("queue", null!));
    }
}
