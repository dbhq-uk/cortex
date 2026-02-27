namespace Cortex.Core.Email;

/// <summary>Stores OAuth tokens keyed by provider and user.</summary>
public interface ITokenStore
{
    /// <summary>Stores or overwrites tokens for the given provider and user.</summary>
    Task StoreAsync(string provider, string userId, TokenSet tokens, CancellationToken cancellationToken = default);

    /// <summary>Retrieves tokens for the given provider and user, or null if not found.</summary>
    Task<TokenSet?> GetAsync(string provider, string userId, CancellationToken cancellationToken = default);

    /// <summary>Removes tokens for the given provider and user.</summary>
    Task RemoveAsync(string provider, string userId, CancellationToken cancellationToken = default);
}
