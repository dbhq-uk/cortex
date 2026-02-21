using Cortex.Core.Messages;

namespace Cortex.Messaging;

/// <summary>
/// Consumes messages from a named queue.
/// </summary>
public interface IMessageConsumer
{
    /// <summary>
    /// Starts consuming messages from the specified queue.
    /// </summary>
    Task StartConsumingAsync(string queueName, Func<MessageEnvelope, Task> handler, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops consuming messages.
    /// </summary>
    Task StopConsumingAsync(CancellationToken cancellationToken = default);
}
