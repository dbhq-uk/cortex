namespace Cortex.Skills;

/// <summary>
/// Registry for discovering and retrieving skill definitions.
/// Agents search this registry to find what they need.
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// Registers a skill definition.
    /// </summary>
    Task RegisterAsync(SkillDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a skill by its unique ID.
    /// </summary>
    Task<SkillDefinition?> FindByIdAsync(string skillId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for skills matching a query string.
    /// </summary>
    Task<IReadOnlyList<SkillDefinition>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all skills in a specific category.
    /// </summary>
    Task<IReadOnlyList<SkillDefinition>> FindByCategoryAsync(SkillCategory category, CancellationToken cancellationToken = default);
}
