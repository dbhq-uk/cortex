namespace Cortex.Skills;

/// <summary>
/// A skill that can be executed with parameters.
/// </summary>
public interface ISkill
{
    /// <summary>
    /// The definition describing this skill's metadata and configuration.
    /// </summary>
    SkillDefinition Definition { get; }

    /// <summary>
    /// Executes this skill with the given parameters.
    /// </summary>
    Task<object?> ExecuteAsync(IDictionary<string, object> parameters, CancellationToken cancellationToken = default);
}
