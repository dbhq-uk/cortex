namespace Cortex.Core.Email;

/// <summary>Stores webhook subscription records for tracking and renewal.</summary>
public interface ISubscriptionStore
{
    /// <summary>Stores a subscription record.</summary>
    Task StoreAsync(SubscriptionRecord record, CancellationToken cancellationToken = default);

    /// <summary>Returns subscriptions that expire within the given window.</summary>
    Task<IReadOnlyList<SubscriptionRecord>> GetExpiringAsync(TimeSpan withinWindow, CancellationToken cancellationToken = default);

    /// <summary>Updates the expiry time for a subscription.</summary>
    Task UpdateExpiryAsync(string subscriptionId, DateTimeOffset newExpiry, CancellationToken cancellationToken = default);

    /// <summary>Removes a subscription record.</summary>
    Task RemoveAsync(string subscriptionId, CancellationToken cancellationToken = default);
}
