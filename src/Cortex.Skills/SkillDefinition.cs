namespace Cortex.Skills;

/// <summary>
/// Defines a skill parsed from a markdown file. Skills are the universal unit of capability
/// in Cortex — they can wrap C# functions, Python scripts, CLI invocations, or API calls.
/// </summary>
public sealed record SkillDefinition
{
    /// <summary>
    /// Unique identifier for this skill.
    /// </summary>
    public required string SkillId { get; init; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Description of what this skill does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Category this skill belongs to.
    /// </summary>
    public required SkillCategory Category { get; init; }

    /// <summary>
    /// Trigger phrases or patterns that activate this skill.
    /// </summary>
    public IReadOnlyList<string> Triggers { get; init; } = [];

    /// <summary>
    /// The type of executor needed: "csharp", "python", "cli", or "api".
    /// </summary>
    public required string ExecutorType { get; init; }

    /// <summary>
    /// Path to the skill definition file.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Version of this skill definition.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Raw content of the skill definition file, loaded at registration time.
    /// Used by executors to extract prompts and configuration.
    /// </summary>
    public string? Content { get; init; }
}
