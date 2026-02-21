using Cortex.Core.Messages;

namespace Cortex.Messaging;

/// <summary>
/// Publishes messages to named queues.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message envelope to the specified queue.
    /// </summary>
    Task PublishAsync(MessageEnvelope envelope, string queueName, CancellationToken cancellationToken = default);
}
