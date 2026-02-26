# IContextProvider Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a business context layer (`IContextProvider`) that the CoS and future orchestrator agents can query when composing prompts, enabling context-aware triage and routing.

**Architecture:** New `Cortex.Core.Context` namespace with `IContextProvider` interface, `ContextEntry`/`ContextQuery` records, and `ContextCategory` enum. Two implementations: `InMemoryContextProvider` for testing and `FileContextProvider` for persistent markdown storage. `SkillDrivenAgent` gains an optional `IContextProvider` dependency to inject business context into the skill pipeline.

**Tech Stack:** C# / .NET 10, xUnit, file I/O for `FileContextProvider`

---

### Task 1: ContextCategory Enum and ContextEntry Record

**Files:**
- Create: `src/Cortex.Core/Context/ContextCategory.cs`
- Create: `src/Cortex.Core/Context/ContextEntry.cs`

**Step 1: Create ContextCategory enum**

```csharp
namespace Cortex.Core.Context;

/// <summary>
/// Classification categories for business context entries.
/// </summary>
public enum ContextCategory
{
    /// <summary>Notes about a specific customer or client.</summary>
    CustomerNote,

    /// <summary>Notes from meetings or discussions.</summary>
    MeetingNote,

    /// <summary>Recorded decisions and their rationale.</summary>
    Decision,

    /// <summary>Lessons learned from past work.</summary>
    Lesson,

    /// <summary>Preferences for how work should be done.</summary>
    Preference,

    /// <summary>High-level strategic context.</summary>
    Strategic,

    /// <summary>Day-to-day operational context.</summary>
    Operational
}
```

**Step 2: Create ContextEntry record**

```csharp
using Cortex.Core.References;

namespace Cortex.Core.Context;

/// <summary>
/// A single business context entry — a piece of accumulated wisdom
/// (customer history, meeting note, decision, lesson learned) that
/// enriches orchestration and triage.
/// </summary>
public sealed record ContextEntry
{
    /// <summary>Unique identifier for this entry.</summary>
    public required string EntryId { get; init; }

    /// <summary>The context text content.</summary>
    public required string Content { get; init; }

    /// <summary>Classification category.</summary>
    public required ContextCategory Category { get; init; }

    /// <summary>Searchable tags for this entry.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Optional link to a message thread reference code.</summary>
    public ReferenceCode? ReferenceCode { get; init; }

    /// <summary>When this entry was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
```

**Step 3: Build to verify no compilation errors**

Run: `dotnet build src/Cortex.Core/Cortex.Core.csproj --configuration Release`
Expected: Build succeeded, 0 errors

**Step 4: Commit**

```bash
git add src/Cortex.Core/Context/ContextCategory.cs src/Cortex.Core/Context/ContextEntry.cs
git commit -m "feat(context): add ContextCategory enum and ContextEntry record (#25)"
```

---

### Task 2: ContextQuery Record and IContextProvider Interface

**Files:**
- Create: `src/Cortex.Core/Context/ContextQuery.cs`
- Create: `src/Cortex.Core/Context/IContextProvider.cs`

**Step 1: Create ContextQuery record**

```csharp
using Cortex.Core.References;

namespace Cortex.Core.Context;

/// <summary>
/// Structured query for searching business context entries.
/// All filters combine with AND semantics. Null/empty filters are ignored.
/// </summary>
public sealed record ContextQuery
{
    /// <summary>Case-insensitive substring match on entry content.</summary>
    public string? Keywords { get; init; }

    /// <summary>Exact category filter.</summary>
    public ContextCategory? Category { get; init; }

    /// <summary>Tag overlap filter — matches entries that have at least one of these tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Exact reference code filter.</summary>
    public ReferenceCode? ReferenceCode { get; init; }

    /// <summary>Maximum number of results to return. Null means no limit.</summary>
    public int? MaxResults { get; init; }
}
```

**Step 2: Create IContextProvider interface**

```csharp
namespace Cortex.Core.Context;

/// <summary>
/// Provides access to business context — the accumulated wisdom store
/// (customer history, meeting notes, past decisions, lessons learned)
/// that enriches orchestration and triage.
/// </summary>
public interface IContextProvider
{
    /// <summary>
    /// Queries the context store using structured filters.
    /// </summary>
    /// <param name="query">The query filters to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching context entries, ordered by creation date descending.</returns>
    Task<IReadOnlyList<ContextEntry>> QueryAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new context entry.
    /// </summary>
    /// <param name="entry">The entry to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StoreAsync(
        ContextEntry entry,
        CancellationToken cancellationToken = default);
}
```

