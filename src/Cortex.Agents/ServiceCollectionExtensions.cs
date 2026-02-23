using Cortex.Agents.Delegation;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Agents;

/// <summary>
/// Extension methods for registering the Cortex agent runtime.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Cortex agent runtime to the service collection.
    /// </summary>
    public static IServiceCollection AddCortexAgentRuntime(
        this IServiceCollection services,
        Action<AgentRuntimeBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new AgentRuntimeBuilder(services);
        configure?.Invoke(builder);

        services.AddSingleton<InMemoryAgentRegistry>();
        services.AddSingleton<IAgentRegistry>(sp => sp.GetRequiredService<InMemoryAgentRegistry>());
        services.AddSingleton<InMemoryDelegationTracker>();
        services.AddSingleton<IDelegationTracker>(sp => sp.GetRequiredService<InMemoryDelegationTracker>());
        services.AddSingleton<AgentRuntime>();
        services.AddSingleton<IAgentRuntime>(sp => sp.GetRequiredService<AgentRuntime>());
        services.AddHostedService(sp => sp.GetRequiredService<AgentRuntime>());

        return services;
    }
}
