using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Skills.Tests;

public sealed class LlmSkillExecutorTests
{
    private readonly FakeLlmClient _fakeLlm = new();

    private LlmSkillExecutor CreateExecutor() =>
        new(_fakeLlm, NullLogger<LlmSkillExecutor>.Instance);

    private static SkillDefinition CreateSkillDefinition(
        string skillId = "test-skill",
        string? content = null) =>
        new()
        {
            SkillId = skillId,
            Name = "Test Skill",
            Description = "A test skill",
            Category = SkillCategory.Agent,
            ExecutorType = "llm",
            Content = content
        };

    [Fact]
    public void ExecutorType_IsLlm()
    {
        var executor = CreateExecutor();

        Assert.Equal("llm", executor.ExecutorType);
    }

    [Fact]
    public async Task ExecuteAsync_SendsPromptToLlmClient()
    {
        _fakeLlm.SetDefaultResponse("""{"capability":"test","authorityTier":"JustDoIt","summary":"test","confidence":0.9}""");
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: "You are a triage agent.");
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "Hello world",
            ["availableCapabilities"] = "email-drafting, code-review"
        };

        await executor.ExecuteAsync(skill, parameters);

        Assert.Single(_fakeLlm.Prompts);
        Assert.Contains("You are a triage agent.", _fakeLlm.Prompts[0]);
        Assert.Contains("Hello world", _fakeLlm.Prompts[0]);
        Assert.Contains("email-drafting", _fakeLlm.Prompts[0]);
    }

    [Fact]
    public async Task ExecuteAsync_UsesDescriptionIfNoContent()
    {
        _fakeLlm.SetDefaultResponse("{}");
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: null);
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "Hello",
            ["availableCapabilities"] = "none"
        };

        await executor.ExecuteAsync(skill, parameters);

        Assert.Contains("A test skill", _fakeLlm.Prompts[0]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsJsonElement()
    {
        _fakeLlm.SetDefaultResponse("""{"capability":"email-drafting","authorityTier":"DoItAndShowMe","summary":"Draft reply","confidence":0.92}""");
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: "Triage prompt");
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "Draft a reply",
            ["availableCapabilities"] = "email-drafting"
        };

        var result = await executor.ExecuteAsync(skill, parameters);

        Assert.IsType<JsonElement>(result);
        var json = (JsonElement)result!;
        Assert.Equal("email-drafting", json.GetProperty("capability").GetString());
        Assert.Equal(0.92, json.GetProperty("confidence").GetDouble());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ReturnsNull()
    {
        _fakeLlm.SetDefaultResponse("not valid json at all");
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: "Triage prompt");
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "test",
            ["availableCapabilities"] = "none"
        };

        var result = await executor.ExecuteAsync(skill, parameters);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_JsonWrappedInMarkdown_ExtractsJson()
    {
        _fakeLlm.SetDefaultResponse("""
            ```json
            {"capability":"email-drafting","authorityTier":"JustDoIt","summary":"test","confidence":0.8}
            ```
            """);
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: "Triage");
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "test",
            ["availableCapabilities"] = "email-drafting"
        };

        var result = await executor.ExecuteAsync(skill, parameters);

        Assert.IsType<JsonElement>(result);
        var json = (JsonElement)result!;
        Assert.Equal("email-drafting", json.GetProperty("capability").GetString());
    }
}
