# Reference Code Generator Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement `IReferenceCodeGenerator` with pluggable persistent sequence tracking, daily reset, overflow handling, and file-based storage.

**Architecture:** `SequentialReferenceCodeGenerator` owns thread safety via `SemaphoreSlim` and delegates persistence to an `ISequenceStore`. Ships with `FileSequenceStore` (configurable path) and `InMemorySequenceStore` (tests). The existing `ReferenceCode` value object is updated to support 3-4 digit sequences for overflow.

**Tech Stack:** .NET 10, C#, xUnit, System.Text.Json

---

### Task 1: Update ReferenceCode to support overflow (3-4 digit sequences)

**Files:**
- Modify: `src/Cortex.Core/References/ReferenceCode.cs`
- Modify: `tests/Cortex.Core.Tests/References/ReferenceCodeTests.cs`

**Step 1: Add overflow test cases to existing test file**

Add these tests to `tests/Cortex.Core.Tests/References/ReferenceCodeTests.cs`:

```csharp
[Fact]
public void Create_WithSequenceOver999_ProducesFourDigitFormat()
{
    var date = new DateTimeOffset(2026, 2, 24, 0, 0, 0, TimeSpan.Zero);

    var code = ReferenceCode.Create(date, 1000);

    Assert.Equal("CTX-2026-0224-1000", code.Value);
}

[Fact]
public void Create_WithMaxExtendedSequence_Succeeds()
{
    var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    var code = ReferenceCode.Create(date, 9999);

    Assert.Equal("CTX-2026-0101-9999", code.Value);
}

[Fact]
public void Create_WithSequenceOver9999_Throws()
{
    var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    Assert.Throws<ArgumentOutOfRangeException>(() => ReferenceCode.Create(date, 10000));
}

[Fact]
public void Constructor_WithFourDigitSequence_Succeeds()
{
    var code = new ReferenceCode("CTX-2026-0224-1000");

    Assert.Equal("CTX-2026-0224-1000", code.Value);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~ReferenceCodeTests"`
Expected: 4 new tests FAIL — sequence over 999 throws, 4-digit format rejected by regex.

**Step 3: Update ReferenceCode to support 3-4 digit sequences**

In `src/Cortex.Core/References/ReferenceCode.cs`, make these changes:

1. Change the regex from `^CTX-\d{4}-\d{4}-\d{3}$` to `^CTX-\d{4}-\d{4}-\d{3,4}$`
2. Change `ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, 999)` to `ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, 9999)`
3. Change the format string from `{sequence:D3}` to `(sequence > 999 ? $"{sequence:D4}" : $"{sequence:D3}")` — use conditional formatting

The full updated `Create` method:

```csharp
public static ReferenceCode Create(DateTimeOffset date, int sequence)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, 9999);

    var sequencePart = sequence > 999 ? $"{sequence:D4}" : $"{sequence:D3}";
    var value = $"CTX-{date.Year:D4}-{date.Month:D2}{date.Day:D2}-{sequencePart}";
    return new ReferenceCode(value);
}
```

The full updated regex:

```csharp
[GeneratedRegex(@"^CTX-\d{4}-\d{4}-\d{3,4}$")]
private static partial Regex ReferenceCodePattern();
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~ReferenceCodeTests"`
Expected: ALL tests pass (old and new).

**Step 5: Run full test suite to check for regressions**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass.

**Step 6: Commit**

```bash
git add src/Cortex.Core/References/ReferenceCode.cs tests/Cortex.Core.Tests/References/ReferenceCodeTests.cs
git commit -m "feat: extend ReferenceCode to support 3-4 digit sequences for overflow"
```

---

### Task 2: Update IReferenceCodeGenerator to async

**Files:**
- Modify: `src/Cortex.Core/References/IReferenceCodeGenerator.cs`

**Step 1: Update the interface to async**

Replace the entire file `src/Cortex.Core/References/IReferenceCodeGenerator.cs` with:

```csharp
namespace Cortex.Core.References;

/// <summary>
/// Generates unique reference codes for message tracking.
/// </summary>
public interface IReferenceCodeGenerator
{
    /// <summary>
    /// Generates a new unique reference code.
    /// </summary>
    Task<ReferenceCode> GenerateAsync(CancellationToken cancellationToken = default);
}
```

**Step 2: Verify build succeeds**

Run: `dotnet build --configuration Release`
Expected: Build succeeds — no existing code calls `Generate()`.

**Step 3: Commit**

```bash
git add src/Cortex.Core/References/IReferenceCodeGenerator.cs
git commit -m "feat: change IReferenceCodeGenerator to async with CancellationToken"
```

---

### Task 3: Add ISequenceStore and SequenceState

