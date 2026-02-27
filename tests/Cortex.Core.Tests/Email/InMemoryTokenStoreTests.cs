using Cortex.Core.Email;

namespace Cortex.Core.Tests.Email;

public sealed class InMemoryTokenStoreTests
{
    private readonly InMemoryTokenStore _store = new();

    private static TokenSet CreateTokenSet(
        string accessToken = "access-1",
        string refreshToken = "refresh-1",
        DateTimeOffset? expiresAt = null) =>
        new()
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(1)
        };

    [Fact]
    public async Task StoreAsync_ThenGetAsync_ReturnsTokens()
    {
        var tokens = CreateTokenSet();
        await _store.StoreAsync("microsoft", "user-1", tokens);

        var result = await _store.GetAsync("microsoft", "user-1");

        Assert.NotNull(result);
        Assert.Equal("access-1", result.AccessToken);
        Assert.Equal("refresh-1", result.RefreshToken);
    }

    [Fact]
    public async Task GetAsync_Nonexistent_ReturnsNull()
    {
        var result = await _store.GetAsync("microsoft", "user-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task StoreAsync_OverwritesExisting()
    {
        var original = CreateTokenSet(accessToken: "old-access");
        var updated = CreateTokenSet(accessToken: "new-access");

        await _store.StoreAsync("microsoft", "user-1", original);
        await _store.StoreAsync("microsoft", "user-1", updated);

        var result = await _store.GetAsync("microsoft", "user-1");

        Assert.NotNull(result);
        Assert.Equal("new-access", result.AccessToken);
    }

    [Fact]
    public async Task RemoveAsync_ThenGetAsync_ReturnsNull()
    {
        var tokens = CreateTokenSet();
        await _store.StoreAsync("microsoft", "user-1", tokens);

        await _store.RemoveAsync("microsoft", "user-1");

        var result = await _store.GetAsync("microsoft", "user-1");
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_Nonexistent_DoesNotThrow()
    {
        await _store.RemoveAsync("microsoft", "user-1");
    }

    [Fact]
    public async Task DifferentProviders_AreSeparate()
    {
        var msTokens = CreateTokenSet(accessToken: "ms-access");
        var googleTokens = CreateTokenSet(accessToken: "google-access");

        await _store.StoreAsync("microsoft", "user-1", msTokens);
        await _store.StoreAsync("google", "user-1", googleTokens);

        var msResult = await _store.GetAsync("microsoft", "user-1");
        var googleResult = await _store.GetAsync("google", "user-1");

        Assert.NotNull(msResult);
        Assert.NotNull(googleResult);
        Assert.Equal("ms-access", msResult.AccessToken);
        Assert.Equal("google-access", googleResult.AccessToken);
    }
}
