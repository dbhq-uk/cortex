namespace Cortex.Skills;

/// <summary>
/// Executes skills of a specific type. Different executors handle different
/// runtime environments (C#, Python, CLI, API).
/// </summary>
public interface ISkillExecutor
{
    /// <summary>
    /// The executor type this instance handles (e.g. "csharp", "python", "cli", "api").
    /// </summary>
    string ExecutorType { get; }

    /// <summary>
    /// Executes a skill with the given parameters.
    /// </summary>
    Task<object?> ExecuteAsync(SkillDefinition skill, IDictionary<string, object> parameters, CancellationToken cancellationToken = default);
}
