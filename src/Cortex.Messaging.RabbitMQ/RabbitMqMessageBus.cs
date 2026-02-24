using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Cortex.Core.Messages;

namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of <see cref="IMessageBus"/>.
/// Uses a topic exchange for routing and a dead letter fanout for failures.
/// </summary>
public sealed class RabbitMqMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessageBus> _logger;

    private IChannel? _publishChannel;
    private readonly SemaphoreSlim _publishChannelLock = new(1, 1);
    private readonly ConcurrentBag<ConsumerHandle> _consumers = [];
    private bool _topologyDeclared;
    private bool _disposed;

    /// <summary>
    /// Header name used to carry the assembly-qualified message type.
    /// </summary>
    public const string MessageTypeHeader = "cortex-message-type";

    /// <summary>
    /// Creates a new <see cref="RabbitMqMessageBus"/>.
    /// </summary>
    public RabbitMqMessageBus(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqMessageBus> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        MessageEnvelope envelope,
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = await GetPublishChannelAsync(cancellationToken);
        await EnsureTopologyAsync(channel, cancellationToken);

        // Ensure the queue exists and is bound before publishing
        var routingKey = $"queue.{queueName}";

        await DeclareQueueAsync(channel, queueName, cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);

        var (body, messageType) = MessageSerializer.Serialize(envelope);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                [MessageTypeHeader] = Encoding.UTF8.GetBytes(messageType)
            }
        };

        _logger.LogDebug(
            "Publishing to {Exchange} with routing key {RoutingKey}",
            _options.ExchangeName, routingKey);

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> StartConsumingAsync(
        string queueName,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = await _connection.CreateChannelAsync(cancellationToken);
        await EnsureTopologyAsync(channel, cancellationToken);

        // Declare the queue and bind it
        var routingKey = $"queue.{queueName}";

        await DeclareQueueAsync(channel, queueName, cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);

        // Prefetch 1 message at a time
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var messageTypeName = ea.BasicProperties.Headers is not null
                    && ea.BasicProperties.Headers.TryGetValue(MessageTypeHeader, out var typeObj)
                    && typeObj is byte[] typeBytes
                        ? Encoding.UTF8.GetString(typeBytes)
                        : null;

                if (messageTypeName is null)
                {
                    _logger.LogWarning(
                        "Message missing {Header} header, nacking to dead letter",
                        MessageTypeHeader);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                var body = ea.Body.ToArray(); // Copy — body is only valid in this scope
                var envelope = MessageSerializer.Deserialize(body, messageTypeName);

                if (envelope is null)
                {
                    _logger.LogWarning(
                        "Failed to deserialise message of type {MessageType}, nacking to dead letter",
                        messageTypeName);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                await handler(envelope);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Handler failed for message {DeliveryTag}, nacking to dead letter",
                    ea.DeliveryTag);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        var handle = new ConsumerHandle(channel, consumerTag, _logger);
        _consumers.Add(handle);

        _logger.LogInformation(
            "Started consuming from queue {QueueName} with consumer {ConsumerTag}",
            queueName, consumerTag);

        return handle;
    }

    /// <inheritdoc />
    public async Task StopConsumingAsync(CancellationToken cancellationToken = default)
    {
        foreach (var handle in _consumers)
        {
            await handle.DisposeAsync();
        }

        _logger.LogInformation("All consumers stopped");
    }

    /// <inheritdoc />
    public Task<QueueTopology> GetTopologyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new QueueTopology());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await StopConsumingAsync();

        if (_publishChannel is not null)
        {
            await _publishChannel.CloseAsync();
            _publishChannel.Dispose();
        }

        _publishChannelLock.Dispose();
    }

    private async Task<IChannel> GetPublishChannelAsync(CancellationToken cancellationToken)
    {
        if (_publishChannel is { IsOpen: true })
        {
            return _publishChannel;
        }

        await _publishChannelLock.WaitAsync(cancellationToken);
        try
        {
            if (_publishChannel is { IsOpen: true })
            {
                return _publishChannel;
            }

            _publishChannel = await _connection.CreateChannelAsync(cancellationToken);
            return _publishChannel;
        }
        finally
        {
            _publishChannelLock.Release();
        }
    }

    /// <summary>
    /// Name of the dead letter queue (terminal destination for failed messages).
    /// </summary>
    private const string DeadLetterQueueName = "cortex.deadletter.queue";

    /// <summary>
    /// Declares a queue with standard settings.
    /// Normal queues get a dead-letter-exchange argument; the dead letter queue itself does not.
    /// </summary>
    private async Task DeclareQueueAsync(
        IChannel channel,
        string queueName,
        CancellationToken cancellationToken)
    {
        var isDeadLetterQueue = string.Equals(queueName, DeadLetterQueueName, StringComparison.Ordinal);

        var arguments = isDeadLetterQueue
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName
            };

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        if (_topologyDeclared)
        {
            return;
        }

        // Declare the topic exchange
        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Declare the dead letter exchange
        await channel.ExchangeDeclareAsync(
            exchange: _options.DeadLetterExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Declare the dead letter queue (no dead-letter-exchange — it's the terminal destination)
        await DeclareQueueAsync(channel, DeadLetterQueueName, cancellationToken);

        await channel.QueueBindAsync(
            queue: DeadLetterQueueName,
            exchange: _options.DeadLetterExchangeName,
            routingKey: "",
            cancellationToken: cancellationToken);

        _topologyDeclared = true;

        _logger.LogInformation(
            "Declared RabbitMQ topology: exchanges {Exchange}, {DeadLetterExchange}",
            _options.ExchangeName, _options.DeadLetterExchangeName);
    }

    /// <summary>
    /// Handle for an individual consumer that can be disposed independently.
    /// </summary>
    private sealed class ConsumerHandle(
        IChannel channel,
        string consumerTag,
        ILogger logger) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                try
                {
                    await channel.BasicCancelAsync(consumerTag);
                    await channel.CloseAsync();
                    channel.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error stopping consumer {ConsumerTag}", consumerTag);
                }
            }
        }
    }
}