**Step 3: Build to verify**

Run: `dotnet build src/Cortex.Core/Cortex.Core.csproj --configuration Release`
Expected: Build succeeded, 0 errors

**Step 4: Commit**

```bash
git add src/Cortex.Core/Context/ContextQuery.cs src/Cortex.Core/Context/IContextProvider.cs
git commit -m "feat(context): add ContextQuery record and IContextProvider interface (#25)"
```

---

### Task 3: InMemoryContextProvider — Failing Tests

**Files:**
- Create: `tests/Cortex.Core.Tests/Context/InMemoryContextProviderTests.cs`

**Step 1: Write all failing tests**

These tests define the contract for InMemoryContextProvider. They will all fail because the class doesn't exist yet.

```csharp
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests/ --configuration Release --filter "FullyQualifiedName~InMemoryContextProviderTests" --verbosity normal`
Expected: Build FAIL — `InMemoryContextProvider` type not found

**Step 3: Commit failing tests**

```bash
git add tests/Cortex.Core.Tests/Context/InMemoryContextProviderTests.cs
git commit -m "test(context): add InMemoryContextProvider failing tests (#25)"
```

---

### Task 4: InMemoryContextProvider — Implementation

**Files:**
- Create: `src/Cortex.Core/Context/InMemoryContextProvider.cs`

**Step 1: Implement InMemoryContextProvider**

```csharp
using System.Collections.Concurrent;

namespace Cortex.Core.Context;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IContextProvider"/>
/// for unit testing and local development.
/// </summary>
public sealed class InMemoryContextProvider : IContextProvider
{
    private readonly ConcurrentDictionary<string, ContextEntry> _entries = new();

    /// <inheritdoc />
    public Task StoreAsync(ContextEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.EntryId] = entry;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ContextEntry>> QueryAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = _entries.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(query.Keywords))
        {
            results = results.Where(e =>
                e.Content.Contains(query.Keywords, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Category.HasValue)
        {
            results = results.Where(e => e.Category == query.Category.Value);
        }

        if (query.Tags is { Count: > 0 })
        {
            results = results.Where(e =>
                e.Tags.Any(t => query.Tags.Contains(t)));
        }

        if (query.ReferenceCode.HasValue)
        {
            results = results.Where(e =>
                e.ReferenceCode.HasValue && e.ReferenceCode.Value == query.ReferenceCode.Value);
        }

        var ordered = results.OrderByDescending(e => e.CreatedAt);

        IReadOnlyList<ContextEntry> list = query.MaxResults.HasValue
            ? ordered.Take(query.MaxResults.Value).ToList()
            : ordered.ToList();

        return Task.FromResult(list);
    }
}
```

**Step 2: Run tests to verify they all pass**

Run: `dotnet test tests/Cortex.Core.Tests/ --configuration Release --filter "FullyQualifiedName~InMemoryContextProviderTests" --verbosity normal`
Expected: All 13 tests PASS

**Step 3: Run full Core test suite to check for regressions**

Run: `dotnet test tests/Cortex.Core.Tests/ --configuration Release --verbosity normal`
Expected: All tests PASS

**Step 4: Commit**

```bash
git add src/Cortex.Core/Context/InMemoryContextProvider.cs
git commit -m "feat(context): implement InMemoryContextProvider (#25)"
```

---

### Task 5: FileContextProvider — Failing Tests

**Files:**
- Create: `tests/Cortex.Core.Tests/Context/FileContextProviderTests.cs`

**Step 1: Write failing tests**

Note: Tests use a temporary directory that is cleaned up via `IDisposable`. Test the YAML front matter format, round-trip, query filtering, and edge cases.

```csharp
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests/ --configuration Release --filter "FullyQualifiedName~FileContextProviderTests" --verbosity normal`
Expected: Build FAIL — `FileContextProvider` type not found

**Step 3: Commit failing tests**

```bash
git add tests/Cortex.Core.Tests/Context/FileContextProviderTests.cs
git commit -m "test(context): add FileContextProvider failing tests (#25)"
```

---

### Task 6: FileContextProvider — Implementation

**Files:**
- Create: `src/Cortex.Core/Context/FileContextProvider.cs`

