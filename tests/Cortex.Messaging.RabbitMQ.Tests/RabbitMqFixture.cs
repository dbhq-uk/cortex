using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Messaging.RabbitMQ.Tests;

/// <summary>
/// Shared fixture that provides a RabbitMqConnection for integration tests.
/// Requires a running RabbitMQ instance at localhost:5672 (cortex/cortex).
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    /// <summary>
    /// The shared connection to RabbitMQ.
    /// </summary>
    public RabbitMqConnection Connection { get; private set; } = null!;

    /// <summary>
    /// Configuration used to connect.
    /// </summary>
    public RabbitMqOptions Options { get; } = new()
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "cortex",
        Password = "cortex",
        VirtualHost = "/"
    };

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Connection = new RabbitMqConnection(
            Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<RabbitMqConnection>.Instance);

        // Verify connectivity
        var conn = await Connection.GetConnectionAsync();
        Assert.True(conn.IsOpen, "RabbitMQ connection should be open");
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}
