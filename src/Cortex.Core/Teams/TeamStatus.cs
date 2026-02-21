namespace Cortex.Core.Teams;

/// <summary>
/// Lifecycle status of a team assembled around a goal.
/// </summary>
public enum TeamStatus
{
    /// <summary>
    /// Team is being assembled — agents being identified and pulled in.
    /// </summary>
    Assembling = 0,

    /// <summary>
    /// Team is active and working on the goal.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Team is winding down — delivering results, cleaning up.
    /// </summary>
    Dissolving = 2,

    /// <summary>
    /// Team has completed its goal and been dissolved.
    /// </summary>
    Complete = 3
}
