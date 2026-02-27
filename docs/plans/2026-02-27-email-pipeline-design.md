# Email Pipeline Design — Issues #4 and #5

## Overview

Build the email pipeline for Cortex: ingest emails via Microsoft Graph API webhooks, analyse and draft responses via skills, and send replies with AskMeFirst approval gating. Provider-agnostic abstractions enable future swap to SMTP/IMAP or other webhook providers.

## Design Decisions

- **Microsoft 365 / Graph API** as primary provider, with `IEmailProvider` interface for future alternatives (SMTP/IMAP, SendGrid, etc.)
- **OAuth 2.0 authorization code flow** — user clicks "Connect Email" in Web UI, grants consent, tokens stored via `ITokenStore`
- **CoS triages emails into existing channels** — email-analyse skill suggests a channel, CoS routes accordingly; unmatched emails go to Default channel
- **Webhook endpoint in Cortex.Web** — single deployment; extract to separate project later if needed
- **Message-ID deduplication** — Graph API provides stable IDs per message; `IEmailDeduplicationStore` tracks seen IDs
- **Attachments** — metadata flows with messages; content fetched on demand via `IEmailProvider.GetAttachmentAsync()`
- **Outbound sending** — gated by AskMeFirst authority; uses existing PlanProposal/PlanApprovalResponse infrastructure
- **Automated subscription renewal** — background `IHostedService` renews Graph API subscriptions before expiry

## New Types

### Cortex.Core — Message Types

#### EmailAttachment

```csharp
public sealed record EmailAttachment
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public required string ContentId { get; init; }
}
```

#### EmailMessage

```csharp
public sealed record EmailMessage : IMessage
{
    public string MessageId { get; }
    public DateTimeOffset Timestamp { get; }
    public string? CorrelationId { get; }

    public required string ExternalId { get; init; }
    public required string From { get; init; }
    public required IReadOnlyList<string> To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string? ThreadId { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = [];
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];
    public DateTimeOffset ReceivedAt { get; init; }
}
```

#### OutboundEmail

```csharp
public sealed record OutboundEmail
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public IReadOnlyList<string> Cc { get; init; } = [];
    public string? InReplyToExternalId { get; init; }
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];
}
```

### Cortex.Core — Provider Abstraction

#### IEmailProvider

```csharp
public interface IEmailProvider
{
    Task<IReadOnlyList<EmailMessage>> ProcessWebhookAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken);

    string? HandleValidation(string payload, IDictionary<string, string> headers);

    Task SendAsync(OutboundEmail email, string userId,
        CancellationToken cancellationToken);

    Task<Stream> GetAttachmentAsync(
        string externalMessageId, string contentId,
        CancellationToken cancellationToken);

    Task<SubscriptionRecord> CreateSubscriptionAsync(
        string userId, string notificationUrl,
        CancellationToken cancellationToken);

    Task<SubscriptionRecord> RenewSubscriptionAsync(
        string subscriptionId, CancellationToken cancellationToken);

    Task DeleteSubscriptionAsync(
        string subscriptionId, CancellationToken cancellationToken);
}
```

#### EmailProviderOptions

```csharp
public sealed class EmailProviderOptions
{
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string RedirectUri { get; init; }
    public string? WebhookNotificationUrl { get; init; }
}
```

### Cortex.Core — Token Storage

#### TokenSet

```csharp
public sealed record TokenSet
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
```

#### ITokenStore

```csharp
public interface ITokenStore
{
    Task StoreAsync(string provider, string userId, TokenSet tokens,
        CancellationToken cancellationToken);
    Task<TokenSet?> GetAsync(string provider, string userId,
        CancellationToken cancellationToken);
    Task RemoveAsync(string provider, string userId,
        CancellationToken cancellationToken);
}
```

InMemoryTokenStore for Phase 1 — keyed by (provider, userId).

### Cortex.Core — Deduplication

#### IEmailDeduplicationStore

```csharp
public interface IEmailDeduplicationStore
{
    Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken);
    Task MarkSeenAsync(string externalId, CancellationToken cancellationToken);
}
```

InMemoryEmailDeduplicationStore — ConcurrentDictionary<string, DateTimeOffset>.

### Cortex.Core — Attachment Storage

#### IAttachmentStore

```csharp
public interface IAttachmentStore
{
    Task<string> StoreAsync(string referenceCode, string fileName,
        Stream content, string contentType, CancellationToken cancellationToken);
    Task<Stream?> GetAsync(string storageId, CancellationToken cancellationToken);
    Task RemoveAsync(string storageId, CancellationToken cancellationToken);
}
```

InMemoryAttachmentStore for Phase 1.

### Cortex.Core — Subscription Management

#### SubscriptionRecord

```csharp
public sealed record SubscriptionRecord
{
    public required string SubscriptionId { get; init; }
    public required string Provider { get; init; }
    public required string UserId { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string Resource { get; init; }
}
```

