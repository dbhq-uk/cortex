using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// Manages the RabbitMQ connection lifecycle.
/// Wraps a single <see cref="IConnection"/> with auto-recovery support.
/// </summary>
public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly ILogger<RabbitMqConnection> _logger;
    private IConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="RabbitMqConnection"/>.
    /// </summary>
    public RabbitMqConnection(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates the AMQP connection.
    /// </summary>
    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var config = _options.Value;

            var factory = new ConnectionFactory
            {
                HostName = config.HostName,
                Port = config.Port,
                UserName = config.UserName,
                Password = config.Password,
                VirtualHost = config.VirtualHost,
                AutomaticRecoveryEnabled = config.AutoRecoveryEnabled,
                ClientProvidedName = "cortex-message-bus"
            };

            _logger.LogInformation(
                "Connecting to RabbitMQ at {HostName}:{Port}/{VirtualHost}",
                config.HostName, config.Port, config.VirtualHost);

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            _logger.LogInformation("Connected to RabbitMQ");

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Creates a new AMQP channel from the connection.
    /// </summary>
    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            _logger.LogInformation("Closing RabbitMQ connection");
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionLock.Dispose();
    }
}
