namespace Cortex.Core.Email;

/// <summary>Tracks seen email identifiers to prevent duplicate processing.</summary>
public interface IEmailDeduplicationStore
{
    /// <summary>Returns true if the email identifier has already been seen.</summary>
    Task<bool> ExistsAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>Marks an email identifier as seen.</summary>
    Task MarkSeenAsync(string externalId, CancellationToken cancellationToken = default);
}
