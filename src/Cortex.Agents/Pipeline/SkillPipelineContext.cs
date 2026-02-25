using Cortex.Core.Messages;

namespace Cortex.Agents.Pipeline;

/// <summary>
/// Accumulates context as a skill pipeline executes.
/// Each skill receives the full context including results from all prior skills.
/// </summary>
public sealed class SkillPipelineContext
{
    /// <summary>
    /// The original incoming message envelope.
    /// </summary>
    public required MessageEnvelope Envelope { get; init; }

    /// <summary>
    /// Additional parameters available to all skills in the pipeline.
    /// Populated by the agent before pipeline execution.
    /// </summary>
    public IDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Results from each skill, keyed by skill ID.
    /// </summary>
    public Dictionary<string, object?> Results { get; } = new();
}
