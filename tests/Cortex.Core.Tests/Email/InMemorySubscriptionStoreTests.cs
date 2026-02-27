using Cortex.Core.Email;

namespace Cortex.Core.Tests.Email;

public sealed class InMemorySubscriptionStoreTests
{
    private readonly InMemorySubscriptionStore _store = new();

    private static SubscriptionRecord CreateRecord(
        string subscriptionId = "sub-1",
        string provider = "microsoft",
        string userId = "user-1",
        DateTimeOffset? expiresAt = null,
        string resource = "me/messages") =>
        new()
        {
            SubscriptionId = subscriptionId,
            Provider = provider,
            UserId = userId,
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddHours(2),
            Resource = resource
        };

    [Fact]
    public async Task StoreAsync_ThenGetExpiringAsync_ReturnsRecord()
    {
        var record = CreateRecord(expiresAt: DateTimeOffset.UtcNow.AddHours(2));
        await _store.StoreAsync(record);

        var result = await _store.GetExpiringAsync(TimeSpan.FromHours(6));

        Assert.Single(result);
        Assert.Equal("sub-1", result[0].SubscriptionId);
    }

    [Fact]
    public async Task GetExpiringAsync_NotExpiringSoon_ReturnsEmpty()
    {
        var record = CreateRecord(expiresAt: DateTimeOffset.UtcNow.AddDays(3));
        await _store.StoreAsync(record);

        var result = await _store.GetExpiringAsync(TimeSpan.FromHours(6));

        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateExpiryAsync_UpdatesExpiry()
    {
        var record = CreateRecord(expiresAt: DateTimeOffset.UtcNow.AddHours(2));
        await _store.StoreAsync(record);

        await _store.UpdateExpiryAsync("sub-1", DateTimeOffset.UtcNow.AddDays(3));

        var result = await _store.GetExpiringAsync(TimeSpan.FromHours(6));
        Assert.Empty(result);
    }

    [Fact]
    public async Task RemoveAsync_ThenGetExpiringAsync_ReturnsEmpty()
    {
        var record = CreateRecord(expiresAt: DateTimeOffset.UtcNow.AddHours(2));
        await _store.StoreAsync(record);

        await _store.RemoveAsync("sub-1");

        var result = await _store.GetExpiringAsync(TimeSpan.FromHours(6));
        Assert.Empty(result);
    }

    [Fact]
    public async Task RemoveAsync_Nonexistent_DoesNotThrow()
    {
        await _store.RemoveAsync("nonexistent-sub");
    }
}
