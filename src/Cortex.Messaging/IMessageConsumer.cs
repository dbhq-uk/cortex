using Cortex.Core.Messages;

namespace Cortex.Messaging;

/// <summary>
/// Consumes messages from named queues.
/// </summary>
public interface IMessageConsumer
{
    /// <summary>
    /// Starts consuming messages from the specified queue.
    /// Returns a handle that can be disposed to stop only this consumer.
    /// </summary>
    Task<IAsyncDisposable> StartConsumingAsync(
        string queueName,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all consumers managed by this instance.
    /// </summary>
    Task StopConsumingAsync(CancellationToken cancellationToken = default);
}
