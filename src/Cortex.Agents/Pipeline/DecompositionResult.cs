namespace Cortex.Agents.Pipeline;

/// <summary>
/// Result of the cos-decompose skill — either a single routing decision
/// (backward compatible with <see cref="TriageResult"/>) or a multi-task decomposition.
/// When <see cref="Tasks"/> has exactly one entry, it is equivalent to 1:1 routing.
/// Multiple entries trigger the workflow path.
/// </summary>
public sealed record DecompositionResult
{
    /// <summary>
    /// The decomposed tasks. One entry = single routing. Multiple = workflow.
    /// </summary>
    public required IReadOnlyList<DecompositionTask> Tasks { get; init; }

    /// <summary>
    /// Human-readable summary of the overall goal.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) in the decomposition decision.
    /// </summary>
    public required double Confidence { get; init; }
}
