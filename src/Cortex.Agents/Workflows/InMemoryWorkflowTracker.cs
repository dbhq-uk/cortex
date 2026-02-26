using System.Collections.Concurrent;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;

namespace Cortex.Agents.Workflows;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IWorkflowTracker"/>.
/// Stores workflow records and tracks partial sub-task results for aggregation.
/// </summary>
public sealed class InMemoryWorkflowTracker : IWorkflowTracker
{
    private readonly ConcurrentDictionary<string, WorkflowState> _workflows = new();
    private readonly ConcurrentDictionary<string, string> _subtaskToWorkflow = new();

    /// <inheritdoc />
    public Task CreateAsync(WorkflowRecord workflow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var state = new WorkflowState(workflow);
        _workflows[workflow.ReferenceCode.Value] = state;

        foreach (var subtaskRef in workflow.SubtaskReferenceCodes)
        {
            _subtaskToWorkflow[subtaskRef.Value] = workflow.ReferenceCode.Value;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowRecord?> FindBySubtaskAsync(
        ReferenceCode subtaskRefCode, CancellationToken cancellationToken = default)
    {
        if (_subtaskToWorkflow.TryGetValue(subtaskRefCode.Value, out var workflowKey)
            && _workflows.TryGetValue(workflowKey, out var state))
        {
            return Task.FromResult<WorkflowRecord?>(state.Record);
        }

        return Task.FromResult<WorkflowRecord?>(null);
    }

    /// <inheritdoc />
    public Task<WorkflowRecord?> GetAsync(
        ReferenceCode workflowRefCode, CancellationToken cancellationToken = default)
    {
        _workflows.TryGetValue(workflowRefCode.Value, out var state);
        return Task.FromResult<WorkflowRecord?>(state?.Record);
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(
        ReferenceCode workflowRefCode, WorkflowStatus status, CancellationToken cancellationToken = default)
    {
        if (_workflows.TryGetValue(workflowRefCode.Value, out var state))
        {
            state.Record = state.Record with { Status = status };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StoreSubtaskResultAsync(
        ReferenceCode subtaskRefCode, MessageEnvelope result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (_subtaskToWorkflow.TryGetValue(subtaskRefCode.Value, out var workflowKey)
            && _workflows.TryGetValue(workflowKey, out var state))
        {
            lock (state.CompletedResults)
            {
                state.CompletedResults[subtaskRefCode] = result;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>> GetCompletedResultsAsync(
        ReferenceCode workflowRefCode, CancellationToken cancellationToken = default)
    {
        if (_workflows.TryGetValue(workflowRefCode.Value, out var state))
        {
            lock (state.CompletedResults)
            {
                var copy = new Dictionary<ReferenceCode, MessageEnvelope>(state.CompletedResults);
                return Task.FromResult<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>>(copy);
            }
        }

        return Task.FromResult<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>>(
            new Dictionary<ReferenceCode, MessageEnvelope>());
    }

    /// <inheritdoc />
    public Task<bool> AllSubtasksCompleteAsync(
        ReferenceCode workflowRefCode, CancellationToken cancellationToken = default)
    {
        if (!_workflows.TryGetValue(workflowRefCode.Value, out var state))
        {
            return Task.FromResult(false);
        }

        lock (state.CompletedResults)
        {
            var allComplete = state.Record.SubtaskReferenceCodes
                .All(r => state.CompletedResults.ContainsKey(r));
            return Task.FromResult(allComplete);
        }
    }

    private sealed class WorkflowState
    {
        public WorkflowState(WorkflowRecord record)
        {
            Record = record;
        }

        public WorkflowRecord Record { get; set; }
        public Dictionary<ReferenceCode, MessageEnvelope> CompletedResults { get; } = new();
    }
}
