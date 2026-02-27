using Cortex.Core.Messages;

namespace Cortex.Core.Email;

/// <summary>
/// Abstraction for email provider integration. Decouples email operations from a specific provider.
/// </summary>
public interface IEmailProvider
{
    /// <summary>Validates and parses an inbound webhook payload into normalised email messages.</summary>
    Task<IReadOnlyList<EmailMessage>> ProcessWebhookAsync(string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default);

    /// <summary>Validates a subscription verification request. Returns the validation token if valid, null otherwise.</summary>
    string? HandleValidation(string payload, IDictionary<string, string> headers);

    /// <summary>Sends an outbound email via the provider.</summary>
    Task SendAsync(OutboundEmail email, string userId, CancellationToken cancellationToken = default);

    /// <summary>Fetches attachment content by message and attachment identifiers.</summary>
    Task<Stream> GetAttachmentAsync(string externalMessageId, string contentId, CancellationToken cancellationToken = default);

    /// <summary>Creates a webhook subscription for the given user's mailbox.</summary>
    Task<SubscriptionRecord> CreateSubscriptionAsync(string userId, string notificationUrl, CancellationToken cancellationToken = default);

    /// <summary>Renews an existing webhook subscription before it expires.</summary>
    Task<SubscriptionRecord> RenewSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a webhook subscription.</summary>
    Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
}
