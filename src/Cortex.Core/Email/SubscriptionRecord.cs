namespace Cortex.Core.Email;

/// <summary>Tracks a webhook subscription with a provider.</summary>
public sealed record SubscriptionRecord
{
    /// <summary>The provider-specific subscription identifier.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>The provider name (e.g. "microsoft").</summary>
    public required string Provider { get; init; }

    /// <summary>The user who connected their account.</summary>
    public required string UserId { get; init; }

    /// <summary>When the subscription expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>The resource being subscribed to (e.g. "me/messages").</summary>
    public required string Resource { get; init; }
}
