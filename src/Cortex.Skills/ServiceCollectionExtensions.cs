using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Skills;

/// <summary>
/// Extension methods for registering Cortex skill services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Cortex skill infrastructure to the service collection.
    /// </summary>
    public static IServiceCollection AddCortexSkills(
        this IServiceCollection services,
        Action<ClaudeCliOptions>? configureCli = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InMemorySkillRegistry>();
        services.AddSingleton<ISkillRegistry>(sp => sp.GetRequiredService<InMemorySkillRegistry>());
        services.AddSingleton<ISkillExecutor, LlmSkillExecutor>();

        if (configureCli is not null)
        {
            services.Configure(configureCli);
        }

        services.AddSingleton<ILlmClient, ClaudeCliClient>();

        return services;
    }
}
