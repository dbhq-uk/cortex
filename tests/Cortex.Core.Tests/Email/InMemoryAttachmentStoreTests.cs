using System.Text;
using Cortex.Core.Email;

namespace Cortex.Core.Tests.Email;

public sealed class InMemoryAttachmentStoreTests
{
    private readonly InMemoryAttachmentStore _store = new();

    [Fact]
    public async Task StoreAsync_ThenGetAsync_ReturnsContent()
    {
        var content = "Hello, attachment!"u8.ToArray();
        using var inputStream = new MemoryStream(content);

        var storageId = await _store.StoreAsync("CTX-2026-0227-001", "report.pdf", inputStream, "application/pdf");

        var result = await _store.GetAsync(storageId);

        Assert.NotNull(result);
        using var reader = new StreamReader(result);
        var text = await reader.ReadToEndAsync();
        Assert.Equal("Hello, attachment!", text);
    }

    [Fact]
    public async Task GetAsync_Nonexistent_ReturnsNull()
    {
        var result = await _store.GetAsync("nonexistent-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_ThenGetAsync_ReturnsNull()
    {
        var content = "test content"u8.ToArray();
        using var inputStream = new MemoryStream(content);

        var storageId = await _store.StoreAsync("CTX-2026-0227-001", "file.txt", inputStream, "text/plain");

        await _store.RemoveAsync(storageId);

        var result = await _store.GetAsync(storageId);
        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_Nonexistent_DoesNotThrow()
    {
        await _store.RemoveAsync("nonexistent-id");
    }

    [Fact]
    public async Task StoreAsync_MultipleFiles_UniqueStorageIds()
    {
        var content1 = "file one"u8.ToArray();
        var content2 = "file two"u8.ToArray();
        using var stream1 = new MemoryStream(content1);
        using var stream2 = new MemoryStream(content2);

        var id1 = await _store.StoreAsync("CTX-2026-0227-001", "a.txt", stream1, "text/plain");
        var id2 = await _store.StoreAsync("CTX-2026-0227-001", "b.txt", stream2, "text/plain");

        Assert.NotEqual(id1, id2);
    }
}
