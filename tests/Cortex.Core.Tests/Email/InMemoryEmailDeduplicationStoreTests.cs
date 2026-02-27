using Cortex.Core.Email;

namespace Cortex.Core.Tests.Email;

public sealed class InMemoryEmailDeduplicationStoreTests
{
    private readonly InMemoryEmailDeduplicationStore _store = new();

    [Fact]
    public async Task ExistsAsync_Unseen_ReturnsFalse()
    {
        var result = await _store.ExistsAsync("msg-123");

        Assert.False(result);
    }

    [Fact]
    public async Task MarkSeenAsync_ThenExistsAsync_ReturnsTrue()
    {
        await _store.MarkSeenAsync("msg-123");

        var result = await _store.ExistsAsync("msg-123");

        Assert.True(result);
    }

    [Fact]
    public async Task MarkSeenAsync_Idempotent_DoesNotThrow()
    {
        await _store.MarkSeenAsync("msg-123");
        await _store.MarkSeenAsync("msg-123");

        var result = await _store.ExistsAsync("msg-123");
        Assert.True(result);
    }

    [Fact]
    public async Task DifferentIds_AreIndependent()
    {
        await _store.MarkSeenAsync("msg-123");

        var seen = await _store.ExistsAsync("msg-123");
        var unseen = await _store.ExistsAsync("msg-456");

        Assert.True(seen);
        Assert.False(unseen);
    }
}
