using Cortex.Skills;

namespace Cortex.Agents.Tests.Pipeline;

/// <summary>
/// Test fake that returns preconfigured results keyed by skill ID.
/// </summary>
public sealed class FakeSkillExecutor : ISkillExecutor
{
    private readonly Dictionary<string, object?> _results = new();
    private readonly List<(string SkillId, IDictionary<string, object> Parameters)> _calls = [];

    /// <inheritdoc />
    public string ExecutorType { get; }

    /// <summary>
    /// Creates a new <see cref="FakeSkillExecutor"/> with the specified executor type.
    /// </summary>
    public FakeSkillExecutor(string executorType = "fake")
    {
        ExecutorType = executorType;
    }

    /// <summary>
    /// Configures the result to return when the specified skill is executed.
    /// </summary>
    public void SetResult(string skillId, object? result)
    {
        _results[skillId] = result;
    }

    /// <summary>
    /// All calls made to this executor: (skillId, parameters).
    /// </summary>
    public IReadOnlyList<(string SkillId, IDictionary<string, object> Parameters)> Calls => _calls;

    /// <inheritdoc />
    public Task<object?> ExecuteAsync(
        SkillDefinition skill,
        IDictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        _calls.Add((skill.SkillId, new Dictionary<string, object>(parameters)));
        _results.TryGetValue(skill.SkillId, out var result);
        return Task.FromResult(result);
    }
}
