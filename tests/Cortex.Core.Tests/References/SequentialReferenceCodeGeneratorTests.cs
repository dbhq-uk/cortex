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
}
