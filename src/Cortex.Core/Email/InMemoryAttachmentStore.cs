using System.Collections.Concurrent;

namespace Cortex.Core.Email;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IAttachmentStore"/>
/// for unit testing and local development.
/// </summary>
public sealed class InMemoryAttachmentStore : IAttachmentStore
{
    private readonly ConcurrentDictionary<string, byte[]> _attachments = new();

    /// <inheritdoc />
    public async Task<string> StoreAsync(string referenceCode, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken);
        var data = memoryStream.ToArray();

        var storageId = $"{referenceCode}/{Guid.NewGuid():N}";
        _attachments[storageId] = data;

        return storageId;
    }

    /// <inheritdoc />
    public Task<Stream?> GetAsync(string storageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageId);

        if (_attachments.TryGetValue(storageId, out var data))
        {
            return Task.FromResult<Stream?>(new MemoryStream(data));
        }

        return Task.FromResult<Stream?>(null);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string storageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageId);

        _attachments.TryRemove(storageId, out _);
        return Task.CompletedTask;
    }
}