**Files:**
- Create: `src/Cortex.Core/References/SequenceState.cs`
- Create: `src/Cortex.Core/References/ISequenceStore.cs`

**Step 1: Create SequenceState record**

Create `src/Cortex.Core/References/SequenceState.cs`:

```csharp
namespace Cortex.Core.References;

/// <summary>
/// The persisted state of a reference code sequence — the date and current sequence number.
/// </summary>
/// <param name="Date">The date this sequence belongs to.</param>
/// <param name="Sequence">The last sequence number generated for this date.</param>
public record SequenceState(DateOnly Date, int Sequence);
```

**Step 2: Create ISequenceStore interface**

Create `src/Cortex.Core/References/ISequenceStore.cs`:

```csharp
namespace Cortex.Core.References;

/// <summary>
/// Persists reference code sequence state across restarts.
/// </summary>
public interface ISequenceStore
{
    /// <summary>
    /// Loads the current sequence state, or returns a zeroed state if none exists.
    /// </summary>
    Task<SequenceState> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current sequence state.
    /// </summary>
    Task SaveAsync(SequenceState state, CancellationToken cancellationToken = default);
}
```

**Step 3: Verify build succeeds**

Run: `dotnet build --configuration Release`
Expected: Build succeeds.

**Step 4: Commit**

```bash
git add src/Cortex.Core/References/SequenceState.cs src/Cortex.Core/References/ISequenceStore.cs
git commit -m "feat: add ISequenceStore and SequenceState for pluggable sequence persistence"
```

---

### Task 4: Add InMemorySequenceStore

**Files:**
- Create: `src/Cortex.Core/References/InMemorySequenceStore.cs`
- Create: `tests/Cortex.Core.Tests/References/InMemorySequenceStoreTests.cs`

**Step 1: Write tests for InMemorySequenceStore**

Create `tests/Cortex.Core.Tests/References/InMemorySequenceStoreTests.cs`:

```csharp
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~InMemorySequenceStoreTests"`
Expected: FAIL — `InMemorySequenceStore` does not exist.

**Step 3: Implement InMemorySequenceStore**

Create `src/Cortex.Core/References/InMemorySequenceStore.cs`:

```csharp
namespace Cortex.Core.References;

/// <summary>
/// In-memory implementation of <see cref="ISequenceStore"/> for testing and local development.
/// State is lost when the process exits.
/// </summary>
public sealed class InMemorySequenceStore : ISequenceStore
{
    private SequenceState _state = new(DateOnly.MinValue, 0);

    /// <inheritdoc />
    public Task<SequenceState> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_state);
    }

    /// <inheritdoc />
    public Task SaveAsync(SequenceState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~InMemorySequenceStoreTests"`
Expected: ALL 3 tests pass.

**Step 5: Commit**

```bash
git add src/Cortex.Core/References/InMemorySequenceStore.cs tests/Cortex.Core.Tests/References/InMemorySequenceStoreTests.cs
git commit -m "feat: add InMemorySequenceStore for testing and local development"
```

---

### Task 5: Add SequentialReferenceCodeGenerator

**Files:**
- Create: `src/Cortex.Core/References/SequentialReferenceCodeGenerator.cs`
- Create: `tests/Cortex.Core.Tests/References/SequentialReferenceCodeGeneratorTests.cs`

**Step 1: Write basic generation tests**

Create `tests/Cortex.Core.Tests/References/SequentialReferenceCodeGeneratorTests.cs`:

```csharp
using Cortex.Core.References;

namespace Cortex.Core.Tests.References;

public sealed class SequentialReferenceCodeGeneratorTests : IDisposable
{
    private readonly InMemorySequenceStore _store = new();
    private readonly SequentialReferenceCodeGenerator _generator;

    public SequentialReferenceCodeGeneratorTests()
    {
        _generator = new SequentialReferenceCodeGenerator(_store, TimeProvider.System);
    }

    public void Dispose()
    {
        _generator.Dispose();
    }

    [Fact]
    public async Task GenerateAsync_FirstCall_ReturnsSequenceOne()
    {
        var code = await _generator.GenerateAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expected = $"CTX-{today.Year:D4}-{today.Month:D2}{today.Day:D2}-001";
        Assert.Equal(expected, code.Value);
    }

    [Fact]
    public async Task GenerateAsync_MultipleCalls_IncrementsSequence()
    {
        var code1 = await _generator.GenerateAsync();
        var code2 = await _generator.GenerateAsync();
        var code3 = await _generator.GenerateAsync();

        Assert.EndsWith("-001", code1.Value);
        Assert.EndsWith("-002", code2.Value);
        Assert.EndsWith("-003", code3.Value);
    }

    [Fact]
    public async Task GenerateAsync_PersistsState()
    {
        await _generator.GenerateAsync();
        await _generator.GenerateAsync();

        var state = await _store.LoadAsync();

        Assert.Equal(2, state.Sequence);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SequentialReferenceCodeGeneratorTests"`
