using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cortex.Skills.Tests;

public sealed class ClaudeCliClientTests
{
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var options = Options.Create(new ClaudeCliOptions());

        Assert.Throws<ArgumentNullException>(() =>
            new ClaudeCliClient(null!, options));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ClaudeCliClient(NullLogger<ClaudeCliClient>.Instance, null!));
    }

    [Fact]
    public async Task CompleteAsync_NullPrompt_ThrowsArgumentNullException()
    {
        var client = new ClaudeCliClient(
            NullLogger<ClaudeCliClient>.Instance,
            Options.Create(new ClaudeCliOptions()));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.CompleteAsync(null!));
    }

    [Fact]
    public async Task CompleteAsync_EmptyPrompt_ThrowsArgumentException()
    {
        var client = new ClaudeCliClient(
            NullLogger<ClaudeCliClient>.Instance,
            Options.Create(new ClaudeCliOptions()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.CompleteAsync(""));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompleteAsync_SimplePrompt_ReturnsNonEmptyResponse()
    {
        var client = new ClaudeCliClient(
            NullLogger<ClaudeCliClient>.Instance,
            Options.Create(new ClaudeCliOptions { TimeoutSeconds = 60 }));

        var result = await client.CompleteAsync("Respond with exactly: hello");

        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
