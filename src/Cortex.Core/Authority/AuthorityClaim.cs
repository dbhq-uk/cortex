namespace Cortex.Core.Authority;

/// <summary>
/// Represents an authority grant that flows with messages through the system.
/// Authority claims define who authorised an action, at what level, and what actions are permitted.
/// </summary>
public sealed record AuthorityClaim
{
    /// <summary>
    /// The agent or user who granted this authority.
    /// </summary>
    public required string GrantedBy { get; init; }

    /// <summary>
    /// The agent or user this authority is granted to.
    /// </summary>
    public required string GrantedTo { get; init; }

    /// <summary>
    /// The tier of authority granted.
    /// </summary>
    public required AuthorityTier Tier { get; init; }

    /// <summary>
    /// Specific actions this claim authorises.
    /// </summary>
    public IReadOnlyList<string> PermittedActions { get; init; } = [];

    /// <summary>
    /// When this authority was granted.
    /// </summary>
    public required DateTimeOffset GrantedAt { get; init; }

    /// <summary>
    /// When this authority expires, if ever.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