Expected: FAIL — `SequentialReferenceCodeGenerator` does not exist.

**Step 3: Implement SequentialReferenceCodeGenerator**

Create `src/Cortex.Core/References/SequentialReferenceCodeGenerator.cs`:

```csharp
namespace Cortex.Core.References;

/// <summary>
/// Generates sequential reference codes with daily reset and persistent sequence tracking.
/// Thread-safe via <see cref="SemaphoreSlim"/>. Delegates persistence to <see cref="ISequenceStore"/>.
/// </summary>
public sealed class SequentialReferenceCodeGenerator : IReferenceCodeGenerator, IDisposable
{
    private readonly ISequenceStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Creates a new <see cref="SequentialReferenceCodeGenerator"/>.
    /// </summary>
    /// <param name="store">The sequence store for persistence.</param>
    /// <param name="timeProvider">The time provider for determining the current date.</param>
    public SequentialReferenceCodeGenerator(ISequenceStore store, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<ReferenceCode> GenerateAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
            var state = await _store.LoadAsync(cancellationToken);

            var sequence = state.Date == today ? state.Sequence + 1 : 1;

            var newState = new SequenceState(today, sequence);
            await _store.SaveAsync(newState, cancellationToken);

            return ReferenceCode.Create(new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), sequence);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Disposes the semaphore.
    /// </summary>
    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SequentialReferenceCodeGeneratorTests"`
Expected: ALL 3 tests pass.

**Step 5: Commit**

```bash
git add src/Cortex.Core/References/SequentialReferenceCodeGenerator.cs tests/Cortex.Core.Tests/References/SequentialReferenceCodeGeneratorTests.cs
git commit -m "feat: add SequentialReferenceCodeGenerator with daily reset and persistence"
```

---

### Task 6: Add daily reset and concurrency tests

**Files:**
- Modify: `tests/Cortex.Core.Tests/References/SequentialReferenceCodeGeneratorTests.cs`

**Step 1: Add daily reset test using FakeTimeProvider**

The `Microsoft.Extensions.TimeProvider.Testing` package provides `FakeTimeProvider` for controlling time in tests. Add the package reference first.

Add to `tests/Cortex.Core.Tests/Cortex.Core.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="9.5.0" />
```

Then add these tests to the test file:

```csharp
[Fact]
public async Task GenerateAsync_NewDay_ResetsSequence()
{
    var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
        new DateTimeOffset(2026, 2, 24, 12, 0, 0, TimeSpan.Zero));
    using var generator = new SequentialReferenceCodeGenerator(_store, fakeTime);

    var day1Code = await generator.GenerateAsync();
    Assert.Equal("CTX-2026-0224-001", day1Code.Value);

    // Advance to next day
    fakeTime.Advance(TimeSpan.FromDays(1));

    var day2Code = await generator.GenerateAsync();
    Assert.Equal("CTX-2026-0225-001", day2Code.Value);
}

[Fact]
public async Task GenerateAsync_SameDay_ContinuesSequence()
{
    var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
        new DateTimeOffset(2026, 2, 24, 12, 0, 0, TimeSpan.Zero));
    using var generator = new SequentialReferenceCodeGenerator(_store, fakeTime);

    await generator.GenerateAsync();
    await generator.GenerateAsync();

    // Advance a few hours, still same day
    fakeTime.Advance(TimeSpan.FromHours(6));

    var code = await generator.GenerateAsync();
    Assert.Equal("CTX-2026-0224-003", code.Value);
}

[Fact]
public async Task GenerateAsync_ConcurrentCalls_ProduceUniqueSequences()
{
    var fakeTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
        new DateTimeOffset(2026, 2, 24, 12, 0, 0, TimeSpan.Zero));
    using var generator = new SequentialReferenceCodeGenerator(_store, fakeTime);

    var tasks = Enumerable.Range(0, 50)
        .Select(_ => generator.GenerateAsync())
        .ToArray();

    var codes = await Task.WhenAll(tasks);

    // All codes should be unique
    var uniqueValues = codes.Select(c => c.Value).Distinct().ToList();
    Assert.Equal(50, uniqueValues.Count);
}

[Fact]
public async Task GenerateAsync_ResumesFromPersistedState()
{
    // Simulate previous run: saved state has sequence 10 for today
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    await _store.SaveAsync(new SequenceState(today, 10));

    var code = await _generator.GenerateAsync();

    Assert.EndsWith("-011", code.Value);
}

[Fact]
public async Task GenerateAsync_CancellationRequested_ThrowsOperationCancelled()
{
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
        () => _generator.GenerateAsync(cts.Token));
}
```

