namespace Cortex.Messaging;

/// <summary>
/// Combined message bus providing publish, consume, and topology management.
/// </summary>
public interface IMessageBus : IMessagePublisher, IMessageConsumer
{
    /// <summary>
    /// Gets the current queue topology configuration.
    /// </summary>
    Task<QueueTopology> GetTopologyAsync(CancellationToken cancellationToken = default);
}
