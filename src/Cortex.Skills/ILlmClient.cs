namespace Cortex.Skills;

/// <summary>
/// Abstraction for language model completions.
/// Implementations may wrap a CLI, API, or local model.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends a prompt and returns the completion text.
    /// </summary>
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}
