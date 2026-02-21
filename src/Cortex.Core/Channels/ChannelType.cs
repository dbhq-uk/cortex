namespace Cortex.Core.Channels;

/// <summary>
/// Types of communication channels in the Cortex system.
/// </summary>
public enum ChannelType
{
    /// <summary>
    /// Catch-all channel where the CoS determines context.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Named channel for a specific project, domain, or concern.
    /// </summary>
    Named = 1,

    /// <summary>
    /// Direct line to a specialist agent, bypassing the CoS.
    /// </summary>
    Direct = 2,

    /// <summary>
    /// Ephemeral channel for a team assembled around a goal.
    /// </summary>
    Team = 3
}
