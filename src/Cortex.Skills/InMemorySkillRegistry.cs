using System.Collections.Concurrent;

namespace Cortex.Skills;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ISkillRegistry"/>.
/// </summary>
public sealed class InMemorySkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, SkillDefinition> _skills = new();

    /// <inheritdoc />
    public Task RegisterAsync(SkillDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _skills[definition.SkillId] = definition;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SkillDefinition?> FindByIdAsync(string skillId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        _skills.TryGetValue(skillId, out var definition);
        return Task.FromResult(definition);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SkillDefinition>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var matches = _skills.Values
            .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<SkillDefinition>>(matches);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SkillDefinition>> FindByCategoryAsync(SkillCategory category, CancellationToken cancellationToken = default)
    {
        var matches = _skills.Values
            .Where(s => s.Category == category)
            .ToList();

        return Task.FromResult<IReadOnlyList<SkillDefinition>>(matches);
    }
}
