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
