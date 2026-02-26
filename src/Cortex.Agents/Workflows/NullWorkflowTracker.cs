using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;

namespace Cortex.Agents.Workflows;

/// <summary>
/// No-op workflow tracker for backward compatibility when decomposition is not needed.
/// </summary>
internal sealed class NullWorkflowTracker : IWorkflowTracker
{
    /// <inheritdoc />
    public Task CreateAsync(WorkflowRecord workflow, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task<WorkflowRecord?> FindBySubtaskAsync(ReferenceCode subtaskRefCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkflowRecord?>(null);

    /// <inheritdoc />
    public Task<WorkflowRecord?> GetAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkflowRecord?>(null);

    /// <inheritdoc />
    public Task UpdateStatusAsync(ReferenceCode workflowRefCode, WorkflowStatus status, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task StoreSubtaskResultAsync(ReferenceCode subtaskRefCode, MessageEnvelope result, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>> GetCompletedResultsAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>>(
            new Dictionary<ReferenceCode, MessageEnvelope>());

    /// <inheritdoc />
    public Task<bool> AllSubtasksCompleteAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
