using System.Collections.Concurrent;

namespace Cortex.Core.Email;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ITokenStore"/>
/// for unit testing and local development.
/// </summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<(string Provider, string UserId), TokenSet> _tokens = new();

    /// <inheritdoc />
    public Task StoreAsync(string provider, string userId, TokenSet tokens, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(tokens);

        _tokens[(provider, userId)] = tokens;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TokenSet?> GetAsync(string provider, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _tokens.TryGetValue((provider, userId), out var tokens);
        return Task.FromResult(tokens);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string provider, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _tokens.TryRemove((provider, userId), out _);
        return Task.CompletedTask;
    }
}
