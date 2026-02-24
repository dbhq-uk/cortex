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
