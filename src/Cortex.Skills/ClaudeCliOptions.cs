namespace Cortex.Skills;

/// <summary>
/// Configuration options for the <see cref="ClaudeCliClient"/>.
/// </summary>
public sealed class ClaudeCliOptions
{
    /// <summary>
    /// Timeout in seconds for each CLI invocation.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Path to the Claude CLI executable. Defaults to "claude" (found via PATH).
    /// </summary>
    public string CliPath { get; init; } = "claude";
}
