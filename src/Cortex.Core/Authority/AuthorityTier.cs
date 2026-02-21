namespace Cortex.Core.Authority;

/// <summary>
/// Three tiers of action authority in the delegation model.
/// </summary>
public enum AuthorityTier
{
    /// <summary>
    /// Internal actions with no external footprint. Execute without approval.
    /// </summary>
    JustDoIt = 0,

    /// <summary>
    /// Prepare and present for approval before external action.
    /// </summary>
    DoItAndShowMe = 1,

    /// <summary>
    /// Novel, high-stakes, or uncertain actions. Requires explicit approval.
    /// </summary>
    AskMeFirst = 2
}
