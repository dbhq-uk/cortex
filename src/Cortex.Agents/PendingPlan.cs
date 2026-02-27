using Cortex.Agents.Pipeline;
using Cortex.Core.Messages;

namespace Cortex.Agents;

/// <summary>
/// A decomposition plan awaiting human approval before dispatch.
/// </summary>
public sealed record PendingPlan
{
    /// <summary>
    /// The original message envelope that triggered the decomposition.
    /// </summary>
    public required MessageEnvelope OriginalEnvelope { get; init; }

    /// <summary>
    /// The decomposition result containing the proposed tasks.
    /// </summary>
    public required DecompositionResult Decomposition { get; init; }

    /// <summary>
    /// When this plan was stored for approval.
    /// </summary>
    public required DateTimeOffset StoredAt { get; init; }
}