**Step 2: Install the testing package**

Run: `dotnet add tests/Cortex.Core.Tests/Cortex.Core.Tests.csproj package Microsoft.Extensions.TimeProvider.Testing`

**Step 3: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SequentialReferenceCodeGeneratorTests"`
Expected: ALL 8 tests pass.

**Step 4: Commit**

```bash
git add tests/Cortex.Core.Tests/
git commit -m "test: add daily reset, concurrency, and persistence resume tests"
```

---

### Task 7: Add FileSequenceStore

**Files:**
- Create: `src/Cortex.Core/References/FileSequenceStoreOptions.cs`
- Create: `src/Cortex.Core/References/FileSequenceStore.cs`
- Create: `tests/Cortex.Core.Tests/References/FileSequenceStoreTests.cs`

**Step 1: Write tests for FileSequenceStore**

Create `tests/Cortex.Core.Tests/References/FileSequenceStoreTests.cs`:

```csharp
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
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~FileSequenceStoreTests"`
Expected: FAIL — `FileSequenceStore` and `FileSequenceStoreOptions` do not exist.

**Step 3: Add Microsoft.Extensions.Options package reference to Cortex.Core**

Run: `dotnet add src/Cortex.Core/Cortex.Core.csproj package Microsoft.Extensions.Options`

**Step 4: Create FileSequenceStoreOptions**

Create `src/Cortex.Core/References/FileSequenceStoreOptions.cs`:

```csharp
namespace Cortex.Core.References;

/// <summary>
/// Configuration options for <see cref="FileSequenceStore"/>.
/// </summary>
public sealed class FileSequenceStoreOptions
{
    /// <summary>
    /// Path to the JSON file where sequence state is persisted.
    /// </summary>
    public required string FilePath { get; set; }
}
```

**Step 5: Create FileSequenceStore**

Create `src/Cortex.Core/References/FileSequenceStore.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Cortex.Core.References;

/// <summary>
/// Persists reference code sequence state to a JSON file on disk.
/// </summary>
public sealed class FileSequenceStore : ISequenceStore
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Creates a new <see cref="FileSequenceStore"/>.
    /// </summary>
    public FileSequenceStore(IOptions<FileSequenceStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.FilePath);

        _filePath = options.Value.FilePath;
    }

    /// <inheritdoc />
    public async Task<SequenceState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new SequenceState(DateOnly.MinValue, 0);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            var data = JsonSerializer.Deserialize<FileData>(json, JsonOptions);

            if (data is null)
            {
                return new SequenceState(DateOnly.MinValue, 0);
            }

            return new SequenceState(DateOnly.Parse(data.Date), data.Sequence);
        }
        catch (Exception) when (it is JsonException or FormatException)
        {
            return new SequenceState(DateOnly.MinValue, 0);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(SequenceState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new FileData
        {
            Date = state.Date.ToString("yyyy-MM-dd"),
            Sequence = state.Sequence
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    private sealed class FileData
    {
        public string Date { get; init; } = "";
        public int Sequence { get; init; }
    }
}
```

**Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~FileSequenceStoreTests"`
Expected: ALL 6 tests pass.

**Step 7: Run full test suite**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass.

**Step 8: Commit**

```bash
git add src/Cortex.Core/References/FileSequenceStoreOptions.cs src/Cortex.Core/References/FileSequenceStore.cs src/Cortex.Core/Cortex.Core.csproj tests/Cortex.Core.Tests/References/FileSequenceStoreTests.cs
git commit -m "feat: add FileSequenceStore with configurable path and directory creation"
```

---

### Task 8: Final verification and cleanup

**Files:**
- Modify: `CHANGELOG.md`

**Step 1: Run full build**

Run: `dotnet build --configuration Release`
Expected: Build succeeds with 0 errors, 0 warnings.

**Step 2: Run full test suite**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass.

**Step 3: Update CHANGELOG.md**

Add under the `## [Unreleased]` section:

```markdown
### Added
- `ISequenceStore` interface for pluggable sequence persistence
- `InMemorySequenceStore` for testing and local development
- `FileSequenceStore` with configurable file path for production persistence
- `SequentialReferenceCodeGenerator` implementing `IReferenceCodeGenerator` with daily reset and thread safety

### Changed
- `IReferenceCodeGenerator.Generate()` → `GenerateAsync(CancellationToken)` (async)
- `ReferenceCode` now supports 3-4 digit sequences (overflow from 999 to 9999)
```

**Step 4: Commit**

```bash
git add CHANGELOG.md
git commit -m "docs: update CHANGELOG for reference code generator"
```
