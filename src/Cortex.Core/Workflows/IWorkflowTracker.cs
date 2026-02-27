using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Core.Workflows;

/// <summary>
/// Manages workflow lifecycle for multi-task decomposition.
/// </summary>
public interface IWorkflowTracker
{
    /// <summary>
    /// Records a new workflow.
    /// </summary>
    Task CreateAsync(WorkflowRecord workflow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the workflow that a sub-task belongs to, by the sub-task's reference code.
    /// Returns null if the reference code is not a known sub-task.
    /// </summary>
    Task<WorkflowRecord?> FindBySubtaskAsync(ReferenceCode subtaskRefCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a workflow by its parent reference code.
    /// </summary>
    Task<WorkflowRecord?> GetAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of an existing workflow.
    /// </summary>
    Task UpdateStatusAsync(ReferenceCode workflowRefCode, WorkflowStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a completed sub-task result against its workflow.
    /// </summary>
    Task StoreSubtaskResultAsync(ReferenceCode subtaskRefCode, MessageEnvelope result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all completed sub-task results for a workflow.
    /// </summary>
    Task<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>> GetCompletedResultsAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether all sub-tasks in a workflow have completed.
    /// </summary>
    Task<bool> AllSubtasksCompleteAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default);
}
