using Cortex.Core.References;

namespace Cortex.Core.Teams;

/// <summary>
/// Represents a team assembled around a specific goal.
/// Teams are ephemeral — created when a goal arrives, dissolved when complete.
/// </summary>
public interface ITeam
{
    /// <summary>
    /// Unique identifier for this team.
    /// </summary>
    string TeamId { get; }

    /// <summary>
    /// Reference code tracking this team's goal.
    /// </summary>
    ReferenceCode ReferenceCode { get; }

    /// <summary>
    /// Current lifecycle status.
    /// </summary>
    TeamStatus Status { get; }

    /// <summary>
    /// IDs of agents (human or AI) that are members of this team.
    /// </summary>
    IReadOnlyList<string> MemberIds { get; }

    /// <summary>
    /// When this team was created.
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When this team completed its goal, if it has.
    /// </summary>
    DateTimeOffset? CompletedAt { get; }
}
