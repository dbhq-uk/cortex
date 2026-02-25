using Cortex.Agents.Pipeline;
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Agents.Tests.Pipeline;

public sealed class SkillPipelineContextTests
{
    private static MessageEnvelope CreateEnvelope(string content = "test") =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    [Fact]
    public void Construction_SetsEnvelope()
    {
        var envelope = CreateEnvelope("hello");

        var context = new SkillPipelineContext { Envelope = envelope };

        Assert.Same(envelope, context.Envelope);
    }

    [Fact]
    public void Results_StartsEmpty()
    {
        var context = new SkillPipelineContext { Envelope = CreateEnvelope() };

        Assert.Empty(context.Results);
    }

    [Fact]
    public void Results_AccumulatesSkillOutputs()
    {
        var context = new SkillPipelineContext { Envelope = CreateEnvelope() };

        context.Results["skill-a"] = "output-a";
        context.Results["skill-b"] = 42;

        Assert.Equal(2, context.Results.Count);
        Assert.Equal("output-a", context.Results["skill-a"]);
        Assert.Equal(42, context.Results["skill-b"]);
    }

    [Fact]
    public void Parameters_StartsEmpty()
    {
        var context = new SkillPipelineContext { Envelope = CreateEnvelope() };

        Assert.Empty(context.Parameters);
    }

    [Fact]
    public void Parameters_CanBeSetAtConstruction()
    {
        var parameters = new Dictionary<string, object> { ["key"] = "value" };

        var context = new SkillPipelineContext
        {
            Envelope = CreateEnvelope(),
            Parameters = parameters
        };

        Assert.Equal("value", context.Parameters["key"]);
    }
}
