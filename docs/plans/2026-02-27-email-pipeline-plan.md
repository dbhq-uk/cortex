# Email Pipeline Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the email pipeline — ingest via Microsoft Graph API webhooks, analyse and draft responses via skills, send replies with AskMeFirst gating.

**Architecture:** Provider-agnostic `IEmailProvider` interface in Cortex.Core with Microsoft Graph implementation in Cortex.Web. OAuth connect flow in Web UI. Background subscription renewal service. Three skills: email-analyse (LLM), email-send (C#), email-fetch-attachment (C#).

**Tech Stack:** .NET 10, Microsoft.Graph SDK, ASP.NET minimal APIs, RabbitMQ (via existing IMessagePublisher)

---

### Task 1: Email Message Types

Create `EmailAttachment`, `EmailMessage`, and `OutboundEmail` records in `Cortex.Core.Messages`, following the existing `TextMessage` and `PlanProposal` patterns.

**Files:**
- Create: `src/Cortex.Core/Messages/EmailAttachment.cs`
- Create: `src/Cortex.Core/Messages/EmailMessage.cs`
- Create: `src/Cortex.Core/Messages/OutboundEmail.cs`
- Create: `tests/Cortex.Core.Tests/Messages/EmailMessageTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Messages;

namespace Cortex.Core.Tests.Messages;

public class EmailMessageTests
{
    [Fact]
    public void EmailAttachment_CarriesMetadata()
    {
        var attachment = new EmailAttachment
        {
            FileName = "report.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1024,
            ContentId = "att-001"
        };

        Assert.Equal("report.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(1024, attachment.SizeBytes);
        Assert.Equal("att-001", attachment.ContentId);
    }

    [Fact]
    public void EmailMessage_ImplementsIMessage_HasMessageIdAndTimestamp()
    {
        var email = new EmailMessage
        {
            ExternalId = "graph-msg-123",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "Hello",
            Body = "Hi Bob",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        IMessage message = email;
        Assert.NotNull(message.MessageId);
        Assert.NotEqual(default, message.Timestamp);
    }

    [Fact]
    public void EmailMessage_CarriesAllFields()
    {
        var now = DateTimeOffset.UtcNow;
        var attachment = new EmailAttachment
        {
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            SizeBytes = 2048,
            ContentId = "att-002"
        };

        var email = new EmailMessage
        {
            ExternalId = "graph-msg-456",
            From = "alice@example.com",
            To = ["bob@example.com", "carol@example.com"],
            Subject = "Project update",
            Body = "Here is the update",
            ThreadId = "thread-789",
            Cc = ["dave@example.com"],
            Attachments = [attachment],
            ReceivedAt = now
        };

        Assert.Equal("graph-msg-456", email.ExternalId);
        Assert.Equal("alice@example.com", email.From);
        Assert.Equal(2, email.To.Count);
        Assert.Equal("Project update", email.Subject);
        Assert.Equal("Here is the update", email.Body);
        Assert.Equal("thread-789", email.ThreadId);
        Assert.Single(email.Cc);
        Assert.Single(email.Attachments);
        Assert.Equal(now, email.ReceivedAt);
    }

    [Fact]
    public void EmailMessage_DefaultCollections_AreEmpty()
    {
        var email = new EmailMessage
        {
            ExternalId = "graph-msg-789",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "Test",
            Body = "Test body",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        Assert.Empty(email.Cc);
        Assert.Empty(email.Attachments);
        Assert.Null(email.ThreadId);
        Assert.Null(email.CorrelationId);
    }

    [Fact]
    public void OutboundEmail_CarriesAllFields()
    {
        var outbound = new OutboundEmail
        {
            To = "bob@example.com",
            Subject = "Re: Hello",
            Body = "Thanks for your email",
            Cc = ["carol@example.com"],
            InReplyToExternalId = "graph-msg-123"
        };

        Assert.Equal("bob@example.com", outbound.To);
        Assert.Equal("Re: Hello", outbound.Subject);
        Assert.Equal("Thanks for your email", outbound.Body);
        Assert.Single(outbound.Cc);
        Assert.Equal("graph-msg-123", outbound.InReplyToExternalId);
    }

    [Fact]
    public void OutboundEmail_DefaultCollections_AreEmpty()
    {
        var outbound = new OutboundEmail
        {
            To = "bob@example.com",
            Subject = "Test",
            Body = "Test body"
        };

        Assert.Empty(outbound.Cc);
        Assert.Empty(outbound.Attachments);
        Assert.Null(outbound.InReplyToExternalId);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~EmailMessageTests" --verbosity normal`
Expected: FAIL — types do not exist.

**Step 3: Write the implementations**

`EmailAttachment.cs`:
```csharp
namespace Cortex.Core.Messages;

/// <summary>
/// Metadata for an email attachment. Content is fetched on demand via <see cref="Email.IEmailProvider"/>.
/// </summary>
public sealed record EmailAttachment
{
    /// <summary>The file name of the attachment.</summary>
    public required string FileName { get; init; }

    /// <summary>The MIME content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>The size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>The provider-specific content identifier, used to fetch the attachment content.</summary>
    public required string ContentId { get; init; }
}
```

`EmailMessage.cs`:
```csharp
namespace Cortex.Core.Messages;

/// <summary>
/// A normalised inbound email message.
/// </summary>
public sealed record EmailMessage : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>The provider-specific message identifier (e.g. Graph API message ID).</summary>
    public required string ExternalId { get; init; }

    /// <summary>The sender email address.</summary>
    public required string From { get; init; }

    /// <summary>The recipient email addresses.</summary>
    public required IReadOnlyList<string> To { get; init; }

    /// <summary>The email subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>The email body as plain text.</summary>
    public required string Body { get; init; }

    /// <summary>The provider-specific thread/conversation identifier.</summary>
    public string? ThreadId { get; init; }

    /// <summary>The CC recipient email addresses.</summary>
    public IReadOnlyList<string> Cc { get; init; } = [];

    /// <summary>Attachment metadata. Content is fetched on demand.</summary>
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];

    /// <summary>When the email was received by the mail server.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }
}
```

`OutboundEmail.cs`:
```csharp
namespace Cortex.Core.Messages;

/// <summary>
/// An outbound email to be sent via <see cref="Email.IEmailProvider"/>.
/// </summary>
public sealed record OutboundEmail
{
    /// <summary>The recipient email address.</summary>
    public required string To { get; init; }

    /// <summary>The email subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>The email body.</summary>
    public required string Body { get; init; }

    /// <summary>CC recipient email addresses.</summary>
    public IReadOnlyList<string> Cc { get; init; } = [];

    /// <summary>The external ID of the email being replied to, for threading.</summary>
    public string? InReplyToExternalId { get; init; }

    /// <summary>Attachments to include.</summary>
    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~EmailMessageTests" --verbosity normal`
Expected: 6 passed.

**Step 5: Commit**

```bash
git add src/Cortex.Core/Messages/EmailAttachment.cs src/Cortex.Core/Messages/EmailMessage.cs src/Cortex.Core/Messages/OutboundEmail.cs tests/Cortex.Core.Tests/Messages/EmailMessageTests.cs
git commit -m "feat(messages): EmailAttachment, EmailMessage, and OutboundEmail types (#4, #5)"
```

---

### Task 2: Token Storage

Create `TokenSet`, `ITokenStore`, and `InMemoryTokenStore` in `Cortex.Core.Email` for OAuth token management.

**Files:**
- Create: `src/Cortex.Core/Email/TokenSet.cs`
- Create: `src/Cortex.Core/Email/ITokenStore.cs`
- Create: `src/Cortex.Core/Email/InMemoryTokenStore.cs`
- Create: `tests/Cortex.Core.Tests/Email/InMemoryTokenStoreTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Email;

namespace Cortex.Core.Tests.Email;

public class InMemoryTokenStoreTests
{
    private readonly InMemoryTokenStore _store = new();

    [Fact]
    public async Task StoreAsync_ThenGetAsync_ReturnsTokens()
    {
        var tokens = new TokenSet
        {
            AccessToken = "access-123",
            RefreshToken = "refresh-456",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _store.StoreAsync("microsoft", "user-1", tokens);
        var result = await _store.GetAsync("microsoft", "user-1");

        Assert.NotNull(result);
        Assert.Equal("access-123", result.AccessToken);
        Assert.Equal("refresh-456", result.RefreshToken);
    }

    [Fact]
    public async Task GetAsync_Nonexistent_ReturnsNull()
    {
        var result = await _store.GetAsync("microsoft", "no-such-user");
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreAsync_OverwritesExisting()
    {
        var original = new TokenSet
        {
            AccessToken = "old",
            RefreshToken = "old-refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        var updated = new TokenSet
        {
            AccessToken = "new",
            RefreshToken = "new-refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
        };

        await _store.StoreAsync("microsoft", "user-1", original);
        await _store.StoreAsync("microsoft", "user-1", updated);

        var result = await _store.GetAsync("microsoft", "user-1");
        Assert.NotNull(result);
        Assert.Equal("new", result.AccessToken);
    }

    [Fact]
    public async Task RemoveAsync_ThenGetAsync_ReturnsNull()
    {
        var tokens = new TokenSet
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _store.StoreAsync("microsoft", "user-1", tokens);
        await _store.RemoveAsync("microsoft", "user-1");

        var result = await _store.GetAsync("microsoft", "user-1");
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_Nonexistent_DoesNotThrow()
    {
        await _store.RemoveAsync("microsoft", "no-such-user");
    }

    [Fact]
    public async Task DifferentProviders_AreSeparate()
    {
        var msTokens = new TokenSet
        {
            AccessToken = "ms-access",
            RefreshToken = "ms-refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        var googleTokens = new TokenSet
        {
            AccessToken = "google-access",
            RefreshToken = "google-refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await _store.StoreAsync("microsoft", "user-1", msTokens);
        await _store.StoreAsync("google", "user-1", googleTokens);

        var ms = await _store.GetAsync("microsoft", "user-1");
        var google = await _store.GetAsync("google", "user-1");

        Assert.Equal("ms-access", ms!.AccessToken);
        Assert.Equal("google-access", google!.AccessToken);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryTokenStoreTests" --verbosity normal`
Expected: FAIL — types do not exist.

**Step 3: Write the implementations**

`TokenSet.cs`:
```csharp
namespace Cortex.Core.Email;

/// <summary>
/// OAuth token set containing access and refresh tokens.
/// </summary>
public sealed record TokenSet
{
    /// <summary>The OAuth access token.</summary>
    public required string AccessToken { get; init; }

    /// <summary>The OAuth refresh token for obtaining new access tokens.</summary>
    public required string RefreshToken { get; init; }

    /// <summary>When the access token expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
```

`ITokenStore.cs`:
```csharp
namespace Cortex.Core.Email;

/// <summary>
/// Stores OAuth tokens keyed by provider and user.
/// </summary>
public interface ITokenStore
{
    /// <summary>Stores or overwrites tokens for the given provider and user.</summary>
    Task StoreAsync(string provider, string userId, TokenSet tokens, CancellationToken cancellationToken = default);

    /// <summary>Retrieves tokens for the given provider and user, or null if not found.</summary>
    Task<TokenSet?> GetAsync(string provider, string userId, CancellationToken cancellationToken = default);

    /// <summary>Removes tokens for the given provider and user.</summary>
    Task RemoveAsync(string provider, string userId, CancellationToken cancellationToken = default);
}
```

`InMemoryTokenStore.cs`:
```csharp
using System.Collections.Concurrent;

namespace Cortex.Core.Email;

/// <summary>
/// In-memory implementation of <see cref="ITokenStore"/> for development and testing.
/// </summary>
public sealed class InMemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<(string Provider, string UserId), TokenSet> _tokens = new();

    /// <inheritdoc />
    public Task StoreAsync(string provider, string userId, TokenSet tokens, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(tokens);

        _tokens[(provider, userId)] = tokens;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TokenSet?> GetAsync(string provider, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _tokens.TryGetValue((provider, userId), out var tokens);
        return Task.FromResult(tokens);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string provider, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        _tokens.TryRemove((provider, userId), out _);
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryTokenStoreTests" --verbosity normal`
Expected: 6 passed.

**Step 5: Commit**

```bash
git add src/Cortex.Core/Email/TokenSet.cs src/Cortex.Core/Email/ITokenStore.cs src/Cortex.Core/Email/InMemoryTokenStore.cs tests/Cortex.Core.Tests/Email/InMemoryTokenStoreTests.cs
git commit -m "feat(email): TokenSet, ITokenStore, and InMemoryTokenStore (#4)"
```

---

### Task 3: Email Deduplication Store

Create `IEmailDeduplicationStore` and `InMemoryEmailDeduplicationStore` in `Cortex.Core.Email`.

**Files:**
- Create: `src/Cortex.Core/Email/IEmailDeduplicationStore.cs`
- Create: `src/Cortex.Core/Email/InMemoryEmailDeduplicationStore.cs`
- Create: `tests/Cortex.Core.Tests/Email/InMemoryEmailDeduplicationStoreTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Email;

namespace Cortex.Core.Tests.Email;

public class InMemoryEmailDeduplicationStoreTests
{
    private readonly InMemoryEmailDeduplicationStore _store = new();

    [Fact]
    public async Task ExistsAsync_Unseen_ReturnsFalse()
    {
        var result = await _store.ExistsAsync("msg-001");
        Assert.False(result);
    }

    [Fact]
    public async Task MarkSeenAsync_ThenExistsAsync_ReturnsTrue()
    {
        await _store.MarkSeenAsync("msg-001");
        var result = await _store.ExistsAsync("msg-001");
        Assert.True(result);
    }

    [Fact]
    public async Task MarkSeenAsync_Idempotent_DoesNotThrow()
    {
        await _store.MarkSeenAsync("msg-001");
        await _store.MarkSeenAsync("msg-001");

        var result = await _store.ExistsAsync("msg-001");
        Assert.True(result);
    }

    [Fact]
    public async Task DifferentIds_AreIndependent()
    {
        await _store.MarkSeenAsync("msg-001");

        Assert.True(await _store.ExistsAsync("msg-001"));
        Assert.False(await _store.ExistsAsync("msg-002"));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryEmailDeduplicationStoreTests" --verbosity normal`
Expected: FAIL — types do not exist.

**Step 3: Write the implementations**

`IEmailDeduplicationStore.cs`:
```csharp
namespace Cortex.Core.Email;

/// <summary>
/// Tracks seen email identifiers to prevent duplicate processing.
/// </summary>
public interface IEmailDeduplicationStore
{
    /// <summary>Returns true if the email identifier has already been seen.</summary>
    Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>Marks an email identifier as seen.</summary>
    Task MarkSeenAsync(string externalId, CancellationToken cancellationToken = default);
}
```

`InMemoryEmailDeduplicationStore.cs`:
```csharp
using System.Collections.Concurrent;

namespace Cortex.Core.Email;

/// <summary>
/// In-memory implementation of <see cref="IEmailDeduplicationStore"/> for development and testing.
/// </summary>
public sealed class InMemoryEmailDeduplicationStore : IEmailDeduplicationStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new();

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return Task.FromResult(_seen.ContainsKey(externalId));
    }

    /// <inheritdoc />
    public Task MarkSeenAsync(string externalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        _seen.TryAdd(externalId, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryEmailDeduplicationStoreTests" --verbosity normal`
Expected: 4 passed.

**Step 5: Commit**

```bash
git add src/Cortex.Core/Email/IEmailDeduplicationStore.cs src/Cortex.Core/Email/InMemoryEmailDeduplicationStore.cs tests/Cortex.Core.Tests/Email/InMemoryEmailDeduplicationStoreTests.cs
git commit -m "feat(email): IEmailDeduplicationStore and InMemoryEmailDeduplicationStore (#4)"
```

---

### Task 4: Attachment Storage

Create `IAttachmentStore` and `InMemoryAttachmentStore` in `Cortex.Core.Email`.

**Files:**
- Create: `src/Cortex.Core/Email/IAttachmentStore.cs`
- Create: `src/Cortex.Core/Email/InMemoryAttachmentStore.cs`
- Create: `tests/Cortex.Core.Tests/Email/InMemoryAttachmentStoreTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Text;
using Cortex.Core.Email;

namespace Cortex.Core.Tests.Email;

public class InMemoryAttachmentStoreTests
{
    private readonly InMemoryAttachmentStore _store = new();

    [Fact]
    public async Task StoreAsync_ThenGetAsync_ReturnsContent()
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("hello world"));
        var storageId = await _store.StoreAsync("CTX-2026-0227-001", "doc.pdf", content, "application/pdf");

        Assert.NotNull(storageId);

        var result = await _store.GetAsync(storageId);
        Assert.NotNull(result);

        using var reader = new StreamReader(result);
        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task GetAsync_Nonexistent_ReturnsNull()
    {
        var result = await _store.GetAsync("no-such-id");
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_ThenGetAsync_ReturnsNull()
    {
        var content = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var storageId = await _store.StoreAsync("CTX-2026-0227-001", "file.txt", content, "text/plain");

        await _store.RemoveAsync(storageId);

        var result = await _store.GetAsync(storageId);
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_Nonexistent_DoesNotThrow()
    {
        await _store.RemoveAsync("no-such-id");
    }

    [Fact]
    public async Task StoreAsync_MultipleFiles_UniquStorageIds()
    {
        var content1 = new MemoryStream(Encoding.UTF8.GetBytes("file1"));
        var content2 = new MemoryStream(Encoding.UTF8.GetBytes("file2"));

        var id1 = await _store.StoreAsync("CTX-2026-0227-001", "a.txt", content1, "text/plain");
        var id2 = await _store.StoreAsync("CTX-2026-0227-001", "b.txt", content2, "text/plain");

        Assert.NotEqual(id1, id2);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryAttachmentStoreTests" --verbosity normal`
Expected: FAIL — types do not exist.

**Step 3: Write the implementations**

`IAttachmentStore.cs`:
```csharp
namespace Cortex.Core.Email;

/// <summary>
/// Stores fetched attachment content for downstream processing.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>Stores attachment content and returns a storage identifier.</summary>
    Task<string> StoreAsync(string referenceCode, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Retrieves attachment content by storage identifier, or null if not found.</summary>
    Task<Stream?> GetAsync(string storageId, CancellationToken cancellationToken = default);

    /// <summary>Removes attachment content by storage identifier.</summary>
    Task RemoveAsync(string storageId, CancellationToken cancellationToken = default);
}
```

`InMemoryAttachmentStore.cs`:
```csharp
using System.Collections.Concurrent;

namespace Cortex.Core.Email;

/// <summary>
/// In-memory implementation of <see cref="IAttachmentStore"/> for development and testing.
/// </summary>
public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new();

    /// <inheritdoc />
    public async Task<string> StoreAsync(string referenceCode, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken);

        var storageId = $"{referenceCode}/{Guid.NewGuid():N}";
        _store[storageId] = memoryStream.ToArray();
        return storageId;
    }

    /// <inheritdoc />
    public Task<Stream?> GetAsync(string storageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageId);

        if (_store.TryGetValue(storageId, out var data))
        {
            return Task.FromResult<Stream?>(new MemoryStream(data));
        }

        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string storageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageId);

        _store.TryRemove(storageId, out _);
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryAttachmentStoreTests" --verbosity normal`
Expected: 5 passed.

**Step 5: Commit**

```bash
git add src/Cortex.Core/Email/IAttachmentStore.cs src/Cortex.Core/Email/InMemoryAttachmentStore.cs tests/Cortex.Core.Tests/Email/InMemoryAttachmentStoreTests.cs
git commit -m "feat(email): IAttachmentStore and InMemoryAttachmentStore (#5)"
```

---

### Task 5: Subscription Storage

Create `SubscriptionRecord`, `ISubscriptionStore`, and `InMemorySubscriptionStore` in `Cortex.Core.Email`.

**Files:**
- Create: `src/Cortex.Core/Email/SubscriptionRecord.cs`
- Create: `src/Cortex.Core/Email/ISubscriptionStore.cs`
- Create: `src/Cortex.Core/Email/InMemorySubscriptionStore.cs`
- Create: `tests/Cortex.Core.Tests/Email/InMemorySubscriptionStoreTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Email;

namespace Cortex.Core.Tests.Email;

public class InMemorySubscriptionStoreTests
{
    private readonly InMemorySubscriptionStore _store = new();

    private static SubscriptionRecord CreateRecord(string id = "sub-001", DateTimeOffset? expiresAt = null) => new()
    {
        SubscriptionId = id,
        Provider = "microsoft",
        UserId = "user-1",
        ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(3),
        Resource = "me/messages"
    };

    [Fact]
    public async Task StoreAsync_ThenGetExpiringAsync_ReturnsRecord()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(2);
        var record = CreateRecord(expiresAt: expiresAt);

        await _store.StoreAsync(record);

        var expiring = await _store.GetExpiringAsync(TimeSpan.FromHours(6));
        Assert.Single(expiring);
        Assert.Equal("sub-001", expiring[0].SubscriptionId);
    }

    [Fact]
    public async Task GetExpiringAsync_NotExpiringSoon_ReturnsEmpty()
    {
        var record = CreateRecord(expiresAt: DateTimeOffset.UtcNow.AddDays(3));

        await _store.StoreAsync(record);

        var expiring = await _store.GetExpiringAsync(TimeSpan.FromHours(6));
        Assert.Empty(expiring);
    }

    [Fact]
    public async Task UpdateExpiryAsync_UpdatesExpiry()
    {
        var record = CreateRecord(expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        await _store.StoreAsync(record);

        var newExpiry = DateTimeOffset.UtcNow.AddDays(3);
        await _store.UpdateExpiryAsync("sub-001", newExpiry);

        var expiring = await _store.GetExpiringAsync(TimeSpan.FromHours(6));
        Assert.Empty(expiring);
    }

    [Fact]
    public async Task RemoveAsync_ThenGetExpiringAsync_ReturnsEmpty()
    {
        var record = CreateRecord(expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        await _store.StoreAsync(record);

        await _store.RemoveAsync("sub-001");

        var expiring = await _store.GetExpiringAsync(TimeSpan.FromHours(6));
        Assert.Empty(expiring);
    }

    [Fact]
    public async Task RemoveAsync_Nonexistent_DoesNotThrow()
    {
        await _store.RemoveAsync("no-such-sub");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemorySubscriptionStoreTests" --verbosity normal`
Expected: FAIL — types do not exist.

**Step 3: Write the implementations**

`SubscriptionRecord.cs`:
```csharp
namespace Cortex.Core.Email;

/// <summary>
/// Tracks a webhook subscription with a provider.
/// </summary>
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
```

`ISubscriptionStore.cs`:
```csharp
namespace Cortex.Core.Email;

/// <summary>
/// Stores webhook subscription records for tracking and renewal.
/// </summary>
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
```

`InMemorySubscriptionStore.cs`:
```csharp
using System.Collections.Concurrent;

namespace Cortex.Core.Email;

/// <summary>
/// In-memory implementation of <see cref="ISubscriptionStore"/> for development and testing.
/// </summary>
public sealed class InMemorySubscriptionStore : ISubscriptionStore
{
    private readonly ConcurrentDictionary<string, SubscriptionRecord> _subscriptions = new();

    /// <inheritdoc />
    public Task StoreAsync(SubscriptionRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        _subscriptions[record.SubscriptionId] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SubscriptionRecord>> GetExpiringAsync(TimeSpan withinWindow, CancellationToken cancellationToken = default)
    {
        var threshold = DateTimeOffset.UtcNow.Add(withinWindow);
        var expiring = _subscriptions.Values
            .Where(s => s.ExpiresAt <= threshold)
            .ToList();

        return Task.FromResult<IReadOnlyList<SubscriptionRecord>>(expiring);
    }

    /// <inheritdoc />
    public Task UpdateExpiryAsync(string subscriptionId, DateTimeOffset newExpiry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        if (_subscriptions.TryGetValue(subscriptionId, out var record))
        {
            _subscriptions[subscriptionId] = record with { ExpiresAt = newExpiry };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        _subscriptions.TryRemove(subscriptionId, out _);
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemorySubscriptionStoreTests" --verbosity normal`
Expected: 5 passed.

**Step 5: Commit**

```bash
git add src/Cortex.Core/Email/SubscriptionRecord.cs src/Cortex.Core/Email/ISubscriptionStore.cs src/Cortex.Core/Email/InMemorySubscriptionStore.cs tests/Cortex.Core.Tests/Email/InMemorySubscriptionStoreTests.cs
git commit -m "feat(email): SubscriptionRecord, ISubscriptionStore, and InMemorySubscriptionStore (#4)"
```

---

### Task 6: Email Provider Contract

Create `EmailProviderOptions` and `IEmailProvider` interface in `Cortex.Core.Email`. These are contracts only — no implementation in this task.

**Files:**
- Create: `src/Cortex.Core/Email/EmailProviderOptions.cs`
- Create: `src/Cortex.Core/Email/IEmailProvider.cs`

**Step 1: Write the implementations** (no tests needed — these are pure interfaces/config)

`EmailProviderOptions.cs`:
```csharp
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
```

`IEmailProvider.cs`:
```csharp
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
```

**Step 2: Build to verify compilation**

Run: `dotnet build src/Cortex.Core/Cortex.Core.csproj --configuration Release`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/Cortex.Core/Email/EmailProviderOptions.cs src/Cortex.Core/Email/IEmailProvider.cs
git commit -m "feat(email): IEmailProvider interface and EmailProviderOptions (#4, #5)"
```

---

### Task 7: Skill Definitions

Create the three email skill markdown files following the existing `cos-triage.md` and `cos-decompose.md` patterns.

**Files:**
- Create: `skills/email-analyse.md`
- Create: `skills/email-send.md`
- Create: `skills/email-fetch-attachment.md`

**Step 1: Write the skill definitions**

`email-analyse.md`:
```markdown
# email-analyse

## Metadata
- **skill-id**: email-analyse
- **category**: integration
- **executor**: llm
- **version**: 1.0.0

## Description

Analyses inbound emails and produces a summary, classification, and draft response.

## Triggers

- email
- EmailMessage

## Prompt

You are an email analyst for a business operating system called Cortex. Your job is to analyse incoming emails and prepare a response.

Given an email with sender, subject, body, and any attachment metadata, determine:

1. A concise summary of the email's content and purpose
2. The sender's intent: request, question, update, complaint, introduction, or other
3. The urgency level: low, normal, high, or critical
4. Which channel this email belongs to (from the available channels list), or null if unknown
5. A professional draft response appropriate for the context
6. Brief reasoning explaining why the draft response is appropriate

Guidelines:
- Match tone and formality to the original email
- Be concise but thorough in the summary
- Flag anything that seems urgent or requires immediate attention
- If attachments are present, note them in the summary
- Draft responses should be helpful and action-oriented

Respond with JSON only, no markdown formatting:

{"summary": "Brief summary of the email", "intent": "request", "urgency": "normal", "suggestedChannel": "channel-id or null", "draftResponse": "Your suggested reply text here", "reasoning": "Why this draft is appropriate"}
```

`email-send.md`:
```markdown
# email-send

## Metadata
- **skill-id**: email-send
- **category**: integration
- **executor**: csharp
- **version**: 1.0.0

## Description

Sends an outbound email via the configured email provider. Requires AskMeFirst authority — sending email has external footprint.

## Authority

AskMeFirst

## Parameters

- **to**: Recipient email address (required)
- **subject**: Email subject line (required)
- **body**: Email body content (required)
- **cc**: Comma-separated CC addresses (optional)
- **inReplyToExternalId**: External ID of email being replied to, for threading (optional)
```

`email-fetch-attachment.md`:
```markdown
# email-fetch-attachment

## Metadata
- **skill-id**: email-fetch-attachment
- **category**: integration
- **executor**: csharp
- **version**: 1.0.0

## Description

Fetches attachment content from the email provider and stores it locally for processing. JustDoIt authority — reading has no external footprint.

## Authority

JustDoIt

## Parameters

- **externalMessageId**: The provider message ID containing the attachment (required)
- **contentId**: The provider attachment ID to fetch (required)
- **fileName**: The attachment file name for storage (required)
- **contentType**: The MIME type of the attachment (required)
- **referenceCode**: The reference code to associate with the stored attachment (required)
```

**Step 2: Build to verify nothing is broken**

Run: `dotnet build --configuration Release`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add skills/email-analyse.md skills/email-send.md skills/email-fetch-attachment.md
git commit -m "feat(skills): email-analyse, email-send, and email-fetch-attachment skill definitions (#4, #5)"
```

---

### Task 8: Microsoft Graph Email Provider

Create `MicrosoftGraphEmailProvider` implementing `IEmailProvider` using the Microsoft.Graph SDK in `Cortex.Web`. Also create the Cortex.Web.Tests project.

**Files:**
- Modify: `src/Cortex.Web/Cortex.Web.csproj` (add Microsoft.Graph and Microsoft.Identity.Client packages)
- Create: `src/Cortex.Web/Email/MicrosoftGraphEmailProvider.cs`
- Create: `tests/Cortex.Web.Tests/Cortex.Web.Tests.csproj`
- Create: `tests/Cortex.Web.Tests/Email/MicrosoftGraphEmailProviderTests.cs`

**Step 1: Add NuGet packages**

Run:
```bash
dotnet add src/Cortex.Web/Cortex.Web.csproj package Microsoft.Graph --version 5.*
dotnet add src/Cortex.Web/Cortex.Web.csproj package Azure.Identity --version 1.*
```

**Step 2: Create the test project**

Run:
```bash
dotnet new xunit -n Cortex.Web.Tests -o tests/Cortex.Web.Tests --framework net10.0
dotnet sln add tests/Cortex.Web.Tests/Cortex.Web.Tests.csproj
dotnet add tests/Cortex.Web.Tests/Cortex.Web.Tests.csproj reference src/Cortex.Web/Cortex.Web.csproj
dotnet add tests/Cortex.Web.Tests/Cortex.Web.Tests.csproj reference src/Cortex.Core/Cortex.Core.csproj
dotnet add tests/Cortex.Web.Tests/Cortex.Web.Tests.csproj package NSubstitute
```

Remove any auto-generated test files (e.g. `UnitTest1.cs`, `GlobalUsings.cs` if generated).

**Step 3: Write the failing tests**

```csharp
using Cortex.Core.Email;
using Cortex.Core.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cortex.Web.Tests.Email;

public class MicrosoftGraphEmailProviderTests
{
    [Fact]
    public void HandleValidation_WithValidationToken_ReturnsToken()
    {
        var options = Options.Create(new EmailProviderOptions
        {
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret",
            RedirectUri = "https://localhost/callback"
        });

        var tokenStore = new InMemoryTokenStore();
        var provider = new MicrosoftGraphEmailProvider(options, tokenStore, NullLogger<MicrosoftGraphEmailProvider>.Instance);

        var headers = new Dictionary<string, string>();
        var payload = "{\"validationToken\": \"test-token-123\"}";

        var result = provider.HandleValidation(payload, headers);

        Assert.Equal("test-token-123", result);
    }

    [Fact]
    public void HandleValidation_WithoutValidationToken_ReturnsNull()
    {
        var options = Options.Create(new EmailProviderOptions
        {
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret",
            RedirectUri = "https://localhost/callback"
        });

        var tokenStore = new InMemoryTokenStore();
        var provider = new MicrosoftGraphEmailProvider(options, tokenStore, NullLogger<MicrosoftGraphEmailProvider>.Instance);

        var headers = new Dictionary<string, string>();
        var payload = "{\"value\": []}";

        var result = provider.HandleValidation(payload, headers);

        Assert.Null(result);
    }
}
```

**Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Web.Tests --filter "FullyQualifiedName~MicrosoftGraphEmailProviderTests" --verbosity normal`
Expected: FAIL — `MicrosoftGraphEmailProvider` does not exist.

**Step 5: Write the implementation**

`MicrosoftGraphEmailProvider.cs`:
```csharp
using System.Text.Json;
using Cortex.Core.Email;
using Cortex.Core.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace Cortex.Web.Email;

/// <summary>
/// Microsoft Graph API implementation of <see cref="IEmailProvider"/>.
/// </summary>
public sealed class MicrosoftGraphEmailProvider : IEmailProvider
{
    private readonly EmailProviderOptions _options;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<MicrosoftGraphEmailProvider> _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="MicrosoftGraphEmailProvider"/> class.
    /// </summary>
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
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("validationToken", out var tokenElement))
            {
                return tokenElement.GetString();
            }
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to parse webhook validation payload");
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailMessage>> ProcessWebhookAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var messages = new List<EmailMessage>();

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("value", out var notifications))
            {
                return messages;
            }

            foreach (var notification in notifications.EnumerateArray())
            {
                var resourceData = notification.GetProperty("resourceData");
                var messageId = resourceData.GetProperty("id").GetString();

                if (string.IsNullOrWhiteSpace(messageId))
                {
                    continue;
                }

                // Determine which user this notification is for
                var userId = "default";
                if (notification.TryGetProperty("tenantId", out _))
                {
                    userId = "default"; // Single-user for Phase 1
                }

                var client = await GetGraphClientAsync(userId, cancellationToken);
                if (client is null)
                {
                    _logger.LogWarning("No token available for user {UserId}, skipping notification", userId);
                    continue;
                }

                var graphMessage = await client.Me.Messages[messageId]
                    .GetAsync(cancellationToken: cancellationToken);

                if (graphMessage is null)
                {
                    continue;
                }

                var emailMessage = MapToEmailMessage(graphMessage);
                if (emailMessage is not null)
                {
                    messages.Add(emailMessage);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse webhook payload");
        }

        return messages;
    }

    /// <inheritdoc />
    public async Task SendAsync(OutboundEmail email, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var client = await GetGraphClientAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"No token available for user {userId}");

        var message = new Message
        {
            Subject = email.Subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = email.Body
            },
            ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = email.To } }],
            CcRecipients = email.Cc.Select(cc => new Recipient
            {
                EmailAddress = new EmailAddress { Address = cc }
            }).ToList()
        };

        await client.Me.SendMail.PostAsync(
            new Microsoft.Graph.Me.SendMail.SendMailPostRequestBody { Message = message },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Sent email to {To} with subject {Subject}", email.To, email.Subject);
    }

    /// <inheritdoc />
    public async Task<Stream> GetAttachmentAsync(
        string externalMessageId, string contentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);

        var client = await GetGraphClientAsync("default", cancellationToken)
            ?? throw new InvalidOperationException("No token available");

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
        string userId, string notificationUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationUrl);

        var client = await GetGraphClientAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException($"No token available for user {userId}");

        var subscription = new Subscription
        {
            ChangeType = "created",
            NotificationUrl = notificationUrl,
            Resource = "me/messages",
            ExpirationDateTime = DateTimeOffset.UtcNow.AddDays(2)
        };

        var created = await client.Subscriptions.PostAsync(subscription, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to create subscription");

        _logger.LogInformation(
            "Created Graph subscription {SubscriptionId} for user {UserId}, expires {ExpiresAt}",
            created.Id, userId, created.ExpirationDateTime);

        return new SubscriptionRecord
        {
            SubscriptionId = created.Id!,
            Provider = "microsoft",
            UserId = userId,
            ExpiresAt = created.ExpirationDateTime!.Value,
            Resource = "me/messages"
        };
    }

    /// <inheritdoc />
    public async Task<SubscriptionRecord> RenewSubscriptionAsync(
        string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        var client = await GetGraphClientAsync("default", cancellationToken)
            ?? throw new InvalidOperationException("No token available");

        var newExpiry = DateTimeOffset.UtcNow.AddDays(2);
        var update = new Subscription { ExpirationDateTime = newExpiry };

        var renewed = await client.Subscriptions[subscriptionId]
            .PatchAsync(update, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Failed to renew subscription {subscriptionId}");

        _logger.LogInformation(
            "Renewed Graph subscription {SubscriptionId}, new expiry {ExpiresAt}",
            subscriptionId, renewed.ExpirationDateTime);

        return new SubscriptionRecord
        {
            SubscriptionId = renewed.Id!,
            Provider = "microsoft",
            UserId = "default",
            ExpiresAt = renewed.ExpirationDateTime!.Value,
            Resource = "me/messages"
        };
    }

    /// <inheritdoc />
    public async Task DeleteSubscriptionAsync(
        string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);

        var client = await GetGraphClientAsync("default", cancellationToken)
            ?? throw new InvalidOperationException("No token available");

        await client.Subscriptions[subscriptionId]
            .DeleteAsync(cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted Graph subscription {SubscriptionId}", subscriptionId);
    }

    private async Task<GraphServiceClient?> GetGraphClientAsync(string userId, CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.GetAsync("microsoft", userId, cancellationToken);
        if (tokens is null)
        {
            return null;
        }

        // Check if token needs refresh
        if (tokens.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5))
        {
            tokens = await RefreshTokenAsync(userId, tokens, cancellationToken);
            if (tokens is null)
            {
                return null;
            }
        }

        var credential = new AccessTokenCredential(tokens.AccessToken);
        return new GraphServiceClient(credential);
    }

    private async Task<TokenSet?> RefreshTokenAsync(string userId, TokenSet tokens, CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new HttpClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["refresh_token"] = tokens.RefreshToken,
                ["grant_type"] = "refresh_token",
                ["scope"] = "https://graph.microsoft.com/.default"
            });

            var response = await httpClient.PostAsync(
                $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var newTokens = new TokenSet
            {
                AccessToken = doc.RootElement.GetProperty("access_token").GetString()!,
                RefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt)
                    ? rt.GetString()!
                    : tokens.RefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(
                    doc.RootElement.GetProperty("expires_in").GetInt32())
            };

            await _tokenStore.StoreAsync("microsoft", userId, newTokens, cancellationToken);

            _logger.LogInformation("Refreshed token for user {UserId}", userId);
            return newTokens;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token for user {UserId}", userId);
            return null;
        }
    }

    private static EmailMessage? MapToEmailMessage(Message graphMessage)
    {
        var from = graphMessage.From?.EmailAddress?.Address;
        if (string.IsNullOrWhiteSpace(from))
        {
            return null;
        }

        var to = graphMessage.ToRecipients?
            .Where(r => r.EmailAddress?.Address is not null)
            .Select(r => r.EmailAddress!.Address!)
            .ToList() ?? [];

        var cc = graphMessage.CcRecipients?
            .Where(r => r.EmailAddress?.Address is not null)
            .Select(r => r.EmailAddress!.Address!)
            .ToList() ?? [];

        var attachments = graphMessage.Attachments?
            .Where(a => a is FileAttachment)
            .Select(a => new EmailAttachment
            {
                FileName = a.Name ?? "unknown",
                ContentType = a.ContentType ?? "application/octet-stream",
                SizeBytes = a.Size ?? 0,
                ContentId = a.Id ?? ""
            })
            .ToList() ?? [];

        return new EmailMessage
        {
            ExternalId = graphMessage.Id!,
            From = from,
            To = to,
            Subject = graphMessage.Subject ?? "(no subject)",
            Body = graphMessage.Body?.Content ?? "",
            ThreadId = graphMessage.ConversationId,
            Cc = cc,
            Attachments = attachments,
            ReceivedAt = graphMessage.ReceivedDateTime ?? DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Simple access token credential for Graph SDK.
/// </summary>
internal sealed class AccessTokenCredential : Azure.Core.TokenCredential
{
    private readonly string _accessToken;

    public AccessTokenCredential(string accessToken) => _accessToken = accessToken;

    public override Azure.Core.AccessToken GetToken(Azure.Core.TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new(_accessToken, DateTimeOffset.UtcNow.AddHours(1));

    public override ValueTask<Azure.Core.AccessToken> GetTokenAsync(Azure.Core.TokenRequestContext requestContext, CancellationToken cancellationToken)
        => new(GetToken(requestContext, cancellationToken));
}
```

**Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Web.Tests --filter "FullyQualifiedName~MicrosoftGraphEmailProviderTests" --verbosity normal`
Expected: 2 passed.

**Step 7: Commit**

```bash
git add src/Cortex.Web/Cortex.Web.csproj src/Cortex.Web/Email/MicrosoftGraphEmailProvider.cs tests/Cortex.Web.Tests/ -A
git commit -m "feat(email): MicrosoftGraphEmailProvider with Graph SDK integration (#4, #5)"
```

---

### Task 9: Subscription Renewal Service

Create `SubscriptionRenewalService` as an `IHostedService` in `Cortex.Web.Email`, following the `DelegationSupervisionService` pattern.

**Files:**
- Create: `src/Cortex.Web/Email/SubscriptionRenewalOptions.cs`
- Create: `src/Cortex.Web/Email/SubscriptionRenewalService.cs`
- Create: `tests/Cortex.Web.Tests/Email/SubscriptionRenewalServiceTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Email;
using Cortex.Web.Email;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

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

        // After renewal, the subscription should no longer be expiring within 6 hours
        var expiring = await _subscriptionStore.GetExpiringAsync(TimeSpan.FromHours(6));
        Assert.Empty(expiring);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Web.Tests --filter "FullyQualifiedName~SubscriptionRenewalServiceTests" --verbosity normal`
Expected: FAIL — types do not exist.

**Step 3: Write the implementations**

`SubscriptionRenewalOptions.cs`:
```csharp
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
```

`SubscriptionRenewalService.cs`:
```csharp
using Cortex.Core.Email;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortex.Web.Email;

/// <summary>
/// Background service that automatically renews webhook subscriptions before they expire.
/// </summary>
public sealed class SubscriptionRenewalService : IHostedService, IDisposable
{
    private readonly IEmailProvider _emailProvider;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly ILogger<SubscriptionRenewalService> _logger;
    private readonly SubscriptionRenewalOptions _options;

    private PeriodicTimer? _timer;
    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Initialises a new instance of the <see cref="SubscriptionRenewalService"/> class.
    /// </summary>
    public SubscriptionRenewalService(
        IEmailProvider emailProvider,
        ISubscriptionStore subscriptionStore,
        ILogger<SubscriptionRenewalService> logger,
        SubscriptionRenewalOptions options)
    {
        ArgumentNullException.ThrowIfNull(emailProvider);
        ArgumentNullException.ThrowIfNull(subscriptionStore);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _emailProvider = emailProvider;
        _subscriptionStore = subscriptionStore;
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// Checks for expiring subscriptions and renews them. Public for testing.
    /// </summary>
    public async Task CheckAndRenewAsync(CancellationToken cancellationToken = default)
    {
        var expiring = await _subscriptionStore.GetExpiringAsync(_options.RenewalWindow, cancellationToken);
        if (expiring.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} expiring subscription(s) to renew", expiring.Count);

        foreach (var record in expiring)
        {
            try
            {
                var renewed = await _emailProvider.RenewSubscriptionAsync(
                    record.SubscriptionId, cancellationToken);

                await _subscriptionStore.UpdateExpiryAsync(
                    record.SubscriptionId, renewed.ExpiresAt, cancellationToken);

                _logger.LogInformation(
                    "Renewed subscription {SubscriptionId}, new expiry {ExpiresAt}",
                    record.SubscriptionId, renewed.ExpiresAt);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to renew subscription {SubscriptionId}",
                    record.SubscriptionId);
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Subscription renewal service starting with check interval {Interval}",
            _options.CheckInterval);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(_options.CheckInterval);
        _loopTask = RunLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Subscription renewal service stopping");

        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _logger.LogInformation("Subscription renewal service stopped");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (await _timer!.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await CheckAndRenewAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during subscription renewal check");
            }
        }
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Web.Tests --filter "FullyQualifiedName~SubscriptionRenewalServiceTests" --verbosity normal`
Expected: 4 passed.

**Step 5: Commit**

```bash
git add src/Cortex.Web/Email/SubscriptionRenewalOptions.cs src/Cortex.Web/Email/SubscriptionRenewalService.cs tests/Cortex.Web.Tests/Email/SubscriptionRenewalServiceTests.cs
git commit -m "feat(email): SubscriptionRenewalService background service (#4)"
```

---

### Task 10: Email Webhook Endpoints

Create the four email HTTP endpoints in `Cortex.Web` using minimal API style: connect, callback, webhook, disconnect.

**Files:**
- Create: `src/Cortex.Web/Email/EmailEndpoints.cs`
- Create: `tests/Cortex.Web.Tests/Email/EmailEndpointsTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Text;
using System.Text.Json;
using Cortex.Core.Email;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Web.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Cortex.Web.Tests.Email;

public class EmailWebhookHandlerTests
{
    private readonly IEmailProvider _emailProvider = Substitute.For<IEmailProvider>();
    private readonly InMemoryEmailDeduplicationStore _deduplicationStore = new();
    private readonly IMessagePublisher _messagePublisher = Substitute.For<IMessagePublisher>();
    private readonly IReferenceCodeGenerator _referenceCodeGenerator = Substitute.For<IReferenceCodeGenerator>();

    private EmailWebhookHandler CreateHandler()
    {
        _referenceCodeGenerator.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns(ReferenceCode.Create(DateTimeOffset.UtcNow, 1));

        return new EmailWebhookHandler(
            _emailProvider,
            _deduplicationStore,
            _messagePublisher,
            _referenceCodeGenerator,
            NullLogger<EmailWebhookHandler>.Instance);
    }

    [Fact]
    public async Task HandleWebhookAsync_ValidationRequest_ReturnsValidationToken()
    {
        _emailProvider.HandleValidation(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .Returns("test-token");

        var handler = CreateHandler();
        var (isValidation, validationToken, count) = await handler.HandleWebhookAsync(
            "{}", new Dictionary<string, string>());

        Assert.True(isValidation);
        Assert.Equal("test-token", validationToken);
    }

    [Fact]
    public async Task HandleWebhookAsync_NewEmail_PublishesToCosQueue()
    {
        _emailProvider.HandleValidation(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .Returns((string?)null);

        var email = new EmailMessage
        {
            ExternalId = "msg-001",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "Hello",
            Body = "Hi there",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _emailProvider.ProcessWebhookAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns([email]);

        var handler = CreateHandler();
        var (isValidation, _, count) = await handler.HandleWebhookAsync(
            "{}", new Dictionary<string, string>());

        Assert.False(isValidation);
        Assert.Equal(1, count);

        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<MessageEnvelope>(e => e.Message is EmailMessage),
            Arg.Is("agent.cos"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWebhookAsync_DuplicateEmail_SkipsIt()
    {
        _emailProvider.HandleValidation(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .Returns((string?)null);

        var email = new EmailMessage
        {
            ExternalId = "msg-001",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "Hello",
            Body = "Hi there",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _emailProvider.ProcessWebhookAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns([email]);

        await _deduplicationStore.MarkSeenAsync("msg-001");

        var handler = CreateHandler();
        var (_, _, count) = await handler.HandleWebhookAsync(
            "{}", new Dictionary<string, string>());

        Assert.Equal(0, count);

        await _messagePublisher.DidNotReceive().PublishAsync(
            Arg.Any<MessageEnvelope>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWebhookAsync_MultipleEmails_PublishesEach()
    {
        _emailProvider.HandleValidation(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .Returns((string?)null);

        var email1 = new EmailMessage
        {
            ExternalId = "msg-001",
            From = "alice@example.com",
            To = ["bob@example.com"],
            Subject = "First",
            Body = "First email",
            ReceivedAt = DateTimeOffset.UtcNow
        };
        var email2 = new EmailMessage
        {
            ExternalId = "msg-002",
            From = "carol@example.com",
            To = ["bob@example.com"],
            Subject = "Second",
            Body = "Second email",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        _emailProvider.ProcessWebhookAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns([email1, email2]);

        var handler = CreateHandler();
        var (_, _, count) = await handler.HandleWebhookAsync(
            "{}", new Dictionary<string, string>());

        Assert.Equal(2, count);

        await _messagePublisher.Received(2).PublishAsync(
            Arg.Any<MessageEnvelope>(),
            Arg.Is("agent.cos"),
            Arg.Any<CancellationToken>());
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Web.Tests --filter "FullyQualifiedName~EmailWebhookHandlerTests" --verbosity normal`
Expected: FAIL — `EmailWebhookHandler` does not exist.

**Step 3: Write the implementations**

`EmailEndpoints.cs`:
```csharp
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
    /// Initialises a new instance of the <see cref="EmailWebhookHandler"/> class.
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
        // Check for subscription validation
        var validationToken = _emailProvider.HandleValidation(payload, headers);
        if (validationToken is not null)
        {
            _logger.LogInformation("Handled webhook subscription validation");
            return (true, validationToken, 0);
        }

        // Process notifications
        var emails = await _emailProvider.ProcessWebhookAsync(payload, headers, cancellationToken);
        var publishedCount = 0;

        foreach (var email in emails)
        {
            if (await _deduplicationStore.ExistsAsync(email.ExternalId, cancellationToken))
            {
                _logger.LogDebug("Skipping duplicate email {ExternalId}", email.ExternalId);
                continue;
            }

            await _deduplicationStore.MarkSeenAsync(email.ExternalId, cancellationToken);

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

    private static IResult HandleConnect(
        IOptions<EmailProviderOptions> options)
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

        // Exchange authorization code for tokens
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

        // Create webhook subscription
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
        // Delete all expiring subscriptions (i.e. all active ones)
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
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Web.Tests --filter "FullyQualifiedName~EmailWebhookHandlerTests" --verbosity normal`
Expected: 4 passed.

**Step 5: Commit**

```bash
git add src/Cortex.Web/Email/EmailEndpoints.cs tests/Cortex.Web.Tests/Email/EmailEndpointsTests.cs
git commit -m "feat(email): webhook handler and email HTTP endpoints (#4)"
```

---

### Task 11: DI Wiring and Program.cs

Wire up all email services in `Cortex.Web/Program.cs` and add the endpoint mapping.

**Files:**
- Modify: `src/Cortex.Web/Program.cs`

**Step 1: Update Program.cs**

```csharp
using Cortex.Core.Email;
using Cortex.Web.Email;

var builder = WebApplication.CreateBuilder(args);

// Email provider configuration
builder.Services.Configure<EmailProviderOptions>(
    builder.Configuration.GetSection("Email"));

// Email services
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddSingleton<IEmailDeduplicationStore, InMemoryEmailDeduplicationStore>();
builder.Services.AddSingleton<IAttachmentStore, InMemoryAttachmentStore>();
builder.Services.AddSingleton<ISubscriptionStore, InMemorySubscriptionStore>();
builder.Services.AddSingleton<IEmailProvider, MicrosoftGraphEmailProvider>();
builder.Services.AddSingleton<EmailWebhookHandler>();

// Subscription renewal background service
builder.Services.AddSingleton(new SubscriptionRenewalOptions());
builder.Services.AddHostedService<SubscriptionRenewalService>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapEmailEndpoints();

app.Run();
```

**Step 2: Build to verify compilation**

Run: `dotnet build --configuration Release`
Expected: Build succeeded.

**Step 3: Run all tests**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass.

**Step 4: Commit**

```bash
git add src/Cortex.Web/Program.cs
git commit -m "feat(email): wire up email services and endpoints in DI (#4, #5)"
```