**Step 1: Implement FileContextProvider**

The provider stores one markdown file per entry with YAML-style front matter. Parsing uses simple string operations — no YAML library dependency needed.

```csharp
using System.Globalization;
using System.Text;
using Cortex.Core.References;

namespace Cortex.Core.Context;

/// <summary>
/// File-based implementation of <see cref="IContextProvider"/> that stores
/// context entries as markdown files with YAML front matter in a directory.
/// </summary>
public sealed class FileContextProvider : IContextProvider
{
    private readonly string _directory;

    /// <summary>
    /// Creates a new FileContextProvider that reads/writes to the specified directory.
    /// </summary>
    /// <param name="directory">The directory path for context files.</param>
    public FileContextProvider(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <inheritdoc />
    public async Task StoreAsync(ContextEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Directory.CreateDirectory(_directory);

        var fileName = $"{entry.EntryId}.md";
        var filePath = Path.Combine(_directory, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"entryId: {entry.EntryId}");
        sb.AppendLine($"category: {entry.Category}");

        if (entry.Tags.Count > 0)
        {
            sb.AppendLine($"tags: [{string.Join(", ", entry.Tags)}]");
        }

        if (entry.ReferenceCode.HasValue)
        {
            sb.AppendLine($"referenceCode: {entry.ReferenceCode.Value}");
        }

        sb.AppendLine($"createdAt: {entry.CreatedAt:O}");
        sb.AppendLine("---");
        sb.Append(entry.Content);

        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextEntry>> QueryAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var files = Directory.GetFiles(_directory, "*.md");
        var entries = new List<ContextEntry>();

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var entry = ParseEntry(content);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        var results = entries.AsEnumerable();

        if (!string.IsNullOrEmpty(query.Keywords))
        {
            results = results.Where(e =>
                e.Content.Contains(query.Keywords, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Category.HasValue)
        {
            results = results.Where(e => e.Category == query.Category.Value);
        }

        if (query.Tags is { Count: > 0 })
        {
            results = results.Where(e =>
                e.Tags.Any(t => query.Tags.Contains(t)));
        }

        if (query.ReferenceCode.HasValue)
        {
            results = results.Where(e =>
                e.ReferenceCode.HasValue && e.ReferenceCode.Value == query.ReferenceCode.Value);
        }

        var ordered = results.OrderByDescending(e => e.CreatedAt);

        return query.MaxResults.HasValue
            ? ordered.Take(query.MaxResults.Value).ToList()
            : ordered.ToList();
    }

    private static ContextEntry? ParseEntry(string fileContent)
    {
        var frontMatterEnd = fileContent.IndexOf("---", 3, StringComparison.Ordinal);
        if (frontMatterEnd < 0)
        {
            return null;
        }

        var frontMatter = fileContent[3..frontMatterEnd].Trim();
        var body = fileContent[(frontMatterEnd + 3)..].TrimStart('\r', '\n');

        string? entryId = null;
        var category = ContextCategory.Operational;
        var tags = new List<string>();
        ReferenceCode? referenceCode = null;
        var createdAt = DateTimeOffset.UtcNow;

        foreach (var line in frontMatter.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex < 0) continue;

            var key = trimmed[..colonIndex].Trim();
            var value = trimmed[(colonIndex + 1)..].Trim();

            switch (key)
            {
                case "entryId":
                    entryId = value;
                    break;
                case "category":
                    if (Enum.TryParse<ContextCategory>(value, out var cat))
                    {
                        category = cat;
                    }
                    break;
                case "tags":
                    var tagContent = value.TrimStart('[').TrimEnd(']');
                    if (!string.IsNullOrWhiteSpace(tagContent))
                    {
                        tags.AddRange(tagContent.Split(',',
                            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    }
                    break;
                case "referenceCode":
                    referenceCode = new ReferenceCode(value);
                    break;
                case "createdAt":
                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var dt))
                    {
                        createdAt = dt;
                    }
                    break;
            }
        }

        if (entryId is null) return null;

        return new ContextEntry
        {
            EntryId = entryId,
            Content = body,
            Category = category,
            Tags = tags,
            ReferenceCode = referenceCode,
            CreatedAt = createdAt
        };
    }
}
```

**Step 2: Run FileContextProvider tests**

Run: `dotnet test tests/Cortex.Core.Tests/ --configuration Release --filter "FullyQualifiedName~FileContextProviderTests" --verbosity normal`
Expected: All 12 tests PASS

