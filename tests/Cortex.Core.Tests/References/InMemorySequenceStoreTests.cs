using Cortex.Core.References;

namespace Cortex.Core.Tests.References;

public sealed class InMemorySequenceStoreTests
{
    private readonly InMemorySequenceStore _store = new();

    [Fact]
    public async Task LoadAsync_WhenEmpty_ReturnsZeroedState()
    {
        var state = await _store.LoadAsync();

        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_ReturnsSavedState()
    {
        var saved = new SequenceState(new DateOnly(2026, 2, 24), 42);
        await _store.SaveAsync(saved);

        var loaded = await _store.LoadAsync();

        Assert.Equal(new DateOnly(2026, 2, 24), loaded.Date);
        Assert.Equal(42, loaded.Sequence);
    }

    [Fact]
    public async Task SaveAsync_OverwritesPreviousState()
    {
        await _store.SaveAsync(new SequenceState(new DateOnly(2026, 2, 24), 1));
        await _store.SaveAsync(new SequenceState(new DateOnly(2026, 2, 24), 2));

        var loaded = await _store.LoadAsync();

        Assert.Equal(2, loaded.Sequence);
    }
}
