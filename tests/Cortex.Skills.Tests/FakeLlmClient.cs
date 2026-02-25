using Cortex.Skills;

namespace Cortex.Skills.Tests;

/// <summary>
/// Test fake that returns preconfigured LLM responses.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Queue<string> _responses = new();
    private string _defaultResponse = "{}";
    private readonly List<string> _prompts = [];

    /// <summary>
    /// All prompts sent to this client.
    /// </summary>
    public IReadOnlyList<string> Prompts => _prompts;

    /// <summary>
    /// Sets the default response for all calls.
    /// </summary>
    public void SetDefaultResponse(string response)
    {
        _defaultResponse = response;
    }

    /// <summary>
    /// Enqueues a response to return on the next call.
    /// </summary>
    public void EnqueueResponse(string response)
    {
        _responses.Enqueue(response);
    }

    /// <inheritdoc />
    public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _prompts.Add(prompt);
        var response = _responses.Count > 0 ? _responses.Dequeue() : _defaultResponse;
        return Task.FromResult(response);
    }
}
