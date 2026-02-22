namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// Configuration options for the RabbitMQ message bus connection.
/// Bound to <c>IOptions&lt;RabbitMqOptions&gt;</c> via appsettings.json.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// RabbitMQ server hostname.
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ AMQP port.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Authentication username.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Authentication password.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// RabbitMQ virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Name of the topic exchange for message routing.
    /// </summary>
    public string ExchangeName { get; set; } = "cortex.messages";

    /// <summary>
    /// Name of the dead letter exchange.
    /// </summary>
    public string DeadLetterExchangeName { get; set; } = "cortex.deadletter";

    /// <summary>
    /// Whether automatic connection recovery is enabled.
    /// </summary>
    public bool AutoRecoveryEnabled { get; set; } = true;
}
