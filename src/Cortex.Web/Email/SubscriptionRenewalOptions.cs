namespace Cortex.Web.Email;

/// <summary>
/// Configuration for the subscription renewal background service.
/// </summary>
public sealed record SubscriptionRenewalOptions
{
    /// <summary>How often to check for expiring subscriptions.</summary>
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Renew subscriptions that expire within this window.</summary>
    public TimeSpan RenewalWindow { get; init; } = TimeSpan.FromHours(6);
}
