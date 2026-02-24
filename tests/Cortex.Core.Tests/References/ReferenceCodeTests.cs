using Cortex.Core.References;

namespace Cortex.Core.Tests.References;

public class ReferenceCodeTests
{
    [Fact]
    public void Create_WithValidDateAndSequence_ProducesCorrectFormat()
    {
        var date = new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero);

        var code = ReferenceCode.Create(date, 1);

        Assert.Equal("CTX-2026-0221-001", code.Value);
    }

    [Fact]
    public void Create_WithHighSequence_PadsCorrectly()
    {
        var date = new DateTimeOffset(2026, 12, 5, 0, 0, 0, TimeSpan.Zero);

        var code = ReferenceCode.Create(date, 42);

        Assert.Equal("CTX-2026-1205-042", code.Value);
    }

    [Fact]
    public void Create_WithMaxSequence_Succeeds()
    {
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var code = ReferenceCode.Create(date, 999);

        Assert.Equal("CTX-2026-0101-999", code.Value);
    }

    [Fact]
    public void Create_WithZeroSequence_Throws()
    {
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => ReferenceCode.Create(date, 0));
    }

    [Fact]
    public void Create_WithNegativeSequence_Throws()
    {
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => ReferenceCode.Create(date, -1));
    }

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

    [Fact]
    public void Constructor_WithValidValue_Succeeds()
    {
        var code = new ReferenceCode("CTX-2026-0221-001");

        Assert.Equal("CTX-2026-0221-001", code.Value);
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ReferenceCode(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithEmptyOrWhitespace_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => new ReferenceCode(value));
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("CTX-2026-0221")]
    [InlineData("CTX-2026-0221-01")]
    [InlineData("ABC-2026-0221-001")]
    [InlineData("CTX-26-0221-001")]
    public void Constructor_WithInvalidFormat_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => new ReferenceCode(value));
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var code = new ReferenceCode("CTX-2026-0221-001");

        Assert.Equal("CTX-2026-0221-001", code.ToString());
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var code1 = new ReferenceCode("CTX-2026-0221-001");
        var code2 = new ReferenceCode("CTX-2026-0221-001");

        Assert.Equal(code1, code2);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var code1 = new ReferenceCode("CTX-2026-0221-001");
        var code2 = new ReferenceCode("CTX-2026-0221-002");

        Assert.NotEqual(code1, code2);
    }
}
