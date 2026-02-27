using Cortex.Core.Email;
using Cortex.Web.Email;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cortex.Web.Tests.Email;

public class SubscriptionRenewalServiceTests
{
    private readonly IEmailProvider _emailProvider = Substitute.For<IEmailProvider>();
    private readonly InMemorySubscriptionStore _subscriptionStore = new();
    private readonly SubscriptionRenewalOptions _options = new()
    {
        CheckInterval = TimeSpan.FromMinutes(30),
        RenewalWindow = TimeSpan.FromHours(6)
    };

    private SubscriptionRenewalService CreateService() =>
        new(_emailProvider, _subscriptionStore, NullLogger<SubscriptionRenewalService>.Instance, _options);

    [Fact]
    public async Task CheckAndRenewAsync_ExpiringSubscription_RenewsIt()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(2);
        var record = new SubscriptionRecord
        {
            SubscriptionId = "sub-001",
            Provider = "microsoft",
            UserId = "user-1",
            ExpiresAt = expiresAt,
            Resource = "me/messages"
        };
        await _subscriptionStore.StoreAsync(record);

        var newExpiry = DateTimeOffset.UtcNow.AddDays(2);
        _emailProvider.RenewSubscriptionAsync("sub-001", Arg.Any<CancellationToken>())
            .Returns(record with { ExpiresAt = newExpiry });

        var service = CreateService();
        await service.CheckAndRenewAsync();

        await _emailProvider.Received(1).RenewSubscriptionAsync("sub-001", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndRenewAsync_NoExpiringSubscriptions_DoesNothing()
    {
        var record = new SubscriptionRecord
        {
            SubscriptionId = "sub-001",
            Provider = "microsoft",
            UserId = "user-1",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            Resource = "me/messages"
        };
        await _subscriptionStore.StoreAsync(record);

        var service = CreateService();
        await service.CheckAndRenewAsync();

        await _emailProvider.DidNotReceive().RenewSubscriptionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndRenewAsync_RenewalFails_LogsAndContinues()
    {
        var record1 = new SubscriptionRecord
        {
            SubscriptionId = "sub-001",
            Provider = "microsoft",
            UserId = "user-1",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            Resource = "me/messages"
        };
        var record2 = new SubscriptionRecord
        {
            SubscriptionId = "sub-002",
            Provider = "microsoft",
            UserId = "user-2",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            Resource = "me/messages"
        };
        await _subscriptionStore.StoreAsync(record1);
        await _subscriptionStore.StoreAsync(record2);

        _emailProvider.RenewSubscriptionAsync("sub-001", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Graph API error"));
        _emailProvider.RenewSubscriptionAsync("sub-002", Arg.Any<CancellationToken>())
            .Returns(record2 with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(2) });

        var service = CreateService();
        await service.CheckAndRenewAsync();

        await _emailProvider.Received(1).RenewSubscriptionAsync("sub-001", Arg.Any<CancellationToken>());
        await _emailProvider.Received(1).RenewSubscriptionAsync("sub-002", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAndRenewAsync_UpdatesStoreAfterRenewal()
    {
        var record = new SubscriptionRecord
        {
            SubscriptionId = "sub-001",
            Provider = "microsoft",
            UserId = "user-1",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            Resource = "me/messages"
        };
        await _subscriptionStore.StoreAsync(record);

        var newExpiry = DateTimeOffset.UtcNow.AddDays(2);
        _emailProvider.RenewSubscriptionAsync("sub-001", Arg.Any<CancellationToken>())
            .Returns(record with { ExpiresAt = newExpiry });

        var service = CreateService();
        await service.CheckAndRenewAsync();

        var expiring = await _subscriptionStore.GetExpiringAsync(TimeSpan.FromHours(6));
        Assert.Empty(expiring);
    }
}
