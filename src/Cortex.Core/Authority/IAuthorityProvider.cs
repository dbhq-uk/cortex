namespace Cortex.Core.Authority;

/// <summary>
/// Resolves and validates authority claims for agents and actions.
/// </summary>
public interface IAuthorityProvider
{
    /// <summary>
    /// Gets the authority claim for a specific agent and action, if one exists.
    /// </summary>
    Task<AuthorityClaim?> GetClaimAsync(string agentId, string action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an agent has sufficient authority for an action at the specified minimum tier.
    /// </summary>
    Task<bool> HasAuthorityAsync(string agentId, string action, AuthorityTier minimumTier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants an authority claim.
    /// </summary>
    Task GrantAsync(AuthorityClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the authority claim for a specific agent and action.
    /// </summary>
    Task RevokeAsync(string agentId, string action, CancellationToken cancellationToken = default);
}
