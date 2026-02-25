using Cortex.Core.Messages;
using Cortex.Skills;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents.Pipeline;

/// <summary>
/// Executes an ordered list of skills, passing context between them.
/// Each skill receives the original envelope, additional parameters, and all prior skill results.
/// </summary>
public sealed class SkillPipelineRunner
{
    private readonly ISkillRegistry _skillRegistry;
    private readonly IReadOnlyDictionary<string, ISkillExecutor> _executors;
    private readonly ILogger<SkillPipelineRunner> _logger;

    /// <summary>
    /// Creates a new <see cref="SkillPipelineRunner"/>.
    /// </summary>
    public SkillPipelineRunner(
        ISkillRegistry skillRegistry,
        IEnumerable<ISkillExecutor> executors,
        ILogger<SkillPipelineRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(skillRegistry);
        ArgumentNullException.ThrowIfNull(executors);
        ArgumentNullException.ThrowIfNull(logger);

        _skillRegistry = skillRegistry;
        _executors = executors.ToDictionary(e => e.ExecutorType);
        _logger = logger;
    }

    /// <summary>
    /// Runs the skill pipeline and returns the accumulated context.
    /// </summary>
    public async Task<SkillPipelineContext> RunAsync(
        IReadOnlyList<string> skillIds,
        MessageEnvelope envelope,
        IDictionary<string, object>? additionalParameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillIds);
        ArgumentNullException.ThrowIfNull(envelope);

        var context = new SkillPipelineContext
        {
            Envelope = envelope,
            Parameters = additionalParameters ?? new Dictionary<string, object>()
        };

        foreach (var skillId in skillIds)
        {
            var definition = await _skillRegistry.FindByIdAsync(skillId, cancellationToken);
            if (definition is null)
            {
                _logger.LogWarning("Skill {SkillId} not found in registry, skipping", skillId);
                continue;
            }

            if (!_executors.TryGetValue(definition.ExecutorType, out var executor))
            {
                _logger.LogWarning(
                    "No executor for type {ExecutorType}, skipping skill {SkillId}",
                    definition.ExecutorType, skillId);
                continue;
            }

            var parameters = new Dictionary<string, object>(context.Parameters)
            {
                ["envelope"] = context.Envelope,
                ["results"] = context.Results
            };

            var result = await executor.ExecuteAsync(definition, parameters, cancellationToken);
            context.Results[skillId] = result;

            _logger.LogDebug("Skill {SkillId} completed", skillId);
        }

        return context;
    }
}
