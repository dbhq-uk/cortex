namespace Cortex.Core.Email;

/// <summary>
/// Configuration for the email provider OAuth app registration.
/// </summary>
public sealed class EmailProviderOptions
{
    /// <summary>The Azure AD tenant identifier.</summary>
    public required string TenantId { get; init; }

    /// <summary>The OAuth application (client) identifier.</summary>
    public required string ClientId { get; init; }

    /// <summary>The OAuth application (client) secret.</summary>
    public required string ClientSecret { get; init; }

    /// <summary>The OAuth redirect URI for the authorization code flow.</summary>
    public required string RedirectUri { get; init; }

    /// <summary>The public URL that Graph API will call for webhook notifications.</summary>
    public string? WebhookNotificationUrl { get; init; }
}
