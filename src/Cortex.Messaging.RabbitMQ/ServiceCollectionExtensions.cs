using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// Extension methods for registering RabbitMQ messaging services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitMQ-backed messaging to the service collection.
    /// Registers <see cref="RabbitMqMessageBus"/> as singleton for
    /// <see cref="IMessageBus"/>, <see cref="IMessagePublisher"/>, and <see cref="IMessageConsumer"/>.
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        Action<RabbitMqOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<RabbitMqMessageBus>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<RabbitMqMessageBus>());
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RabbitMqMessageBus>());
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<RabbitMqMessageBus>());

        return services;
    }
}