**Step 3: Run full Core test suite**

Run: `dotnet test tests/Cortex.Core.Tests/ --configuration Release --verbosity normal`
Expected: All tests PASS (existing + new)

**Step 4: Commit**

```bash
git add src/Cortex.Core/Context/FileContextProvider.cs
git commit -m "feat(context): implement FileContextProvider with markdown storage (#25)"
```

---

### Task 7: Wire IContextProvider into SkillDrivenAgent

**Files:**
- Modify: `src/Cortex.Agents/SkillDrivenAgent.cs` (constructor lines 32-55, ProcessAsync lines 79-91)
- Create: `tests/Cortex.Agents.Tests/SkillDrivenAgentContextTests.cs`

**Step 1: Write failing test**

Add a new test file specifically for context integration. This tests that when an `IContextProvider` is supplied, its results appear in the pipeline parameters as `businessContext`.

```csharp
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Core.Authority;
using Cortex.Core.Context;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class SkillDrivenAgentContextTests
{
    [Fact]
    public async Task ProcessAsync_WithContextProvider_InjectsBusinessContext()
    {
        // Arrange — store a context entry
        var contextProvider = new InMemoryContextProvider();
        await contextProvider.StoreAsync(new ContextEntry
        {
            EntryId = "ctx-1",
            Content = "Customer Smith prefers monthly billing",
            Category = ContextCategory.CustomerNote,
            Tags = ["smith"],
            CreatedAt = DateTimeOffset.UtcNow
        });

        // Capture parameters passed to pipeline
        IDictionary<string, object>? capturedParams = null;
        var skillRegistry = new InMemorySkillRegistry();
        await skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "Triage",
            Description = "Test triage",
            Category = SkillCategory.Agent,
            ExecutorType = "test"
        });

        var executors = new List<ISkillExecutor>
        {
            new CapturingSkillExecutor(p => capturedParams = p)
        };

        var pipelineRunner = new SkillPipelineRunner(skillRegistry, executors);
        var agentRegistry = new InMemoryAgentRegistry();
        var delegationTracker = new InMemoryDelegationTracker();
        var refCodeGen = new SequentialReferenceCodeGenerator(new InMemorySequenceStore());
        var publisher = new NullMessagePublisher();

        var persona = new PersonaDefinition
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = "triage", Description = "triage" }],
            Pipeline = ["cos-triage"],
            EscalationTarget = "agent.founder"
        };

        var agent = new SkillDrivenAgent(
            persona,
            pipelineRunner,
            agentRegistry,
            delegationTracker,
            refCodeGen,
            publisher,
            NullLogger<SkillDrivenAgent>.Instance,
            contextProvider);

        var envelope = new MessageEnvelope
        {
            Message = new TestMessage { Content = "billing question from Smith" },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = "reply-queue" }
        };

        // Act
        await agent.ProcessAsync(envelope);

        // Assert — businessContext parameter should contain the matching entry
        Assert.NotNull(capturedParams);
        Assert.True(capturedParams.ContainsKey("businessContext"));
        var contextValue = capturedParams["businessContext"] as string;
        Assert.NotNull(contextValue);
        Assert.Contains("Smith", contextValue);
    }

    [Fact]
    public async Task ProcessAsync_WithoutContextProvider_StillWorks()
    {
        // This verifies backward compatibility — IContextProvider is optional
        var skillRegistry = new InMemorySkillRegistry();
        await skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "Triage",
            Description = "Test triage",
            Category = SkillCategory.Agent,
            ExecutorType = "test"
        });

        IDictionary<string, object>? capturedParams = null;
        var executors = new List<ISkillExecutor>
        {
            new CapturingSkillExecutor(p => capturedParams = p)
        };

        var pipelineRunner = new SkillPipelineRunner(skillRegistry, executors);
        var agentRegistry = new InMemoryAgentRegistry();
        var delegationTracker = new InMemoryDelegationTracker();
        var refCodeGen = new SequentialReferenceCodeGenerator(new InMemorySequenceStore());
        var publisher = new NullMessagePublisher();

        var persona = new PersonaDefinition
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = "triage", Description = "triage" }],
            Pipeline = ["cos-triage"],
            EscalationTarget = "agent.founder"
        };

        var agent = new SkillDrivenAgent(
            persona,
            pipelineRunner,
            agentRegistry,
            delegationTracker,
            refCodeGen,
            publisher,
            NullLogger<SkillDrivenAgent>.Instance);

        var envelope = new MessageEnvelope
        {
            Message = new TestMessage { Content = "test message" },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = "reply-queue" }
        };

        // Act — should not throw
        await agent.ProcessAsync(envelope);

        // Assert — no businessContext key when no provider
        Assert.NotNull(capturedParams);
        Assert.False(capturedParams.ContainsKey("businessContext"));
    }
}

/// <summary>
/// Test executor that captures parameters for assertion.
/// Returns a triage result that causes escalation (low confidence).
/// </summary>
internal sealed class CapturingSkillExecutor : ISkillExecutor
{
    private readonly Action<IDictionary<string, object>> _capture;

    public CapturingSkillExecutor(Action<IDictionary<string, object>> capture)
    {
        _capture = capture;
    }

    public string ExecutorType => "test";

    public Task<object?> ExecuteAsync(
        SkillDefinition skill,
        IDictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        _capture(parameters);
        // Return a low-confidence triage result to trigger escalation (simplest path)
        var json = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            capability = "triage",
            authorityTier = "JustDoIt",
            summary = "test",
            confidence = 0.1
        });
        return Task.FromResult<object?>(json);
    }
}

/// <summary>
/// No-op message publisher for tests.
/// </summary>
internal sealed class NullMessagePublisher : IMessagePublisher
{
    public Task PublishAsync(
        string queueName,
        MessageEnvelope envelope,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed record TestMessage : IMessage
{
    public required string Content { get; init; }
}
```

