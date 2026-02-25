using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cortex.Skills;

/// <summary>
/// Skill executor for type "llm". Constructs a prompt from the skill definition
/// and parameters, sends it to an <see cref="ILlmClient"/>, and returns the
/// parsed JSON response.
/// </summary>
public sealed partial class LlmSkillExecutor : ISkillExecutor
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<LlmSkillExecutor> _logger;

    /// <summary>
    /// Creates a new <see cref="LlmSkillExecutor"/>.
    /// </summary>
    public LlmSkillExecutor(ILlmClient llmClient, ILogger<LlmSkillExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(llmClient);
        ArgumentNullException.ThrowIfNull(logger);

        _llmClient = llmClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ExecutorType => "llm";

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(
        SkillDefinition skill,
        IDictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(parameters);

        var systemPrompt = skill.Content ?? skill.Description;
        var messageContent = parameters.TryGetValue("messageContent", out var mc)
            ? mc.ToString() ?? ""
            : "";
        var capabilities = parameters.TryGetValue("availableCapabilities", out var caps)
            ? caps.ToString() ?? "none"
            : "none";

        var fullPrompt = $"""
            {systemPrompt}

            Available capabilities: {capabilities}

            Message:
            {messageContent}

            Respond with JSON only, no markdown formatting.
            """;

        _logger.LogDebug("Executing LLM skill {SkillId}", skill.SkillId);

        var response = await _llmClient.CompleteAsync(fullPrompt, cancellationToken);

        return ParseJsonResponse(response, skill.SkillId);
    }

    private object? ParseJsonResponse(string response, string skillId)
    {
        var cleaned = ExtractJsonFromMarkdown(response);

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(cleaned);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse JSON response from LLM skill {SkillId}",
                skillId);
            return null;
        }
    }

    private static string ExtractJsonFromMarkdown(string response)
    {
        var match = JsonCodeFencePattern().Match(response);
        return match.Success ? match.Groups["json"].Value.Trim() : response.Trim();
    }

    [GeneratedRegex(@"```(?:json)?\s*(?<json>\{[\s\S]*?\})\s*```")]
    private static partial Regex JsonCodeFencePattern();
}
