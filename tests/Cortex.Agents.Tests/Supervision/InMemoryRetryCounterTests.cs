using Cortex.Agents.Supervision;
using Cortex.Core.References;

namespace Cortex.Agents.Tests.Supervision;

public sealed class InMemoryRetryCounterTests
{
    private readonly InMemoryRetryCounter _counter = new();
    private int _sequenceCounter;

    private ReferenceCode CreateRefCode() =>
        ReferenceCode.Create(DateTimeOffset.UtcNow, Interlocked.Increment(ref _sequenceCounter));

    [Fact]
    public async Task GetCountAsync_NoIncrements_ReturnsZero()
    {
        var refCode = CreateRefCode();

        var count = await _counter.GetCountAsync(refCode);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task IncrementAsync_ReturnsNewCount()
    {
        var refCode = CreateRefCode();

        var first = await _counter.IncrementAsync(refCode);
        var second = await _counter.IncrementAsync(refCode);
        var third = await _counter.IncrementAsync(refCode);

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(3, third);
    }

    [Fact]
    public async Task GetCountAsync_AfterIncrements_ReturnsCurrent()
    {
        var refCode = CreateRefCode();

        await _counter.IncrementAsync(refCode);
        await _counter.IncrementAsync(refCode);

        var count = await _counter.GetCountAsync(refCode);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ResetAsync_ResetsToZero()
    {
        var refCode = CreateRefCode();

        await _counter.IncrementAsync(refCode);
        await _counter.IncrementAsync(refCode);
        await _counter.ResetAsync(refCode);

        var count = await _counter.GetCountAsync(refCode);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ResetAsync_Nonexistent_DoesNotThrow()
    {
        var refCode = CreateRefCode();

        var exception = await Record.ExceptionAsync(() => _counter.ResetAsync(refCode));

        Assert.Null(exception);
    }

    [Fact]
    public async Task IndependentRefCodes_TrackSeparately()
    {
        var refCodeA = CreateRefCode();
        var refCodeB = CreateRefCode();

        await _counter.IncrementAsync(refCodeA);
        await _counter.IncrementAsync(refCodeA);
        await _counter.IncrementAsync(refCodeA);
        await _counter.IncrementAsync(refCodeB);

        var countA = await _counter.GetCountAsync(refCodeA);
        var countB = await _counter.GetCountAsync(refCodeB);

        Assert.Equal(3, countA);
        Assert.Equal(1, countB);
    }
}
