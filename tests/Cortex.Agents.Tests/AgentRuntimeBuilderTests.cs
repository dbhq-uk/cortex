using Cortex.Agents.Personas;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Agents.Tests;

public sealed class AgentRuntimeBuilderTests
{
    [Fact]
    public void AddPersona_FromDefinition_RegistersSkillDrivenAgent()
    {
        var services = new ServiceCollection();
        var builder = new AgentRuntimeBuilder(services);

        var persona = new PersonaDefinition
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = "triage", Description = "Triage" }],
            Pipeline = ["cos-triage"],
            EscalationTarget = "agent.founder"
        };

        builder.AddPersona(persona);

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IAgent)
                && d.Lifetime == ServiceLifetime.Singleton);

        Assert.NotNull(descriptor);
    }
}
