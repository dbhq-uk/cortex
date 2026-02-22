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
    private readonly List<CancellationTokenSource> _consumers = [];
    private readonly object _consumersLock = new();

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
    public Task StartConsumingAsync(
        string queueName,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(handler);

        var channel = _queues.GetOrAdd(queueName, _ =>
            Channel.CreateUnbounded<MessageEnvelope>());

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_consumersLock)
        {
            _consumers.Add(cts);
        }

        _ = ConsumeLoopAsync(channel.Reader, handler, cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopConsumingAsync(CancellationToken cancellationToken = default)
    {
        lock (_consumersLock)
        {
            foreach (var cts in _consumers)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _consumers.Clear();
        }

        return Task.CompletedTask;
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
                await handler(envelope);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — consumer was stopped.
        }
    }
}
