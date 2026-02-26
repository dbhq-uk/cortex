using Cortex.Core.Context;
using Cortex.Core.References;

namespace Cortex.Core.Tests.Context;

public sealed class FileContextProviderTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileContextProvider _provider;

    public FileContextProviderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"cortex-ctx-{Guid.NewGuid():N}");
        _provider = new FileContextProvider(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private static ContextEntry CreateEntry(
        string entryId = "entry-1",
        string content = "test content",
        ContextCategory category = ContextCategory.CustomerNote,
        IReadOnlyList<string>? tags = null,
        ReferenceCode? referenceCode = null) =>
        new()
        {
            EntryId = entryId,
            Content = content,
            Category = category,
            Tags = tags ?? [],
            ReferenceCode = referenceCode,
            CreatedAt = new DateTimeOffset(2026, 2, 26, 10, 0, 0, TimeSpan.Zero)
        };

    [Fact]
    public async Task StoreAsync_CreatesMarkdownFile()
    {
        await _provider.StoreAsync(CreateEntry());

        var files = Directory.GetFiles(_testDir, "*.md");
        Assert.Single(files);
    }

    [Fact]
    public async Task StoreAsync_CreatesDirectoryIfMissing()
    {
        Assert.False(Directory.Exists(_testDir));

        await _provider.StoreAsync(CreateEntry());

        Assert.True(Directory.Exists(_testDir));
    }

    [Fact]
    public async Task StoreAsync_NullEntry_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _provider.StoreAsync(null!));
    }

    [Fact]
    public async Task StoreAsync_ThenQuery_RoundTripsAllFields()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 42);
        var entry = CreateEntry(
            content: "Customer prefers monthly billing",
            category: ContextCategory.Decision,
            tags: ["billing", "smith"],
            referenceCode: refCode);

        await _provider.StoreAsync(entry);
        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Single(results);
        var result = results[0];
        Assert.Equal("entry-1", result.EntryId);
        Assert.Equal("Customer prefers monthly billing", result.Content);
        Assert.Equal(ContextCategory.Decision, result.Category);
        Assert.Equal(["billing", "smith"], result.Tags);
        Assert.Equal(refCode, result.ReferenceCode);
    }

    [Fact]
    public async Task QueryAsync_MissingDirectory_ReturnsEmpty()
    {
        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_KeywordsFilter_Works()
    {
        await _provider.StoreAsync(CreateEntry(content: "monthly billing info"));
        await _provider.StoreAsync(CreateEntry(
            entryId: "entry-2", content: "unrelated topic"));

        var results = await _provider.QueryAsync(
            new ContextQuery { Keywords = "billing" });

        Assert.Single(results);
        Assert.Contains("billing", results[0].Content);
    }

    [Fact]
    public async Task QueryAsync_CategoryFilter_Works()
    {
        await _provider.StoreAsync(CreateEntry(
            category: ContextCategory.CustomerNote));
        await _provider.StoreAsync(CreateEntry(
            entryId: "entry-2", category: ContextCategory.Lesson));

        var results = await _provider.QueryAsync(
            new ContextQuery { Category = ContextCategory.Lesson });

        Assert.Single(results);
        Assert.Equal(ContextCategory.Lesson, results[0].Category);
    }

    [Fact]
    public async Task QueryAsync_TagsFilter_AnyOverlap()
    {
        await _provider.StoreAsync(CreateEntry(tags: ["pricing", "smith"]));
        await _provider.StoreAsync(CreateEntry(
            entryId: "entry-2", tags: ["internal"]));

        var results = await _provider.QueryAsync(
            new ContextQuery { Tags = ["smith"] });

        Assert.Single(results);
    }

    [Fact]
    public async Task QueryAsync_ReferenceCodeFilter_Works()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 7);
        await _provider.StoreAsync(CreateEntry(referenceCode: refCode));
        await _provider.StoreAsync(CreateEntry(entryId: "entry-2"));

        var results = await _provider.QueryAsync(
            new ContextQuery { ReferenceCode = refCode });

        Assert.Single(results);
    }

    [Fact]
    public async Task StoreAsync_OverwritesExistingEntry()
    {
        await _provider.StoreAsync(CreateEntry(content: "original"));
        await _provider.StoreAsync(CreateEntry(content: "updated"));

        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Single(results);
        Assert.Equal("updated", results[0].Content);
    }

    [Fact]
    public async Task QueryAsync_EntryWithNoTags_RoundTrips()
    {
        await _provider.StoreAsync(CreateEntry(tags: []));

        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Single(results);
        Assert.Empty(results[0].Tags);
    }

    [Fact]
    public async Task QueryAsync_EntryWithNoReferenceCode_RoundTrips()
    {
        await _provider.StoreAsync(CreateEntry());

        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Single(results);
        Assert.Null(results[0].ReferenceCode);
    }
}
