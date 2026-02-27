namespace Cortex.Core.Workflows;

/// <summary>
/// Current status of a multi-task workflow.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>Workflow is active, waiting for sub-task completions.</summary>
    InProgress,

    /// <summary>All sub-tasks completed successfully.</summary>
    Completed,

    /// <summary>One or more sub-tasks failed.</summary>
    Failed
}
