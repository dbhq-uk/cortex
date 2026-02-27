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
