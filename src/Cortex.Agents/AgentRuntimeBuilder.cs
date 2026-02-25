using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Registers a <see cref="SkillDrivenAgent"/> from a <see cref="PersonaDefinition"/>.
    /// </summary>
    public AgentRuntimeBuilder AddPersona(PersonaDefinition persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        _services.AddSingleton<IAgent>(sp =>
        {
            var pipelineRunner = new SkillPipelineRunner(
                sp.GetRequiredService<ISkillRegistry>(),
                sp.GetServices<ISkillExecutor>(),
                sp.GetRequiredService<ILogger<SkillPipelineRunner>>());

            return new SkillDrivenAgent(
                persona,
                pipelineRunner,
                sp.GetRequiredService<IAgentRegistry>(),
                sp.GetRequiredService<IDelegationTracker>(),
                sp.GetRequiredService<IReferenceCodeGenerator>(),
                sp.GetRequiredService<IMessagePublisher>(),
                sp.GetRequiredService<ILogger<SkillDrivenAgent>>());
        });

        return this;
    }

    /// <summary>
    /// Registers a <see cref="SkillDrivenAgent"/> from a persona markdown file.
    /// </summary>
    public AgentRuntimeBuilder AddPersonaFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var persona = PersonaParser.ParseFileAsync(filePath).GetAwaiter().GetResult();
        return AddPersona(persona);
    }
}
