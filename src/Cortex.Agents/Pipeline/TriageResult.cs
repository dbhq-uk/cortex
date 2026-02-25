using Cortex.Core.Authority;

namespace Cortex.Agents.Pipeline;

/// <summary>
/// Result of an LLM triage skill — the routing recommendation for a message.
/// </summary>
public sealed record TriageResult
{
    /// <summary>
    /// The capability name that should handle this message.
    /// </summary>
    public required string Capability { get; init; }

    /// <summary>
    /// The recommended authority tier for the delegated work.
    /// </summary>
    public required AuthorityTier AuthorityTier { get; init; }

    /// <summary>
    /// A brief summary of what needs to be done.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) in the triage decision.
    /// </summary>
    public required double Confidence { get; init; }
}
