using System.Collections.Concurrent;

namespace Cortex.Core.Authority;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IAuthorityProvider"/>
/// for unit testing and local development.
/// </summary>
public sealed class InMemoryAuthorityProvider : IAuthorityProvider
{
    private readonly ConcurrentDictionary<(string AgentId, string Action), AuthorityClaim> _claims = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new <see cref="InMemoryAuthorityProvider"/> with the specified time provider.
    /// </summary>
    /// <param name="timeProvider">The time provider for checking claim expiry.</param>
    public InMemoryAuthorityProvider(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates a new <see cref="InMemoryAuthorityProvider"/> using the system clock.
    /// </summary>
    public InMemoryAuthorityProvider()
        : this(TimeProvider.System)
    {
    }

    /// <inheritdoc />
    public Task GrantAsync(AuthorityClaim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (claim.PermittedActions is { Count: > 0 })
        {
            foreach (var action in claim.PermittedActions)
            {
                _claims[(claim.GrantedTo, action)] = claim;
            }
        }
        else
        {
            _claims[(claim.GrantedTo, "*")] = claim;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevokeAsync(string agentId, string action, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        _claims.TryRemove((agentId, action), out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AuthorityClaim?> GetClaimAsync(string agentId, string action, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        // Check specific action first, then fall back to wildcard.
        if (_claims.TryGetValue((agentId, action), out var claim))
        {
            if (IsExpired(claim))
            {
                _claims.TryRemove((agentId, action), out _);
            }
            else
            {
                return Task.FromResult<AuthorityClaim?>(claim);
            }
        }

        if (_claims.TryGetValue((agentId, "*"), out var wildcardClaim))
        {
            if (IsExpired(wildcardClaim))
            {
                _claims.TryRemove((agentId, "*"), out _);
                return Task.FromResult<AuthorityClaim?>(null);
            }

            return Task.FromResult<AuthorityClaim?>(wildcardClaim);
        }

        return Task.FromResult<AuthorityClaim?>(null);
    }

    /// <inheritdoc />
    public async Task<bool> HasAuthorityAsync(string agentId, string action, AuthorityTier minimumTier, CancellationToken cancellationToken = default)
    {
        var claim = await GetClaimAsync(agentId, action, cancellationToken);
        return claim is not null && claim.Tier >= minimumTier;
    }

    private bool IsExpired(AuthorityClaim claim) =>
        claim.ExpiresAt.HasValue && claim.ExpiresAt.Value < _timeProvider.GetUtcNow();
}