#### ISubscriptionStore

```csharp
public interface ISubscriptionStore
{
    Task StoreAsync(SubscriptionRecord record, CancellationToken cancellationToken);
    Task<IReadOnlyList<SubscriptionRecord>> GetExpiringAsync(
        TimeSpan withinWindow, CancellationToken cancellationToken);
    Task UpdateExpiryAsync(string subscriptionId, DateTimeOffset newExpiry,
        CancellationToken cancellationToken);
    Task RemoveAsync(string subscriptionId, CancellationToken cancellationToken);
}
```

InMemorySubscriptionStore for Phase 1.

## Cortex.Web — Implementation

### MicrosoftGraphEmailProvider

Implements `IEmailProvider` using the official `Microsoft.Graph` SDK:
- OAuth token acquisition and refresh via `ITokenStore`
- Change notification processing from Graph webhooks
- Message fetching, attachment retrieval, email sending
- Subscription lifecycle (create, renew, delete)

### SubscriptionRenewalService

`IHostedService` that keeps Graph API subscriptions alive:
- Periodic timer (default: 1 hour)
- Queries `ISubscriptionStore.GetExpiringAsync(withinWindow: 6 hours)`
- Calls `IEmailProvider.RenewSubscriptionAsync()` for each
- Updates store with new expiry
- Logs warnings on renewal failure
- Follows same pattern as `DelegationSupervisionService`

### HTTP Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/email/connect` | GET | Redirects to Microsoft OAuth consent screen |
| `/api/email/oauth/callback` | GET | Handles code exchange, stores tokens, creates webhook subscription |
| `/api/email/webhook` | POST | Receives Graph notifications, deduplicates, normalises, publishes to CoS |
| `/api/email/disconnect` | POST | Removes tokens, deletes webhook subscription |

### Webhook Ingestion Flow

```
Graph API notification
    → POST /api/email/webhook
    → Check for validation request → respond with token if so
    → IEmailProvider.ProcessWebhookAsync() → list of EmailMessage
    → For each EmailMessage:
        → IEmailDeduplicationStore.ExistsAsync() → skip if duplicate
        → MarkSeenAsync()
        → Generate ReferenceCode
        → Wrap in MessageEnvelope
        → Publish to "agent.cos" queue
    → Return 202 Accepted
```

## Skills

### email-analyse

- **skill-id**: email-analyse
- **category**: integration
- **executor**: llm
- **triggers**: email, EmailMessage

Input: EmailMessage content (from, subject, body, attachments metadata).

Output:
```json
{
    "summary": "Brief summary of what the email is about",
    "intent": "request | question | update | complaint | introduction | other",
    "urgency": "low | normal | high | critical",
    "suggestedChannel": "channel-id or null",
    "draftResponse": "Suggested reply text",
    "reasoning": "Why this draft is appropriate"
}
```

### email-send

- **skill-id**: email-send
- **category**: integration
- **executor**: csharp
- **authority**: AskMeFirst

Sends email via `IEmailProvider.SendAsync()`. Gated by AskMeFirst — uses existing PlanProposal/PlanApprovalResponse infrastructure. Human approves before any email leaves the system.

### email-fetch-attachment

- **skill-id**: email-fetch-attachment
- **category**: integration
- **executor**: csharp
- **authority**: JustDoIt

Fetches attachment content via `IEmailProvider.GetAttachmentAsync()`, stores in `IAttachmentStore`. No external footprint — just reading.

## End-to-End Data Flow

```
User clicks "Connect Email"
    → OAuth consent → tokens stored → subscription created
    → Graph API starts sending notifications

Graph notification arrives
    → Webhook validates, deduplicates, normalises
    → EmailMessage published to CoS queue

CoS receives EmailMessage
    → cos-triage routes to email-analyse capability
    → Email analyst agent runs email-analyse skill
    → Returns: summary, intent, urgency, suggested channel, draft response

Result routed to suggested channel
    → Human sees analysis + draft in their channel
    → Human reviews draft

If human approves sending:
    → AskMeFirst gating via PlanProposal
    → Human approves → email-send skill executes
    → Email sent via Graph API from connected mailbox

If attachments needed:
    → email-fetch-attachment skill called
    → Content fetched from Graph API, stored in IAttachmentStore
    → Available for analysis or forwarding
```

## Scope Summary

| Feature | Included |
|---------|----------|
| OAuth connect/disconnect flow | Yes |
| Inbound email via Graph webhook | Yes |
| Deduplication | Yes |
| Email analysis + draft response | Yes |
| Outbound email sending (AskMeFirst) | Yes |
| Attachment metadata in messages | Yes |
| Attachment content fetching on demand | Yes |
| Automated subscription renewal | Yes |
| CoS triage into existing channels | Yes |
| Multi-user / multi-mailbox | No (single user, single mailbox) |
