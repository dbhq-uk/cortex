using Cortex.Core.Authority;
using Microsoft.Extensions.Time.Testing;

namespace Cortex.Core.Tests.Authority;

public sealed class InMemoryAuthorityProviderTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly InMemoryAuthorityProvider _provider;

    public InMemoryAuthorityProviderTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 2, 27, 12, 0, 0, TimeSpan.Zero));
        _provider = new InMemoryAuthorityProvider(_timeProvider);
    }

    private static AuthorityClaim CreateClaim(
        string grantedBy = "admin",
        string grantedTo = "agent-1",
        AuthorityTier tier = AuthorityTier.JustDoIt,
        IReadOnlyList<string>? permittedActions = null,
        DateTimeOffset? grantedAt = null,
        DateTimeOffset? expiresAt = null) =>
        new()
        {
            GrantedBy = grantedBy,
            GrantedTo = grantedTo,
            Tier = tier,
            PermittedActions = permittedActions ?? [],
            GrantedAt = grantedAt ?? DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

    // --- Task 2: Core grant and get tests ---

    [Fact]
    public async Task GrantAsync_ThenGetClaim_ReturnsClaim()
    {
        var claim = CreateClaim(permittedActions: ["send-email"]);
        await _provider.GrantAsync(claim);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.NotNull(result);
        Assert.Equal("agent-1", result.GrantedTo);
        Assert.Equal(AuthorityTier.JustDoIt, result.Tier);
    }

    [Fact]
    public async Task GrantAsync_NullClaim_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _provider.GrantAsync(null!));
    }

    [Fact]
    public async Task GetClaimAsync_NoMatchingClaim_ReturnsNull()
    {
        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.Null(result);
    }

    [Fact]
    public async Task GrantAsync_NoPermittedActions_StoresWithWildcard()
    {
        var claim = CreateClaim(permittedActions: []);
        await _provider.GrantAsync(claim);

        var result = await _provider.GetClaimAsync("agent-1", "any-action");

        Assert.NotNull(result);
        Assert.Equal("agent-1", result.GrantedTo);
    }

    [Fact]
    public async Task GrantAsync_MultipleActions_StoresEach()
    {
        var claim = CreateClaim(permittedActions: ["send-email", "create-file"]);
        await _provider.GrantAsync(claim);

        var emailClaim = await _provider.GetClaimAsync("agent-1", "send-email");
        var fileClaim = await _provider.GetClaimAsync("agent-1", "create-file");

        Assert.NotNull(emailClaim);
        Assert.NotNull(fileClaim);
    }

    [Fact]
    public async Task GetClaimAsync_SpecificActionTakesPrecedenceOverWildcard()
    {
        var wildcardClaim = CreateClaim(tier: AuthorityTier.JustDoIt, permittedActions: []);
        var specificClaim = CreateClaim(tier: AuthorityTier.AskMeFirst, permittedActions: ["send-email"]);

        await _provider.GrantAsync(wildcardClaim);
        await _provider.GrantAsync(specificClaim);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.NotNull(result);
        Assert.Equal(AuthorityTier.AskMeFirst, result.Tier);
    }

    [Fact]
    public async Task GetClaimAsync_FallsBackToWildcard_WhenNoSpecificAction()
    {
        var wildcardClaim = CreateClaim(tier: AuthorityTier.DoItAndShowMe, permittedActions: []);
        await _provider.GrantAsync(wildcardClaim);

        var result = await _provider.GetClaimAsync("agent-1", "unknown-action");

        Assert.NotNull(result);
        Assert.Equal(AuthorityTier.DoItAndShowMe, result.Tier);
    }

    // --- Task 2: HasAuthorityAsync tests ---

    [Fact]
    public async Task HasAuthorityAsync_SufficientTier_ReturnsTrue()
    {
        var claim = CreateClaim(tier: AuthorityTier.AskMeFirst, permittedActions: ["send-email"]);
        await _provider.GrantAsync(claim);

        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.DoItAndShowMe);

        Assert.True(result);
    }

    [Fact]
    public async Task HasAuthorityAsync_InsufficientTier_ReturnsFalse()
    {
        var claim = CreateClaim(tier: AuthorityTier.JustDoIt, permittedActions: ["send-email"]);
        await _provider.GrantAsync(claim);

        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.AskMeFirst);

        Assert.False(result);
    }

    [Fact]
    public async Task HasAuthorityAsync_NoClaim_ReturnsFalse()
    {
        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.JustDoIt);

        Assert.False(result);
    }

    // --- Task 3: Expiry tests ---

    [Fact]
    public async Task GetClaimAsync_ExpiredClaim_ReturnsNull()
    {
        var claim = CreateClaim(
            permittedActions: ["send-email"],
            expiresAt: _timeProvider.GetUtcNow().AddHours(-1));
        await _provider.GrantAsync(claim);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetClaimAsync_NotYetExpiredClaim_ReturnsClaim()
    {
        var claim = CreateClaim(
            permittedActions: ["send-email"],
            expiresAt: _timeProvider.GetUtcNow().AddHours(1));
        await _provider.GrantAsync(claim);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetClaimAsync_NoExpiresAt_ReturnsClaim()
    {
        var claim = CreateClaim(permittedActions: ["send-email"], expiresAt: null);
        await _provider.GrantAsync(claim);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task HasAuthorityAsync_ExpiredClaim_ReturnsFalse()
    {
        var claim = CreateClaim(
            tier: AuthorityTier.AskMeFirst,
            permittedActions: ["send-email"],
            expiresAt: _timeProvider.GetUtcNow().AddHours(-1));
        await _provider.GrantAsync(claim);

        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.JustDoIt);

        Assert.False(result);
    }

    // --- Task 3: Revoke tests ---

    [Fact]
    public async Task RevokeAsync_RemovesClaim()
    {
        var claim = CreateClaim(permittedActions: ["send-email"]);
        await _provider.GrantAsync(claim);

        await _provider.RevokeAsync("agent-1", "send-email");

        var result = await _provider.GetClaimAsync("agent-1", "send-email");
        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAsync_NonexistentClaim_DoesNotThrow()
    {
        await _provider.RevokeAsync("agent-1", "send-email");
    }

    [Fact]
    public async Task GrantAsync_OverwritesPreviousClaim()
    {
        var original = CreateClaim(tier: AuthorityTier.JustDoIt, permittedActions: ["send-email"]);
        var updated = CreateClaim(tier: AuthorityTier.AskMeFirst, permittedActions: ["send-email"]);

        await _provider.GrantAsync(original);
        await _provider.GrantAsync(updated);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.NotNull(result);
        Assert.Equal(AuthorityTier.AskMeFirst, result.Tier);
    }

    [Fact]
    public async Task RevokeAsync_WildcardClaim_RemovesWildcard()
    {
        var claim = CreateClaim(permittedActions: []);
        await _provider.GrantAsync(claim);

        await _provider.RevokeAsync("agent-1", "*");

        var result = await _provider.GetClaimAsync("agent-1", "any-action");
        Assert.Null(result);
    }
}
