using Cortex.Agents.Personas;

namespace Cortex.Agents.Tests.Personas;

public sealed class PersonaParserTests
{
    private const string ValidPersona = """
        # Chief of Staff

        ## Identity
        - **agent-id**: cos
        - **name**: Chief of Staff
        - **type**: ai

        ## Capabilities
        - triage: Analyses incoming messages and determines routing
        - routing: Routes messages to specialist agents by capability

        ## Pipeline
        1. cos-triage

        ## Configuration
        - **escalation-target**: agent.founder
        - **model-tier**: balanced
        - **confidence-threshold**: 0.6
        """;

    [Fact]
    public void Parse_ValidMarkdown_ExtractsAgentId()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("cos", result.AgentId);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsName()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("Chief of Staff", result.Name);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsAgentType()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("ai", result.AgentType);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsCapabilities()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal(2, result.Capabilities.Count);
        Assert.Equal("triage", result.Capabilities[0].Name);
        Assert.Equal("Analyses incoming messages and determines routing", result.Capabilities[0].Description);
        Assert.Equal("routing", result.Capabilities[1].Name);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsPipeline()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Single(result.Pipeline);
        Assert.Equal("cos-triage", result.Pipeline[0]);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsEscalationTarget()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("agent.founder", result.EscalationTarget);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsModelTier()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("balanced", result.ModelTier);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsConfidenceThreshold()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal(0.6, result.ConfidenceThreshold);
    }

    [Fact]
    public void Parse_MultiplePipelineSteps_ExtractsAll()
    {
        var markdown = """
            # Multi-Step Agent

            ## Identity
            - **agent-id**: multi
            - **name**: Multi
            - **type**: ai

            ## Capabilities
            - analysis: Analyses things

            ## Pipeline
            1. step-one
            2. step-two
            3. step-three

            ## Configuration
            - **escalation-target**: agent.founder
            """;

        var result = PersonaParser.Parse(markdown);

        Assert.Equal(3, result.Pipeline.Count);
        Assert.Equal("step-one", result.Pipeline[0]);
        Assert.Equal("step-two", result.Pipeline[1]);
        Assert.Equal("step-three", result.Pipeline[2]);
    }

    [Fact]
    public void Parse_MissingConfidenceThreshold_DefaultsTo06()
    {
        var markdown = """
            # Minimal Agent

            ## Identity
            - **agent-id**: minimal
            - **name**: Minimal
            - **type**: ai

            ## Capabilities
            - work: Does work

            ## Pipeline
            1. do-work

            ## Configuration
            - **escalation-target**: agent.founder
            """;

        var result = PersonaParser.Parse(markdown);

        Assert.Equal(0.6, result.ConfidenceThreshold);
    }

    [Fact]
    public void Parse_EmptyMarkdown_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => PersonaParser.Parse(""));
    }

    [Fact]
    public void Parse_NullMarkdown_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PersonaParser.Parse(null!));
    }
}
