using System.Globalization;
using System.Text.RegularExpressions;

namespace Cortex.Agents.Personas;

/// <summary>
/// Parses persona markdown files into <see cref="PersonaDefinition"/> records.
/// </summary>
public static partial class PersonaParser
{
    /// <summary>
    /// Parses a persona markdown string into a <see cref="PersonaDefinition"/>.
    /// </summary>
    /// <param name="markdown">The persona markdown content to parse.</param>
    /// <returns>A parsed <see cref="PersonaDefinition"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="markdown"/> is null or empty.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="markdown"/> is whitespace-only or missing required sections.</exception>
    public static PersonaDefinition Parse(string markdown)
    {
        if (markdown is null)
        {
            throw new ArgumentException("Persona markdown must not be null.", nameof(markdown));
        }

        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new FormatException("Persona markdown must not be empty or whitespace.");
        }

        var sections = SplitSections(markdown);

        if (!sections.TryGetValue("identity", out var identityLines))
        {
            throw new FormatException("Persona markdown must contain an '## Identity' section.");
        }

        var identity = ParseKeyValues(identityLines);
        var capabilities = sections.TryGetValue("capabilities", out var capLines)
            ? ParseCapabilities(capLines)
            : [];
        var pipeline = sections.TryGetValue("pipeline", out var pipeLines)
            ? ParsePipeline(pipeLines)
            : [];
        var config = sections.TryGetValue("configuration", out var configLines)
            ? ParseKeyValues(configLines)
            : new Dictionary<string, string>();

        return new PersonaDefinition
        {
            AgentId = identity.GetValueOrDefault("agent-id")
                ?? throw new FormatException("Identity section must contain 'agent-id'."),
            Name = identity.GetValueOrDefault("name")
                ?? throw new FormatException("Identity section must contain 'name'."),
            AgentType = identity.GetValueOrDefault("type")
                ?? throw new FormatException("Identity section must contain 'type'."),
            Capabilities = capabilities,
            Pipeline = pipeline,
            EscalationTarget = config.GetValueOrDefault("escalation-target")
                ?? throw new FormatException("Configuration section must contain 'escalation-target'."),
            ModelTier = config.GetValueOrDefault("model-tier") ?? "balanced",
            ConfidenceThreshold = config.TryGetValue("confidence-threshold", out var ct)
                ? double.Parse(ct, CultureInfo.InvariantCulture)
                : 0.6
        };
    }

    /// <summary>
    /// Parses a persona from a markdown file on disk.
    /// </summary>
    /// <param name="filePath">The path to the persona markdown file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A parsed <see cref="PersonaDefinition"/>.</returns>
    public static async Task<PersonaDefinition> ParseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Parse(content);
    }

    private static Dictionary<string, List<string>> SplitSections(string markdown)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                currentSection = line[3..].Trim().ToLowerInvariant();
                sections[currentSection] = [];
            }
            else if (currentSection is not null && !string.IsNullOrWhiteSpace(line))
            {
                sections[currentSection].Add(line);
            }
        }

        return sections;
    }

    private static Dictionary<string, string> ParseKeyValues(List<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var match = BoldKeyValuePattern().Match(line);
            if (match.Success)
            {
                result[match.Groups["key"].Value.Trim()] = match.Groups["value"].Value.Trim();
            }
        }

        return result;
    }

    private static List<AgentCapability> ParseCapabilities(List<string> lines)
    {
        var capabilities = new List<AgentCapability>();

        foreach (var line in lines)
        {
            var match = CapabilityPattern().Match(line);
            if (match.Success)
            {
                capabilities.Add(new AgentCapability
                {
                    Name = match.Groups["name"].Value.Trim(),
                    Description = match.Groups["desc"].Value.Trim()
                });
            }
        }

        return capabilities;
    }

    private static List<string> ParsePipeline(List<string> lines)
    {
        var pipeline = new List<string>();

        foreach (var line in lines)
        {
            var match = PipelineStepPattern().Match(line);
            if (match.Success)
            {
                pipeline.Add(match.Groups["skill"].Value.Trim());
            }
        }

        return pipeline;
    }

    [GeneratedRegex(@"^-\s+\*\*(?<key>[^*]+)\*\*:\s*(?<value>.+)$")]
    private static partial Regex BoldKeyValuePattern();

    [GeneratedRegex(@"^-\s+(?<name>[^:]+):\s*(?<desc>.+)$")]
    private static partial Regex CapabilityPattern();

    [GeneratedRegex(@"^\d+\.\s+(?<skill>.+)$")]
    private static partial Regex PipelineStepPattern();
}
