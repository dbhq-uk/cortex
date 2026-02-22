using Cortex.Core.Messages;
using Cortex.Core.References;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Messaging.RabbitMQ.Tests;

/// <summary>
/// Integration tests for <see cref="RabbitMqMessageBus"/>.
/// Requires a running RabbitMQ instance (see docker-compose.yml).
/// </summary>
[Trait("Category", "Integration")]
public sealed class RabbitMqMessageBusTests : IClassFixture<RabbitMqFixture>, IAsyncDisposable
{
    private readonly RabbitMqMessageBus _bus;

    public RabbitMqMessageBusTests(RabbitMqFixture fixture)
    {
        _bus = new RabbitMqMessageBus(
            fixture.Connection,
            Microsoft.Extensions.Options.Options.Create(fixture.Options),
            NullLogger<RabbitMqMessageBus>.Instance);
    }

    private static MessageEnvelope CreateEnvelope(string content) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    [Fact]
    public async Task PublishAndConsume_RoundTrip_DeliversMessage()
    {
        // Arrange
        var queueName = $"test-roundtrip-{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<MessageEnvelope>();

        await _bus.StartConsumingAsync(queueName, envelope =>
        {
            received.SetResult(envelope);
            return Task.CompletedTask;
        });

        var sent = CreateEnvelope("hello-rabbit");

        // Act
        await _bus.PublishAsync(sent, queueName);

        // Assert
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var message = Assert.IsType<TestMessage>(result.Message);
        Assert.Equal("hello-rabbit", message.Content);
        Assert.Equal(sent.ReferenceCode, result.ReferenceCode);
    }

    [Fact]
    public async Task PublishAndConsume_PreservesTypeHeader()
    {
        // Arrange
        var queueName = $"test-type-{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<MessageEnvelope>();

        await _bus.StartConsumingAsync(queueName, envelope =>
        {
            received.SetResult(envelope);
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(CreateEnvelope("typed"), queueName);

        // Assert
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsType<TestMessage>(result.Message);
    }

    [Fact]
    public async Task HandlerThrows_MessageNackedToDeadLetter()
    {
        // Arrange
        var queueName = $"test-deadletter-{Guid.NewGuid():N}";
        var deadLetterReceived = new TaskCompletionSource<bool>();

        // Start a consumer that always throws
        await _bus.StartConsumingAsync(queueName, _ =>
            throw new InvalidOperationException("Simulated failure"));

        // Start a dead letter consumer
        await _bus.StartConsumingAsync("cortex.deadletter.queue", _ =>
        {
            deadLetterReceived.TrySetResult(true);
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(CreateEnvelope("will-fail"), queueName);

        // Assert
        var wasDeadLettered = await deadLetterReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(wasDeadLettered);
    }

    [Fact]
    public async Task MultipleQueues_RoutesCorrectly()
    {
        // Arrange
        var queueA = $"test-route-a-{Guid.NewGuid():N}";
        var queueB = $"test-route-b-{Guid.NewGuid():N}";
        var receivedA = new TaskCompletionSource<MessageEnvelope>();
        var receivedB = new TaskCompletionSource<MessageEnvelope>();

        await _bus.StartConsumingAsync(queueA, envelope =>
        {
            receivedA.SetResult(envelope);
            return Task.CompletedTask;
        });
        await _bus.StartConsumingAsync(queueB, envelope =>
        {
            receivedB.SetResult(envelope);
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(CreateEnvelope("for-a"), queueA);
        await _bus.PublishAsync(CreateEnvelope("for-b"), queueB);

        // Assert
        var resultA = await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var resultB = await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("for-a", ((TestMessage)resultA.Message).Content);
        Assert.Equal("for-b", ((TestMessage)resultB.Message).Content);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _bus.DisposeAsync();
    }
}
