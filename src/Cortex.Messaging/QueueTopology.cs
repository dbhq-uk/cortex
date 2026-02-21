namespace Cortex.Messaging;

/// <summary>
/// Defines the queue structure that maps to the organisational chart.
/// Manages bindings between queues, agents, and channels.
/// </summary>
public sealed class QueueTopology
{
    private readonly List<QueueBinding> _bindings = [];

    /// <summary>
    /// All configured queue bindings.
    /// </summary>
    public IReadOnlyList<QueueBinding> Bindings => _bindings.AsReadOnly();

    /// <summary>
    /// Adds a binding to the topology.
    /// </summary>
    public void AddBinding(QueueBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _bindings.Add(binding);
    }

    /// <summary>
    /// Removes a binding from the topology.
    /// </summary>
    public bool RemoveBinding(QueueBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return _bindings.Remove(binding);
    }

    /// <summary>
    /// Finds all queue bindings for a specific agent.
    /// </summary>
    public IReadOnlyList<QueueBinding> FindByAgent(string agentId) =>
        _bindings.Where(b => b.AgentId == agentId).ToList().AsReadOnly();

    /// <summary>
    /// Finds all queue bindings for a specific channel.
    /// </summary>
    public IReadOnlyList<QueueBinding> FindByChannel(string channelId) =>
        _bindings.Where(b => b.ChannelId == channelId).ToList().AsReadOnly();
}
