using System.Collections.Concurrent;
using System.Threading.Channels;
using Cortex.Core.Messages;

namespace Cortex.Messaging;

/// <summary>
/// In-memory message bus for unit testing, local development, and prototyping.
/// Uses System.Threading.Channels for async queue semantics.
/// </summary>
public sealed class InMemoryMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Channel<MessageEnvelope>> _queues = new();
    private readonly ConcurrentBag<ConsumerHandle> _consumers = [];

    /// <inheritdoc />
    public Task PublishAsync(
        MessageEnvelope envelope,
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var channel = _queues.GetOrAdd(queueName, _ =>
            Channel.CreateUnbounded<MessageEnvelope>());

        return channel.Writer.WriteAsync(envelope, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public Task<IAsyncDisposable> StartConsumingAsync(
        string queueName,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(handler);

        var channel = _queues.GetOrAdd(queueName, _ =>
            Channel.CreateUnbounded<MessageEnvelope>());

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var handle = new ConsumerHandle(cts);
        _consumers.Add(handle);

        _ = ConsumeLoopAsync(channel.Reader, handler, cts.Token);

        return Task.FromResult<IAsyncDisposable>(handle);
    }

    /// <inheritdoc />
    public async Task StopConsumingAsync(CancellationToken cancellationToken = default)
    {
        foreach (var handle in _consumers)
        {
            await handle.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public Task<QueueTopology> GetTopologyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new QueueTopology());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopConsumingAsync();

        foreach (var channel in _queues.Values)
        {
            channel.Writer.TryComplete();
        }

        _queues.Clear();
    }

    private static async Task ConsumeLoopAsync(
        ChannelReader<MessageEnvelope> reader,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await handler(envelope);
                }
                catch (Exception)
                {
                    // Individual handler failure should not kill the consumer.
                    // In a production bus, this would nack and dead-letter.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — consumer was stopped.
        }
    }

    private sealed class ConsumerHandle(CancellationTokenSource cts) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                await cts.CancelAsync();
                cts.Dispose();
            }
        }
    }
}