Note: The test file requires helper types (`CapturingSkillExecutor`, `NullMessagePublisher`, `TestMessage`). If `TestMessage` already exists in the test project, remove the duplicate and use the existing one.

Check for an existing `TestMessage` in the Agents test project first:
Run: `grep -r "record TestMessage" tests/Cortex.Agents.Tests/`

If it exists, remove the duplicate definition and add a using statement instead.

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests/ --configuration Release --filter "FullyQualifiedName~SkillDrivenAgentContextTests" --verbosity normal`
Expected: Build FAIL — `SkillDrivenAgent` constructor doesn't accept `IContextProvider`

**Step 3: Modify SkillDrivenAgent constructor**

In `src/Cortex.Agents/SkillDrivenAgent.cs`:

1. Add `using Cortex.Core.Context;` to the usings
2. Add an optional `IContextProvider? contextProvider = null` parameter to the constructor
3. Store it in a `private readonly IContextProvider? _contextProvider;` field
4. In `ProcessAsync`, after building the existing parameters dictionary, add context query logic:

```csharp
// After the existing parameter building (lines 83-87):
if (_contextProvider is not null)
{
    var messageContent = parameters["messageContent"] as string ?? "";
    var contextResults = await _contextProvider.QueryAsync(
        new ContextQuery { Keywords = messageContent, MaxResults = 5 },
        cancellationToken);

    if (contextResults.Count > 0)
    {
        var contextSummary = string.Join("\n",
            contextResults.Select(c => $"[{c.Category}] {c.Content}"));
        parameters["businessContext"] = contextSummary;
    }
}
```

**Step 4: Run context tests**

Run: `dotnet test tests/Cortex.Agents.Tests/ --configuration Release --filter "FullyQualifiedName~SkillDrivenAgentContextTests" --verbosity normal`
Expected: Both tests PASS

**Step 5: Run full Agents test suite for regressions**

Run: `dotnet test tests/Cortex.Agents.Tests/ --configuration Release --verbosity normal`
Expected: All tests PASS (existing tests unaffected because parameter is optional with default null)

**Step 6: Commit**

```bash
git add src/Cortex.Agents/SkillDrivenAgent.cs tests/Cortex.Agents.Tests/SkillDrivenAgentContextTests.cs
git commit -m "feat(context): wire IContextProvider into SkillDrivenAgent pipeline (#25)"
```

---

### Task 8: Full Test Suite and Final Verification

**Step 1: Build entire solution**

Run: `dotnet build --configuration Release`
Expected: Build succeeded, 0 errors, 0 warnings

**Step 2: Run all unit tests**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests PASS

**Step 3: Commit any final adjustments if needed**

If any linter or formatting changes are needed, commit them.

**Step 4: Verify git log looks clean**

Run: `git log --oneline -10`
Expected: Clean sequence of commits for this feature.
