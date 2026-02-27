using System.Text.Json;
using Azure.Core;
using Cortex.Core.Email;
using Cortex.Core.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Graph.Models;

namespace Cortex.Web.Email;

/// <summary>
/// Email provider implementation using Microsoft Graph API for sending, receiving,
/// and managing email subscriptions via OAuth2 delegated permissions.
/// </summary>
public sealed class MicrosoftGraphEmailProvider : IEmailProvider
{
    private const string ProviderName = "microsoft";
    private const string SubscriptionResource = "me/messages";
    private static readonly TimeSpan SubscriptionLifetime = TimeSpan.FromDays(2);
    private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(5);

    private readonly EmailProviderOptions _options;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<MicrosoftGraphEmailProvider> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="MicrosoftGraphEmailProvider"/>.
    /// </summary>
    /// <param name="options">OAuth application configuration.</param>
    /// <param name="tokenStore">Store for persisting OAuth tokens.</param>
    /// <param name="logger">Logger instance.</param>
    public MicrosoftGraphEmailProvider(
        IOptions<EmailProviderOptions> options,
        ITokenStore tokenStore,
        ILogger<MicrosoftGraphEmailProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokenStore);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public string? HandleValidation(string payload, IDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(headers);

        using var document = JsonDocument.Parse(payload);

        if (document.RootElement.TryGetProperty("validationToken", out var tokenElement))
        {
            var token = tokenElement.GetString();
            _logger.LogDebug("Handled Graph subscription validation, token: {Token}", token);
            return token;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailMessage>> ProcessWebhookAsync(
        string payload,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(headers);

        using var document = JsonDocument.Parse(payload);

        if (!document.RootElement.TryGetProperty("value", out var valueArray))
        {
            _logger.LogWarning("Webhook payload missing 'value' array");
            return [];
        }

        var messages = new List<EmailMessage>();

        foreach (var notification in valueArray.EnumerateArray())
        {
            if (!notification.TryGetProperty("resourceData", out var resourceData))
            {
                continue;
            }

            if (!resourceData.TryGetProperty("id", out var idElement))
            {
                continue;
            }

            var messageId = idElement.GetString();
            if (string.IsNullOrEmpty(messageId))
            {
                continue;
            }

            // Extract the user ID from the notification for token lookup
            var userId = "me";
            if (notification.TryGetProperty("userId", out var userIdElement))
            {
                userId = userIdElement.GetString() ?? "me";
            }

            try
            {
                var client = await GetGraphClientAsync(userId, cancellationToken);
                var graphMessage = await client.Me.Messages[messageId].GetAsync(cancellationToken: cancellationToken);

                if (graphMessage is not null)
                {
                    messages.Add(MapToEmailMessage(graphMessage));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Graph message {MessageId}", messageId);
            }
        }

        return messages;
    }

    /// <inheritdoc />
    public async Task SendAsync(
        OutboundEmail email,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var client = await GetGraphClientAsync(userId, cancellationToken);

        var message = new Message
        {
            Subject = email.Subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = email.Body
            },
            ToRecipients =
            [
                new Recipient
                {
                    EmailAddress = new EmailAddress { Address = email.To }
                }
            ]
        };

        if (email.Cc.Count > 0)
        {
            message.CcRecipients = email.Cc.Select(cc => new Recipient
            {
                EmailAddress = new EmailAddress { Address = cc }
            }).ToList();
        }

        if (!string.IsNullOrEmpty(email.InReplyToExternalId))
        {
            message.ConversationId = email.InReplyToExternalId;
        }

        if (email.Attachments.Count > 0)
        {
            message.Attachments = email.Attachments.Select(a => (Attachment)new FileAttachment
            {
                Name = a.FileName,
                ContentType = a.ContentType
            }).ToList();
        }

        await client.Me.SendMail.PostAsync(new SendMailPostRequestBody
        {
            Message = message,
            SaveToSentItems = true
        }, cancellationToken: cancellationToken);

        _logger.LogInformation("Sent email to {To} with subject '{Subject}'", email.To, email.Subject);
    }

    /// <inheritdoc />
    public async Task<Stream> GetAttachmentAsync(
        string externalMessageId,
        string contentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);

        // Use "me" as the default user for attachment fetching
        var client = await GetGraphClientAsync("me", cancellationToken);

        var attachment = await client.Me.Messages[externalMessageId]
            .Attachments[contentId]
            .GetAsync(cancellationToken: cancellationToken);

        if (attachment is FileAttachment fileAttachment && fileAttachment.ContentBytes is not null)
        {
            return new MemoryStream(fileAttachment.ContentBytes);
        }

        return new MemoryStream();
    }

    /// <inheritdoc />
    public async Task<SubscriptionRecord> CreateSubscriptionAsync(
        string userId,
        string notificationUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationUrl);

        var client = await GetGraphClientAsync(userId, cancellationToken);

        var subscription = new Subscription
        {
            ChangeType = "created",
            NotificationUrl = notificationUrl,
            Resource = SubscriptionResource,
            ExpirationDateTime = DateTimeOffset.UtcNow.Add(SubscriptionLifetime)
        };

        var created = await client.Subscriptions.PostAsync(subscription, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Created Graph subscription {SubscriptionId} for user {UserId}, expires {ExpiresAt}",
            created!.Id,
            userId,
            created.ExpirationDateTime);

        return new SubscriptionRecord
        {
            SubscriptionId = created.Id!,
            Provider = ProviderName,
            UserId = userId,
            ExpiresAt = created.ExpirationDateTime!.Value,
            Resource = SubscriptionResource
        };
    }

