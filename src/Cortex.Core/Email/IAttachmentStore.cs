namespace Cortex.Core.Email;

/// <summary>Stores fetched attachment content for downstream processing.</summary>
public interface IAttachmentStore
{
    /// <summary>Stores attachment content and returns a storage identifier.</summary>
    Task<string> StoreAsync(string referenceCode, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Retrieves attachment content by storage identifier, or null if not found.</summary>
    Task<Stream?> GetAsync(string storageId, CancellationToken cancellationToken = default);

    /// <summary>Removes attachment content by storage identifier.</summary>
    Task RemoveAsync(string storageId, CancellationToken cancellationToken = default);
}
