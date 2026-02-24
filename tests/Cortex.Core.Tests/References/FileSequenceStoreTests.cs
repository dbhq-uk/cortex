using System.Text.Json;
using Cortex.Core.References;
using Microsoft.Extensions.Options;

namespace Cortex.Core.Tests.References;

public sealed class FileSequenceStoreTests : IDisposable
{
    private readonly string _tempFile;
    private readonly FileSequenceStore _store;

    public FileSequenceStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"cortex-test-{Guid.NewGuid()}.json");
        var options = Options.Create(new FileSequenceStoreOptions { FilePath = _tempFile });
        _store = new FileSequenceStore(options);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_NoFile_ReturnsZeroedState()
    {
        var state = await _store.LoadAsync();

        Assert.Equal(DateOnly.MinValue, state.Date);
        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTrips()
    {
        var saved = new SequenceState(new DateOnly(2026, 2, 24), 42);
        await _store.SaveAsync(saved);

        var loaded = await _store.LoadAsync();

        Assert.Equal(new DateOnly(2026, 2, 24), loaded.Date);
        Assert.Equal(42, loaded.Sequence);
    }

    [Fact]
    public async Task SaveAsync_CreatesFile()
    {
        await _store.SaveAsync(new SequenceState(new DateOnly(2026, 2, 24), 1));

        Assert.True(File.Exists(_tempFile));
    }

    [Fact]
    public async Task SaveAsync_WritesValidJson()
    {
        await _store.SaveAsync(new SequenceState(new DateOnly(2026, 2, 24), 7));

        var json = await File.ReadAllTextAsync(_tempFile);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("2026-02-24", doc.RootElement.GetProperty("date").GetString());
        Assert.Equal(7, doc.RootElement.GetProperty("sequence").GetInt32());
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsZeroedState()
    {
        await File.WriteAllTextAsync(_tempFile, "not json at all");

        var state = await _store.LoadAsync();

        Assert.Equal(0, state.Sequence);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), $"cortex-test-{Guid.NewGuid()}", "seq.json");
        var options = Options.Create(new FileSequenceStoreOptions { FilePath = nestedPath });
        var store = new FileSequenceStore(options);

        await store.SaveAsync(new SequenceState(new DateOnly(2026, 2, 24), 1));

        Assert.True(File.Exists(nestedPath));

        // Cleanup
        Directory.Delete(Path.GetDirectoryName(nestedPath)!, true);
    }
}