    /// <inheritdoc />
    public async Task<SubscriptionRecord> RenewSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        // Use "me" as the default user for subscription renewal
        var client = await GetGraphClientAsync("me", cancellationToken);

        var update = new Subscription
        {
            ExpirationDateTime = DateTimeOffset.UtcNow.Add(SubscriptionLifetime)
        };

        var renewed = await client.Subscriptions[subscriptionId]
            .PatchAsync(update, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Renewed Graph subscription {SubscriptionId}, new expiry {ExpiresAt}",
            subscriptionId,
            renewed!.ExpirationDateTime);

        return new SubscriptionRecord
        {
            SubscriptionId = subscriptionId,
            Provider = ProviderName,
            UserId = "me",
            ExpiresAt = renewed.ExpirationDateTime!.Value,
            Resource = SubscriptionResource
        };
    }

    /// <inheritdoc />
    public async Task DeleteSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        // Use "me" as the default user for subscription deletion
        var client = await GetGraphClientAsync("me", cancellationToken);

        await client.Subscriptions[subscriptionId]
            .DeleteAsync(cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted Graph subscription {SubscriptionId}", subscriptionId);
    }

    private async Task<GraphServiceClient> GetGraphClientAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.GetAsync(ProviderName, userId, cancellationToken);

        if (tokens is null)
        {
            throw new InvalidOperationException(
                $"No OAuth tokens found for provider '{ProviderName}' and user '{userId}'. " +
                "Complete the OAuth authorization flow first.");
        }

        if (tokens.ExpiresAt <= DateTimeOffset.UtcNow.Add(TokenRefreshBuffer))
        {
            _logger.LogDebug("Access token near expiry for user {UserId}, refreshing", userId);
            tokens = await RefreshTokenAsync(userId, tokens.RefreshToken, cancellationToken);
        }

        var credential = new AccessTokenCredential(tokens.AccessToken);
        return new GraphServiceClient(credential);
    }

    private async Task<TokenSet> RefreshTokenAsync(
        string userId,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();

        var tokenEndpoint = $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = "https://graph.microsoft.com/.default offline_access"
        });

        var response = await httpClient.PostAsync(tokenEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;
        var expiresIn = root.GetProperty("expires_in").GetInt32();

        var tokenSet = new TokenSet
        {
            AccessToken = root.GetProperty("access_token").GetString()!,
            RefreshToken = root.TryGetProperty("refresh_token", out var rt)
                ? rt.GetString()!
                : refreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
        };

        await _tokenStore.StoreAsync(ProviderName, userId, tokenSet, cancellationToken);

        _logger.LogDebug("Refreshed OAuth token for user {UserId}", userId);

        return tokenSet;
    }

    private static EmailMessage MapToEmailMessage(Message graphMessage)
    {
        var attachments = graphMessage.Attachments?
            .OfType<FileAttachment>()
            .Select(a => new EmailAttachment
            {
                FileName = a.Name ?? "unknown",
                ContentType = a.ContentType ?? "application/octet-stream",
                SizeBytes = a.Size ?? 0,
                ContentId = a.Id ?? string.Empty
            })
            .ToList() ?? [];

        return new EmailMessage
        {
            ExternalId = graphMessage.Id ?? string.Empty,
            From = graphMessage.From?.EmailAddress?.Address ?? string.Empty,
            To = graphMessage.ToRecipients?
                .Where(r => r.EmailAddress?.Address is not null)
                .Select(r => r.EmailAddress!.Address!)
                .ToList() ?? [],
            Subject = graphMessage.Subject ?? string.Empty,
            Body = graphMessage.Body?.Content ?? string.Empty,
            ThreadId = graphMessage.ConversationId,
            Cc = graphMessage.CcRecipients?
                .Where(r => r.EmailAddress?.Address is not null)
                .Select(r => r.EmailAddress!.Address!)
                .ToList() ?? [],
            Attachments = attachments,
            ReceivedAt = graphMessage.ReceivedDateTime ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Simple <see cref="TokenCredential"/> implementation that wraps a pre-obtained access token.
    /// Used to create a <see cref="GraphServiceClient"/> from stored OAuth tokens.
    /// </summary>
    internal sealed class AccessTokenCredential : TokenCredential
    {
        private readonly string _accessToken;

        /// <summary>
        /// Initialises a new <see cref="AccessTokenCredential"/> with the specified access token.
        /// </summary>
        /// <param name="accessToken">The OAuth access token.</param>
        public AccessTokenCredential(string accessToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
            _accessToken = accessToken;
        }

        /// <inheritdoc />
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new AccessToken(_accessToken, DateTimeOffset.MaxValue);
        }

        /// <inheritdoc />
        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken)
        {
            return new ValueTask<AccessToken>(new AccessToken(_accessToken, DateTimeOffset.MaxValue));
        }
    }
}
