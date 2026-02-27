using Cortex.Core.Email;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.Web.Email;

/// <summary>
/// Handles email webhook processing — deduplication, normalisation, and publishing to CoS queue.
/// </summary>
public sealed class EmailWebhookHandler
{
    private readonly IEmailProvider _emailProvider;
    private readonly IEmailDeduplicationStore _deduplicationStore;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IReferenceCodeGenerator _referenceCodeGenerator;
    private readonly ILogger<EmailWebhookHandler> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="EmailWebhookHandler"/>.
    /// </summary>
    public EmailWebhookHandler(
        IEmailProvider emailProvider,
        IEmailDeduplicationStore deduplicationStore,
        IMessagePublisher messagePublisher,
        IReferenceCodeGenerator referenceCodeGenerator,
        ILogger<EmailWebhookHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(emailProvider);
        ArgumentNullException.ThrowIfNull(deduplicationStore);
        ArgumentNullException.ThrowIfNull(messagePublisher);
        ArgumentNullException.ThrowIfNull(referenceCodeGenerator);
        ArgumentNullException.ThrowIfNull(logger);

        _emailProvider = emailProvider;
        _deduplicationStore = deduplicationStore;
        _messagePublisher = messagePublisher;
        _referenceCodeGenerator = referenceCodeGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Processes an inbound webhook payload. Returns validation info and count of messages published.
    /// </summary>
    public async Task<(bool IsValidation, string? ValidationToken, int PublishedCount)> HandleWebhookAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        // 1. Check for subscription validation
        var validationToken = _emailProvider.HandleValidation(payload, headers);
        if (validationToken is not null)
        {
            _logger.LogInformation("Handled webhook subscription validation");
            return (true, validationToken, 0);
        }

        // 2. Process notifications
        var emails = await _emailProvider.ProcessWebhookAsync(payload, headers, cancellationToken);
        var publishedCount = 0;

        foreach (var email in emails)
        {
            // 3. Deduplicate
            if (await _deduplicationStore.ExistsAsync(email.ExternalId, cancellationToken))
            {
                _logger.LogDebug("Skipping duplicate email {ExternalId}", email.ExternalId);
                continue;
            }

            await _deduplicationStore.MarkSeenAsync(email.ExternalId, cancellationToken);

            // 4. Wrap and publish
            var referenceCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
            var envelope = new MessageEnvelope
            {
                Message = email,
                ReferenceCode = referenceCode
            };

            await _messagePublisher.PublishAsync(envelope, "agent.cos", cancellationToken);
            publishedCount++;

            _logger.LogInformation(
                "Published email {ExternalId} from {From} with reference {ReferenceCode}",
                email.ExternalId, email.From, referenceCode);
        }

        return (false, null, publishedCount);
    }
}

/// <summary>
/// Registers email-related HTTP endpoints.
/// </summary>
public static class EmailEndpoints
{
    /// <summary>
    /// Maps email endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/email");

        group.MapGet("/connect", HandleConnect);
        group.MapGet("/oauth/callback", HandleOAuthCallback);
        group.MapPost("/webhook", HandleWebhook);
        group.MapPost("/disconnect", HandleDisconnect);

        return endpoints;
    }

    private static IResult HandleConnect(IOptions<EmailProviderOptions> options)
    {
        var o = options.Value;
        var scopes = Uri.EscapeDataString("openid offline_access Mail.Read Mail.Send");
        var redirectUri = Uri.EscapeDataString(o.RedirectUri);
        var authUrl = $"https://login.microsoftonline.com/{o.TenantId}/oauth2/v2.0/authorize" +
                      $"?client_id={o.ClientId}" +
                      $"&response_type=code" +
                      $"&redirect_uri={redirectUri}" +
                      $"&scope={scopes}" +
                      $"&response_mode=query";
        return Results.Redirect(authUrl);
    }

    private static async Task<IResult> HandleOAuthCallback(
        HttpContext context,
        IOptions<EmailProviderOptions> options,
        ITokenStore tokenStore,
        IEmailProvider emailProvider,
        ISubscriptionStore subscriptionStore,
        ILogger<EmailWebhookHandler> logger)
    {
        var code = context.Request.Query["code"].ToString();
        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.BadRequest("Missing authorization code");
        }

        var o = options.Value;

        using var httpClient = new HttpClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = o.ClientId,
            ["client_secret"] = o.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = o.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["scope"] = "openid offline_access Mail.Read Mail.Send"
        });

        var response = await httpClient.PostAsync(
            $"https://login.microsoftonline.com/{o.TenantId}/oauth2/v2.0/token",
            content);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Token exchange failed: {Status}", response.StatusCode);
            return Results.Problem("Failed to exchange authorization code for tokens");
        }

        using var doc = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var tokens = new TokenSet
        {
            AccessToken = doc.RootElement.GetProperty("access_token").GetString()!,
            RefreshToken = doc.RootElement.GetProperty("refresh_token").GetString()!,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(
                doc.RootElement.GetProperty("expires_in").GetInt32())
        };

        await tokenStore.StoreAsync("microsoft", "default", tokens);

        if (!string.IsNullOrWhiteSpace(o.WebhookNotificationUrl))
        {
            var subscription = await emailProvider.CreateSubscriptionAsync(
                "default", o.WebhookNotificationUrl);
            await subscriptionStore.StoreAsync(subscription);

            logger.LogInformation(
                "Email connected: subscription {SubscriptionId} created, expires {ExpiresAt}",
                subscription.SubscriptionId, subscription.ExpiresAt);
        }

        return Results.Ok(new { message = "Email connected successfully" });
    }

    private static async Task<IResult> HandleWebhook(
        HttpContext context,
        EmailWebhookHandler handler)
    {
        using var reader = new StreamReader(context.Request.Body);
        var payload = await reader.ReadToEndAsync();

        var headers = context.Request.Headers
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        var (isValidation, validationToken, publishedCount) =
            await handler.HandleWebhookAsync(payload, headers, context.RequestAborted);

        if (isValidation)
        {
            return Results.Text(validationToken!);
        }

        return Results.Accepted(value: new { processed = publishedCount });
    }

    private static async Task<IResult> HandleDisconnect(
        ITokenStore tokenStore,
        ISubscriptionStore subscriptionStore,
        IEmailProvider emailProvider,
        ILogger<EmailWebhookHandler> logger)
    {
        var subscriptions = await subscriptionStore.GetExpiringAsync(TimeSpan.FromDays(365));
        foreach (var sub in subscriptions)
        {
            try
            {
                await emailProvider.DeleteSubscriptionAsync(sub.SubscriptionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete subscription {SubscriptionId}", sub.SubscriptionId);
            }

            await subscriptionStore.RemoveAsync(sub.SubscriptionId);
        }

        await tokenStore.RemoveAsync("microsoft", "default");

        logger.LogInformation("Email disconnected");
        return Results.Ok(new { message = "Email disconnected" });
    }
}
