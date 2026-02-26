using Cortex.Core.Context;
using Cortex.Core.References;

namespace Cortex.Core.Tests.Context;

public sealed class InMemoryContextProviderTests
{
    private readonly InMemoryContextProvider _provider = new();

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
            CreatedAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task StoreAsync_ThenQuery_ReturnsEntry()
    {
        var entry = CreateEntry();
        await _provider.StoreAsync(entry);

        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Single(results);
        Assert.Equal("entry-1", results[0].EntryId);
    }

    [Fact]
    public async Task StoreAsync_NullEntry_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _provider.StoreAsync(null!));
    }

    [Fact]
    public async Task QueryAsync_EmptyStore_ReturnsEmpty()
    {
        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_KeywordsFilter_MatchesCaseInsensitive()
    {
        await _provider.StoreAsync(CreateEntry(content: "Customer prefers monthly billing"));
        await _provider.StoreAsync(CreateEntry(entryId: "entry-2", content: "Internal meeting notes"));

        var results = await _provider.QueryAsync(new ContextQuery { Keywords = "MONTHLY" });

        Assert.Single(results);
        Assert.Contains("monthly", results[0].Content);
    }

    [Fact]
    public async Task QueryAsync_KeywordsFilter_PartialMatch()
    {
        await _provider.StoreAsync(CreateEntry(content: "Customer prefers monthly billing"));

        var results = await _provider.QueryAsync(new ContextQuery { Keywords = "month" });

        Assert.Single(results);
    }

    [Fact]
    public async Task QueryAsync_CategoryFilter_ExactMatch()
    {
        await _provider.StoreAsync(CreateEntry(category: ContextCategory.CustomerNote));
        await _provider.StoreAsync(CreateEntry(
            entryId: "entry-2", category: ContextCategory.MeetingNote));

        var results = await _provider.QueryAsync(
            new ContextQuery { Category = ContextCategory.CustomerNote });

        Assert.Single(results);
        Assert.Equal(ContextCategory.CustomerNote, results[0].Category);
    }

    [Fact]
    public async Task QueryAsync_TagsFilter_AnyOverlap()
    {
        await _provider.StoreAsync(CreateEntry(tags: ["pricing", "smith"]));
        await _provider.StoreAsync(CreateEntry(
            entryId: "entry-2", tags: ["internal", "ops"]));

        var results = await _provider.QueryAsync(
            new ContextQuery { Tags = ["smith", "unrelated"] });

        Assert.Single(results);
        Assert.Equal("entry-1", results[0].EntryId);
    }

    [Fact]
    public async Task QueryAsync_ReferenceCodeFilter_ExactMatch()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        await _provider.StoreAsync(CreateEntry(referenceCode: refCode));
        await _provider.StoreAsync(CreateEntry(entryId: "entry-2"));

        var results = await _provider.QueryAsync(
            new ContextQuery { ReferenceCode = refCode });

        Assert.Single(results);
        Assert.Equal(refCode, results[0].ReferenceCode);
    }

    [Fact]
    public async Task QueryAsync_CombinedFilters_AndSemantics()
    {
        await _provider.StoreAsync(CreateEntry(
            content: "billing update",
            category: ContextCategory.CustomerNote,
            tags: ["billing"]));
        await _provider.StoreAsync(CreateEntry(
            entryId: "entry-2",
            content: "billing policy",
            category: ContextCategory.Decision,
            tags: ["billing"]));

        var results = await _provider.QueryAsync(new ContextQuery
        {
            Keywords = "billing",
            Category = ContextCategory.CustomerNote
        });

        Assert.Single(results);
        Assert.Equal("entry-1", results[0].EntryId);
    }

    [Fact]
    public async Task QueryAsync_MaxResults_LimitsOutput()
    {
        for (var i = 0; i < 5; i++)
        {
            await _provider.StoreAsync(CreateEntry(entryId: $"entry-{i}"));
        }

        var results = await _provider.QueryAsync(new ContextQuery { MaxResults = 2 });

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task QueryAsync_ResultsOrderedByCreatedAtDescending()
    {
        var older = CreateEntry(entryId: "old") with
        {
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1)
        };
        var newer = CreateEntry(entryId: "new") with
        {
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _provider.StoreAsync(older);
        await _provider.StoreAsync(newer);

        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Equal("new", results[0].EntryId);
        Assert.Equal("old", results[1].EntryId);
    }

    [Fact]
    public async Task StoreAsync_DuplicateEntryId_OverwritesEntry()
    {
        await _provider.StoreAsync(CreateEntry(content: "original"));
        await _provider.StoreAsync(CreateEntry(content: "updated"));

        var results = await _provider.QueryAsync(new ContextQuery());

        Assert.Single(results);
        Assert.Equal("updated", results[0].Content);
    }

    [Fact]
    public async Task QueryAsync_NoMatchingKeywords_ReturnsEmpty()
    {
        await _provider.StoreAsync(CreateEntry(content: "something else entirely"));

        var results = await _provider.QueryAsync(
            new ContextQuery { Keywords = "nonexistent" });

        Assert.Empty(results);
    }
}
