namespace Cortex.Agents.Pipeline;

/// <summary>
/// A single routable sub-task within a decomposition.
/// </summary>
public sealed record DecompositionTask
{
    /// <summary>
    /// The capability name that should handle this sub-task.
    /// </summary>
    public required string Capability { get; init; }

    /// <summary>
    /// Description of what this sub-task should accomplish.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The authority tier for this sub-task as a string
    /// ("JustDoIt", "DoItAndShowMe", "AskMeFirst").
    /// </summary>
    public required string AuthorityTier { get; init; }
}
