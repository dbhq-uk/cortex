using Cortex.Core.Messages;

namespace Cortex.Agents.Tests;

/// <summary>
/// Test agent that echoes back the received message content in a response envelope.
/// </summary>
public sealed class EchoAgent : IAgent
{
    /// <inheritdoc />
    public string AgentId { get; } = "echo-agent";

    /// <inheritdoc />
    public string Name { get; } = "Echo Agent";

    /// <inheritdoc />
    public IReadOnlyList<AgentCapability> Capabilities { get; } =
    [
        new AgentCapability { Name = "echo", Description = "Echoes messages back" }
    ];

    /// <inheritdoc />
    public Task<MessageEnvelope?> ProcessAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var incoming = (TestMessage)envelope.Message;

        var response = envelope with
        {
            Message = new TestMessage { Content = $"echo: {incoming.Content}" }
        };

        return Task.FromResult<MessageEnvelope?>(response);
    }
}
