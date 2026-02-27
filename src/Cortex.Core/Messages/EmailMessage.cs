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
