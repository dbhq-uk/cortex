using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Agents;

/// <summary>
/// Builder for configuring agents that start with the runtime.
/// </summary>
public sealed class AgentRuntimeBuilder
{
    private readonly IServiceCollection _services;

    internal AgentRuntimeBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Registers an agent type to be started when the runtime starts.
    /// </summary>
    public AgentRuntimeBuilder AddAgent<TAgent>() where TAgent : class, IAgent
    {
        _services.AddSingleton<IAgent, TAgent>();
        return this;
    }
}
