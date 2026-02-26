# Multi-Task Decomposition Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable the CoS agent to decompose complex goals into multiple independent sub-tasks, delegate each to a specialist agent, collect results, and assemble a single response.

**Architecture:** Extend `SkillDrivenAgent` with a workflow branch alongside the existing 1:1 routing. A new `IWorkflowTracker` manages parent-child coordination. A new `cos-decompose` skill replaces `cos-triage`, producing either single-task or multi-task output. Sub-task replies are detected by reference code lookup and aggregated mechanically.

**Tech Stack:** C# / .NET 10 / xUnit / InMemoryMessageBus

---

### Task 1: WorkflowStatus Enum

**Files:**
- Create: `src/Cortex.Core/Workflows/WorkflowStatus.cs`

**Step 1: Write the enum**

```csharp
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
```

**Step 2: Build to verify it compiles**

Run: `dotnet build src/Cortex.Core/Cortex.Core.csproj --configuration Release`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Cortex.Core/Workflows/WorkflowStatus.cs
git commit -m "feat(core): add WorkflowStatus enum for multi-task workflows"
```

---

### Task 2: WorkflowRecord

**Files:**
- Create: `src/Cortex.Core/Workflows/WorkflowRecord.cs`
- Test: `tests/Cortex.Core.Tests/Workflows/WorkflowRecordTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;

namespace Cortex.Core.Tests.Workflows;

public sealed class WorkflowRecordTests
{
    private static MessageEnvelope CreateEnvelope() => new()
    {
        Message = new TextMessage("test"),
        ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
        Context = new MessageContext { ReplyTo = "agent.requester" }
    };

    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var parentRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var childRef1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var childRef2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);
        var envelope = CreateEnvelope();

        var record = new WorkflowRecord
        {
            ReferenceCode = parentRef,
            OriginalEnvelope = envelope,
            SubtaskReferenceCodes = [childRef1, childRef2],
            Summary = "Quarterly report"
        };

        Assert.Equal(parentRef, record.ReferenceCode);
        Assert.Same(envelope, record.OriginalEnvelope);
        Assert.Equal(2, record.SubtaskReferenceCodes.Count);
        Assert.Equal("Quarterly report", record.Summary);
    }

    [Fact]
    public void Status_DefaultsToInProgress()
    {
        var record = new WorkflowRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = [ReferenceCode.Create(DateTimeOffset.UtcNow, 2)],
            Summary = "Test"
        };

        Assert.Equal(WorkflowStatus.InProgress, record.Status);
    }

    [Fact]
    public void CreatedAt_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var record = new WorkflowRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = [ReferenceCode.Create(DateTimeOffset.UtcNow, 2)],
            Summary = "Test"
        };

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(record.CreatedAt, before, after);
    }

    [Fact]
    public void CompletedAt_DefaultsToNull()
    {
        var record = new WorkflowRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = [ReferenceCode.Create(DateTimeOffset.UtcNow, 2)],
            Summary = "Test"
        };

        Assert.Null(record.CompletedAt);
    }

    [Fact]
    public void With_CanUpdateStatus()
    {
        var record = new WorkflowRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = [ReferenceCode.Create(DateTimeOffset.UtcNow, 2)],
            Summary = "Test"
        };

        var completed = record with
        {
            Status = WorkflowStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal(WorkflowStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);
    }
}
```

Note: `TextMessage` does not exist yet. You need a concrete `IMessage` in `Cortex.Core` for tests. Check if one exists; if not, create a minimal one:

```csharp
// src/Cortex.Core/Messages/TextMessage.cs
namespace Cortex.Core.Messages;

/// <summary>
/// Simple text-based message for general communication.
/// </summary>
public sealed record TextMessage(string Content) : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }
}
```

If `Cortex.Core.Tests` already has a `TestMessage` in its own test project, use that instead. Check `tests/Cortex.Core.Tests/` for an existing test message type. If none exists, use the `TextMessage` above in `Cortex.Core` so both test projects can reference it.

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~WorkflowRecordTests"`
Expected: FAIL — `WorkflowRecord` does not exist yet

**Step 3: Write the implementation**

```csharp
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Core.Workflows;

/// <summary>
/// Tracks a decomposed goal as a coordinated unit of work.
/// The parent reference code identifies the workflow; each sub-task has its own reference code.
/// </summary>
public sealed record WorkflowRecord
{
    /// <summary>
    /// Parent reference code for the entire workflow.
    /// </summary>
    public required ReferenceCode ReferenceCode { get; init; }

    /// <summary>
    /// The original inbound envelope, preserved for ReplyTo and context.
    /// </summary>
    public required MessageEnvelope OriginalEnvelope { get; init; }

    /// <summary>
    /// Reference codes for each sub-task in the workflow.
    /// </summary>
    public required IReadOnlyList<ReferenceCode> SubtaskReferenceCodes { get; init; }

    /// <summary>
    /// Human-readable summary of the decomposed goal.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Current status of the workflow.
    /// </summary>
    public WorkflowStatus Status { get; init; } = WorkflowStatus.InProgress;

    /// <summary>
    /// When the workflow was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When the workflow completed, if applicable.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~WorkflowRecordTests"`
Expected: 5 passed

**Step 5: Commit**

```bash
git add src/Cortex.Core/Workflows/WorkflowRecord.cs src/Cortex.Core/Messages/TextMessage.cs tests/Cortex.Core.Tests/Workflows/WorkflowRecordTests.cs
git commit -m "feat(core): add WorkflowRecord for multi-task decomposition tracking"
```

---

### Task 3: IWorkflowTracker Interface

**Files:**
- Create: `src/Cortex.Core/Workflows/IWorkflowTracker.cs`

**Step 1: Write the interface**

```csharp
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
```

**Step 2: Build to verify it compiles**

Run: `dotnet build src/Cortex.Core/Cortex.Core.csproj --configuration Release`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Cortex.Core/Workflows/IWorkflowTracker.cs
git commit -m "feat(core): add IWorkflowTracker interface for workflow lifecycle"
```

---

### Task 4: InMemoryWorkflowTracker — Failing Tests

**Files:**
- Create: `tests/Cortex.Agents.Tests/Workflows/InMemoryWorkflowTrackerTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Agents.Workflows;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;

namespace Cortex.Agents.Tests.Workflows;

public sealed class InMemoryWorkflowTrackerTests
{
    private readonly InMemoryWorkflowTracker _tracker = new();

    private static MessageEnvelope CreateEnvelope(string content = "test") => new()
    {
        Message = new TestMessage { Content = content },
        ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
        Context = new MessageContext { ReplyTo = "agent.requester" }
    };

    private static WorkflowRecord CreateWorkflow(
        ReferenceCode? parentRef = null,
        IReadOnlyList<ReferenceCode>? subtaskRefs = null)
    {
        var parent = parentRef ?? ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var children = subtaskRefs ?? [
            ReferenceCode.Create(DateTimeOffset.UtcNow, 2),
            ReferenceCode.Create(DateTimeOffset.UtcNow, 3)
        ];

        return new WorkflowRecord
        {
            ReferenceCode = parent,
            OriginalEnvelope = CreateEnvelope(),
            SubtaskReferenceCodes = children,
            Summary = "Test workflow"
        };
    }

    // --- CreateAsync and GetAsync ---

    [Fact]
    public async Task CreateAsync_ThenGetAsync_ReturnsWorkflow()
    {
        var workflow = CreateWorkflow();
        await _tracker.CreateAsync(workflow);

        var retrieved = await _tracker.GetAsync(workflow.ReferenceCode);

        Assert.NotNull(retrieved);
        Assert.Equal(workflow.ReferenceCode, retrieved.ReferenceCode);
        Assert.Equal("Test workflow", retrieved.Summary);
    }

    [Fact]
    public async Task GetAsync_UnknownRefCode_ReturnsNull()
    {
        var result = await _tracker.GetAsync(ReferenceCode.Create(DateTimeOffset.UtcNow, 999));

        Assert.Null(result);
    }

    // --- FindBySubtaskAsync ---

    [Fact]
    public async Task FindBySubtaskAsync_KnownSubtask_ReturnsWorkflow()
    {
        var childRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var workflow = CreateWorkflow(subtaskRefs: [childRef]);
        await _tracker.CreateAsync(workflow);

        var found = await _tracker.FindBySubtaskAsync(childRef);

        Assert.NotNull(found);
        Assert.Equal(workflow.ReferenceCode, found.ReferenceCode);
    }

    [Fact]
    public async Task FindBySubtaskAsync_UnknownRefCode_ReturnsNull()
    {
        var result = await _tracker.FindBySubtaskAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 999));

        Assert.Null(result);
    }

    [Fact]
    public async Task FindBySubtaskAsync_ParentRefCode_ReturnsNull()
    {
        var parentRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var workflow = CreateWorkflow(parentRef: parentRef);
        await _tracker.CreateAsync(workflow);

        var result = await _tracker.FindBySubtaskAsync(parentRef);

        Assert.Null(result);
    }

    // --- UpdateStatusAsync ---

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var workflow = CreateWorkflow();
        await _tracker.CreateAsync(workflow);

        await _tracker.UpdateStatusAsync(workflow.ReferenceCode, WorkflowStatus.Completed);

        var retrieved = await _tracker.GetAsync(workflow.ReferenceCode);
        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowStatus.Completed, retrieved.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownRefCode_NoOp()
    {
        // Should not throw
        await _tracker.UpdateStatusAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 999),
            WorkflowStatus.Failed);
    }

    // --- StoreSubtaskResultAsync and GetCompletedResultsAsync ---

    [Fact]
    public async Task StoreSubtaskResultAsync_ThenGetCompletedResults_ReturnsResult()
    {
        var childRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var workflow = CreateWorkflow(subtaskRefs: [childRef]);
        await _tracker.CreateAsync(workflow);

        var resultEnvelope = CreateEnvelope("sub-task result");
        await _tracker.StoreSubtaskResultAsync(childRef, resultEnvelope);

        var results = await _tracker.GetCompletedResultsAsync(workflow.ReferenceCode);
        Assert.Single(results);
        Assert.True(results.ContainsKey(childRef));
    }

    [Fact]
    public async Task GetCompletedResultsAsync_NoResults_ReturnsEmpty()
    {
        var workflow = CreateWorkflow();
        await _tracker.CreateAsync(workflow);

        var results = await _tracker.GetCompletedResultsAsync(workflow.ReferenceCode);

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetCompletedResultsAsync_UnknownWorkflow_ReturnsEmpty()
    {
        var results = await _tracker.GetCompletedResultsAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 999));

        Assert.Empty(results);
    }

    // --- AllSubtasksCompleteAsync ---

    [Fact]
    public async Task AllSubtasksCompleteAsync_NoneComplete_ReturnsFalse()
    {
        var child1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var child2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);
        var workflow = CreateWorkflow(subtaskRefs: [child1, child2]);
        await _tracker.CreateAsync(workflow);

        Assert.False(await _tracker.AllSubtasksCompleteAsync(workflow.ReferenceCode));
    }

    [Fact]
    public async Task AllSubtasksCompleteAsync_PartialComplete_ReturnsFalse()
    {
        var child1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var child2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);
        var workflow = CreateWorkflow(subtaskRefs: [child1, child2]);
        await _tracker.CreateAsync(workflow);

        await _tracker.StoreSubtaskResultAsync(child1, CreateEnvelope("result 1"));

        Assert.False(await _tracker.AllSubtasksCompleteAsync(workflow.ReferenceCode));
    }

    [Fact]
    public async Task AllSubtasksCompleteAsync_AllComplete_ReturnsTrue()
    {
        var child1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 2);
        var child2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 3);
        var workflow = CreateWorkflow(subtaskRefs: [child1, child2]);
        await _tracker.CreateAsync(workflow);

        await _tracker.StoreSubtaskResultAsync(child1, CreateEnvelope("result 1"));
        await _tracker.StoreSubtaskResultAsync(child2, CreateEnvelope("result 2"));

        Assert.True(await _tracker.AllSubtasksCompleteAsync(workflow.ReferenceCode));
    }

    [Fact]
    public async Task AllSubtasksCompleteAsync_UnknownWorkflow_ReturnsFalse()
    {
        Assert.False(await _tracker.AllSubtasksCompleteAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 999)));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~InMemoryWorkflowTrackerTests"`
Expected: FAIL — `InMemoryWorkflowTracker` does not exist

---

### Task 5: InMemoryWorkflowTracker — Implementation

**Files:**
- Create: `src/Cortex.Agents/Workflows/InMemoryWorkflowTracker.cs`

**Step 1: Write the implementation**

```csharp
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

    /// <summary>
    /// Internal mutable state for a workflow. The record stays immutable;
    /// the state holds partial results as they arrive.
    /// </summary>
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
```

**Step 2: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~InMemoryWorkflowTrackerTests"`
Expected: 13 passed

**Step 3: Commit**

```bash
git add src/Cortex.Agents/Workflows/InMemoryWorkflowTracker.cs tests/Cortex.Agents.Tests/Workflows/InMemoryWorkflowTrackerTests.cs
git commit -m "feat(agents): add InMemoryWorkflowTracker with full test coverage"
```

---

### Task 6: DecompositionResult and DecompositionTask

**Files:**
- Create: `src/Cortex.Agents/Pipeline/DecompositionResult.cs`
- Create: `src/Cortex.Agents/Pipeline/DecompositionTask.cs`

**Step 1: Write DecompositionResult**

```csharp
namespace Cortex.Agents.Pipeline;

/// <summary>
/// Result of the cos-decompose skill — either a single routing decision
/// (backward compatible with <see cref="TriageResult"/>) or a multi-task decomposition.
/// When <see cref="Tasks"/> has exactly one entry, it is equivalent to 1:1 routing.
/// Multiple entries trigger the workflow path.
/// </summary>
public sealed record DecompositionResult
{
    /// <summary>
    /// The decomposed tasks. One entry = single routing. Multiple = workflow.
    /// </summary>
    public required IReadOnlyList<DecompositionTask> Tasks { get; init; }

    /// <summary>
    /// Human-readable summary of the overall goal.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) in the decomposition decision.
    /// </summary>
    public required double Confidence { get; init; }
}
```

**Step 2: Write DecompositionTask**

```csharp
namespace Cortex.Agents.Pipeline;

/// <summary>
/// A single routable sub-task within a decomposition.
/// </summary>
public sealed record DecompositionTask
{
    /// <summary>
    /// The capability name that should handle this sub-task.
    /// </summary>
    public required string Capability { get; init; }

    /// <summary>
    /// Description of what this sub-task should accomplish.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The authority tier for this sub-task as a string
    /// ("JustDoIt", "DoItAndShowMe", "AskMeFirst").
    /// </summary>
    public required string AuthorityTier { get; init; }
}
```

**Step 3: Build to verify it compiles**

Run: `dotnet build src/Cortex.Agents/Cortex.Agents.csproj --configuration Release`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/Cortex.Agents/Pipeline/DecompositionResult.cs src/Cortex.Agents/Pipeline/DecompositionTask.cs
git commit -m "feat(agents): add DecompositionResult and DecompositionTask types"
```

---

### Task 7: cos-decompose Skill Definition

**Files:**
- Create: `skills/cos-decompose.md`

**Step 1: Write the skill definition**

```markdown
# cos-decompose

## Metadata
- **skill-id**: cos-decompose
- **category**: agent
- **executor**: llm
- **version**: 1.0.0

## Description

Analyses incoming messages and determines routing. Produces either a single-task routing decision (backward compatible with cos-triage) or decomposes complex goals into multiple independent sub-tasks.

## Prompt

You are a decomposition agent for a business operating system called Cortex. Your job is to analyse incoming messages and determine the best routing strategy.

Given a message, business context, and a list of available agent capabilities, decide:

**Option A — Single task:** The message maps cleanly to one capability.
**Option B — Multiple tasks:** The message is a complex goal requiring multiple specialists working in parallel.

For each task (whether one or many), determine:
1. Which capability should handle it
2. What authority tier is appropriate:
   - JustDoIt: internal actions with no external footprint (log, update, file)
   - DoItAndShowMe: prepare and present for approval (draft email, create plan)
   - AskMeFirst: novel, high-stakes, or uncertain (send email, publish, spend money)
3. A clear description of what that sub-task should accomplish

Rules:
- Only use capabilities from the provided list. Never invent capabilities.
- Each task targets exactly one capability.
- Tasks are independent and can run in parallel — no ordering or dependencies.
- If unsure how to route or decompose, set confidence below 0.5 so the message escalates.
- Prefer fewer tasks over more. Only decompose when the goal genuinely requires different specialist capabilities.

Respond with JSON only, no markdown formatting:

{"tasks": [{"capability": "capability-name", "description": "what to do", "authorityTier": "DoItAndShowMe"}], "summary": "brief goal description", "confidence": 0.95}
```

**Step 2: Commit**

```bash
git add skills/cos-decompose.md
git commit -m "feat(skills): add cos-decompose skill definition"
```

---

### Task 8: SkillDrivenAgent — Extract DecompositionResult from Pipeline

**Files:**
- Modify: `src/Cortex.Agents/SkillDrivenAgent.cs:247-288` (the `ExtractTriageResult` method area)
- Test: `tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs`

This task adds the ability to parse `DecompositionResult` from the pipeline output, alongside the existing `TriageResult` extraction. It does NOT yet add the workflow branch — that comes in Task 9.

**Step 1: Write the failing tests**

Create `tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs`. This test class verifies that when `cos-decompose` returns a single-task result, existing 1:1 routing still works (backward compatibility).

```csharp
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Tests.Pipeline;
using Cortex.Agents.Workflows;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class SkillDrivenAgentDecompositionTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly InMemoryWorkflowTracker _workflowTracker = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;

    public SkillDrivenAgentDecompositionTests()
    {
        _refCodeGenerator = new SequentialReferenceCodeGenerator(
            new InMemorySequenceStore(), TimeProvider.System);
    }

    private SkillDrivenAgent CreateAgent()
    {
        var persona = new PersonaDefinition
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = "triage", Description = "Triage" }],
            Pipeline = ["cos-decompose"],
            EscalationTarget = "agent.founder",
            ConfidenceThreshold = 0.6
        };

        var pipelineRunner = new SkillPipelineRunner(
            _skillRegistry,
            [_fakeExecutor],
            NullLogger<SkillPipelineRunner>.Instance);

        return new SkillDrivenAgent(
            persona,
            pipelineRunner,
            _agentRegistry,
            _delegationTracker,
            _refCodeGenerator,
            _bus,
            NullLogger<SkillDrivenAgent>.Instance,
            contextProvider: null,
            workflowTracker: _workflowTracker);
    }

    private static MessageEnvelope CreateEnvelope(
        string content = "test", string? replyTo = "agent.requester") =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo }
        };

    private void RegisterDecomposeSkill()
    {
        _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-decompose",
            Name = "CoS Decompose",
            Description = "Decompose",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        }).GetAwaiter().GetResult();
    }

    private void SetDecomposeResult(object result)
    {
        var json = JsonSerializer.SerializeToElement(result);
        _fakeExecutor.SetResult("cos-decompose", json);
    }

    private async Task RegisterSpecialistAgent(string agentId, string capabilityName)
    {
        await _agentRegistry.RegisterAsync(new AgentRegistration
        {
            AgentId = agentId,
            Name = $"Agent {agentId}",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = capabilityName, Description = capabilityName }],
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = true
        });
    }

    // --- Single-task backward compatibility ---

    [Fact]
    public async Task ProcessAsync_SingleTask_RoutesToMatchingAgent()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = new[] { new { capability = "email-drafting", description = "Draft reply", authorityTier = "DoItAndShowMe" } },
            summary = "Draft email reply",
            confidence = 0.9
        });
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("Draft reply to John"));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(routedMsg);
    }

    [Fact]
    public async Task ProcessAsync_SingleTask_CreatesDelegationRecord()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = new[] { new { capability = "email-drafting", description = "Draft reply", authorityTier = "DoItAndShowMe" } },
            summary = "Draft email reply",
            confidence = 0.9
        });
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        await _bus.StartConsumingAsync("agent.email-agent", _ => Task.CompletedTask);

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var delegations = await _delegationTracker.GetByAssigneeAsync("email-agent");
        Assert.Single(delegations);
    }

    [Fact]
    public async Task ProcessAsync_SingleTask_DoesNotCreateWorkflow()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = new[] { new { capability = "email-drafting", description = "Draft reply", authorityTier = "DoItAndShowMe" } },
            summary = "Draft email reply",
            confidence = 0.9
        });
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        await _bus.StartConsumingAsync("agent.email-agent", _ => Task.CompletedTask);

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        // No workflow should be created for single-task routing
        var workflow = await _workflowTracker.GetAsync(
            ReferenceCode.Create(DateTimeOffset.UtcNow, 1));
        Assert.Null(workflow);
    }

    // --- Low confidence escalation ---

    [Fact]
    public async Task ProcessAsync_LowConfidence_Escalates()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = new[] { new { capability = "email-drafting", description = "Draft reply", authorityTier = "DoItAndShowMe" } },
            summary = "Unsure",
            confidence = 0.3
        });

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("ambiguous request"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    // --- Malformed result ---

    [Fact]
    public async Task ProcessAsync_MalformedResult_Escalates()
    {
        RegisterDecomposeSkill();
        _fakeExecutor.SetResult("cos-decompose",
            JsonSerializer.SerializeToElement(new { garbage = "data" }));

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    // --- Empty tasks array ---

    [Fact]
    public async Task ProcessAsync_EmptyTasks_Escalates()
    {
        RegisterDecomposeSkill();
        SetDecomposeResult(new
        {
            tasks = Array.Empty<object>(),
            summary = "Nothing to do",
            confidence = 0.9
        });

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SkillDrivenAgentDecompositionTests"`
Expected: FAIL — constructor does not accept `workflowTracker` parameter

---

### Task 9: SkillDrivenAgent — Add Workflow Tracker Dependency and Decomposition Parsing

**Files:**
- Modify: `src/Cortex.Agents/SkillDrivenAgent.cs`

This task modifies `SkillDrivenAgent` to:
1. Accept `IWorkflowTracker` as a constructor dependency
2. Replace `ExtractTriageResult` with `ExtractDecompositionResult`
3. Handle single-task decomposition the same as the old `TriageResult` path

**Step 1: Modify SkillDrivenAgent constructor**

Add `IWorkflowTracker` parameter after `IContextProvider?`:

```csharp
// Add to using statements at top of file
using Cortex.Core.Workflows;
```

Add field:
```csharp
private readonly IWorkflowTracker _workflowTracker;
```

Update constructor signature — add after `contextProvider`:
```csharp
IWorkflowTracker? workflowTracker = null
```

Add to constructor body:
```csharp
_workflowTracker = workflowTracker ?? new NullWorkflowTracker();
```

**Step 2: Create NullWorkflowTracker**

Create a no-op implementation for backward compatibility when no tracker is provided. Place inside `SkillDrivenAgent.cs` as a private nested class, or create a separate file `src/Cortex.Agents/Workflows/NullWorkflowTracker.cs`:

```csharp
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;

namespace Cortex.Agents.Workflows;

/// <summary>
/// No-op workflow tracker for backward compatibility when decomposition is not needed.
/// </summary>
internal sealed class NullWorkflowTracker : IWorkflowTracker
{
    public Task CreateAsync(WorkflowRecord workflow, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<WorkflowRecord?> FindBySubtaskAsync(ReferenceCode subtaskRefCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkflowRecord?>(null);

    public Task<WorkflowRecord?> GetAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkflowRecord?>(null);

    public Task UpdateStatusAsync(ReferenceCode workflowRefCode, WorkflowStatus status, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StoreSubtaskResultAsync(ReferenceCode subtaskRefCode, MessageEnvelope result, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>> GetCompletedResultsAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>>(
            new Dictionary<ReferenceCode, MessageEnvelope>());

    public Task<bool> AllSubtasksCompleteAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
```

**Step 3: Replace ExtractTriageResult with ExtractDecompositionResult**

Replace the `ExtractTriageResult` method (lines 247-288) with:

```csharp
private static DecompositionResult? ExtractDecompositionResult(SkillPipelineContext context)
{
    foreach (var result in context.Results.Values)
    {
        if (result is not JsonElement json)
        {
            continue;
        }

        try
        {
            if (!json.TryGetProperty("tasks", out var tasksElement)
                || tasksElement.ValueKind != JsonValueKind.Array)
            {
                // Try legacy TriageResult format for backward compatibility
                return ExtractFromLegacyTriageFormat(json);
            }

            var tasks = new List<DecompositionTask>();
            foreach (var taskElement in tasksElement.EnumerateArray())
            {
                var capability = taskElement.GetProperty("capability").GetString();
                var description = taskElement.GetProperty("description").GetString();
                var authorityTier = taskElement.GetProperty("authorityTier").GetString();

                if (capability is null || description is null || authorityTier is null)
                {
                    continue;
                }

                tasks.Add(new DecompositionTask
                {
                    Capability = capability,
                    Description = description,
                    AuthorityTier = authorityTier
                });
            }

            var summary = json.GetProperty("summary").GetString();
            var confidence = json.GetProperty("confidence").GetDouble();

            if (tasks.Count == 0 || summary is null)
            {
                continue;
            }

            return new DecompositionResult
            {
                Tasks = tasks,
                Summary = summary,
                Confidence = confidence
            };
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            continue;
        }
    }

    return null;
}

private static DecompositionResult? ExtractFromLegacyTriageFormat(JsonElement json)
{
    try
    {
        var capability = json.GetProperty("capability").GetString();
        var authorityStr = json.GetProperty("authorityTier").GetString();
        var summary = json.GetProperty("summary").GetString();
        var confidence = json.GetProperty("confidence").GetDouble();

        if (capability is null || authorityStr is null || summary is null)
        {
            return null;
        }

        return new DecompositionResult
        {
            Tasks =
            [
                new DecompositionTask
                {
                    Capability = capability,
                    Description = summary,
                    AuthorityTier = authorityStr
                }
            ],
            Summary = summary,
            Confidence = confidence
        };
    }
    catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
    {
        return null;
    }
}
```

**Step 4: Update ProcessAsync to use DecompositionResult for single-task path**

Replace the section from `var triageResult = ExtractTriageResult(context)` (line 112) through the routing logic. The single-task path should use the first `DecompositionTask` from the result:

```csharp
var decomposition = ExtractDecompositionResult(context);

if (decomposition is null || decomposition.Confidence < _persona.ConfidenceThreshold)
{
    var reason = decomposition is null ? "No decomposition result" : "Low confidence";
    await EscalateAsync(envelope, reason, cancellationToken);
    return null;
}

if (decomposition.Tasks.Count == 0)
{
    await EscalateAsync(envelope, "Empty task list", cancellationToken);
    return null;
}

if (decomposition.Tasks.Count == 1)
{
    // Single-task routing — same as legacy 1:1 path
    return await RouteSingleTaskAsync(envelope, decomposition.Tasks[0], cancellationToken);
}

// Multi-task workflow path — Task 10
return await RouteWorkflowAsync(envelope, decomposition, cancellationToken);
```

Extract the existing single-task routing into a helper method:

```csharp
private async Task<MessageEnvelope?> RouteSingleTaskAsync(
    MessageEnvelope envelope,
    DecompositionTask task,
    CancellationToken cancellationToken)
{
    if (!Enum.TryParse<AuthorityTier>(task.AuthorityTier, ignoreCase: true, out var taskAuthority))
    {
        taskAuthority = AuthorityTier.JustDoIt;
    }

    var candidates = await _agentRegistry.FindByCapabilityAsync(task.Capability, cancellationToken);
    var filtered = candidates.Where(a => a.AgentId != AgentId).ToList();

    if (filtered.Count == 0)
    {
        await EscalateAsync(envelope, $"No agent with capability '{task.Capability}'", cancellationToken);
        return null;
    }

    var target = filtered[0];
    var maxInbound = GetMaxAuthorityTier(envelope);
    var effectiveTier = (AuthorityTier)Math.Min((int)taskAuthority, (int)maxInbound);

    var refCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
    await _delegationTracker.DelegateAsync(new DelegationRecord
    {
        ReferenceCode = refCode,
        DelegatedBy = AgentId,
        DelegatedTo = target.AgentId,
        Description = task.Description,
        Status = DelegationStatus.Assigned,
        AssignedAt = DateTimeOffset.UtcNow
    }, cancellationToken);

    var routedEnvelope = envelope with
    {
        ReferenceCode = refCode,
        AuthorityClaims =
        [
            new AuthorityClaim
            {
                GrantedBy = AgentId,
                GrantedTo = target.AgentId,
                Tier = effectiveTier,
                GrantedAt = DateTimeOffset.UtcNow
            }
        ],
        Context = envelope.Context with
        {
            ParentMessageId = envelope.Message.MessageId,
            FromAgentId = AgentId
        }
    };

    await _messagePublisher.PublishAsync(routedEnvelope, $"agent.{target.AgentId}", cancellationToken);

    _logger.LogInformation(
        "Routed {RefCode} to {TargetAgent} (capability: {Capability}, authority: {Authority})",
        refCode, target.AgentId, task.Capability, effectiveTier);

    return null;
}
```

Add a placeholder for the workflow path (implemented in Task 10):

```csharp
private Task<MessageEnvelope?> RouteWorkflowAsync(
    MessageEnvelope envelope,
    DecompositionResult decomposition,
    CancellationToken cancellationToken)
{
    // Implemented in Task 10
    throw new NotImplementedException("Workflow routing not yet implemented");
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SkillDrivenAgentDecompositionTests"`
Expected: All single-task and escalation tests pass

Also run existing tests to verify backward compatibility:

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SkillDrivenAgentTests"`
Expected: All existing tests still pass (they don't pass `workflowTracker`, so `NullWorkflowTracker` is used)

**Step 6: Commit**

```bash
git add src/Cortex.Agents/SkillDrivenAgent.cs src/Cortex.Agents/Workflows/NullWorkflowTracker.cs tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs
git commit -m "feat(agents): parse DecompositionResult and single-task routing in SkillDrivenAgent"
```

---

### Task 10: SkillDrivenAgent — Multi-Task Workflow Path

**Files:**
- Modify: `src/Cortex.Agents/SkillDrivenAgent.cs` (replace `RouteWorkflowAsync` placeholder)
- Test: `tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs` (add multi-task tests)

**Step 1: Add multi-task tests to the existing test class**

Append these tests to `SkillDrivenAgentDecompositionTests`:

```csharp
// --- Multi-task decomposition ---

[Fact]
public async Task ProcessAsync_MultiTask_PublishesToMultipleAgents()
{
    RegisterDecomposeSkill();
    SetDecomposeResult(new
    {
        tasks = new[]
        {
            new { capability = "data-analysis", description = "Gather metrics", authorityTier = "JustDoIt" },
            new { capability = "drafting", description = "Write narrative", authorityTier = "DoItAndShowMe" }
        },
        summary = "Quarterly report",
        confidence = 0.9
    });
    await RegisterSpecialistAgent("analyst", "data-analysis");
    await RegisterSpecialistAgent("writer", "drafting");

    var routedToAnalyst = new TaskCompletionSource<MessageEnvelope>();
    var routedToWriter = new TaskCompletionSource<MessageEnvelope>();
    await _bus.StartConsumingAsync("agent.analyst", e =>
    {
        routedToAnalyst.SetResult(e);
        return Task.CompletedTask;
    });
    await _bus.StartConsumingAsync("agent.writer", e =>
    {
        routedToWriter.SetResult(e);
        return Task.CompletedTask;
    });

    var agent = CreateAgent();
    await agent.ProcessAsync(CreateEnvelope("Prepare quarterly report"));

    var analystMsg = await routedToAnalyst.Task.WaitAsync(TimeSpan.FromSeconds(5));
    var writerMsg = await routedToWriter.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.NotNull(analystMsg);
    Assert.NotNull(writerMsg);
}

[Fact]
public async Task ProcessAsync_MultiTask_CreatesWorkflowRecord()
{
    RegisterDecomposeSkill();
    SetDecomposeResult(new
    {
        tasks = new[]
        {
            new { capability = "data-analysis", description = "Gather metrics", authorityTier = "JustDoIt" },
            new { capability = "drafting", description = "Write narrative", authorityTier = "DoItAndShowMe" }
        },
        summary = "Quarterly report",
        confidence = 0.9
    });
    await RegisterSpecialistAgent("analyst", "data-analysis");
    await RegisterSpecialistAgent("writer", "drafting");

    await _bus.StartConsumingAsync("agent.analyst", _ => Task.CompletedTask);
    await _bus.StartConsumingAsync("agent.writer", _ => Task.CompletedTask);

    var agent = CreateAgent();
    await agent.ProcessAsync(CreateEnvelope("Prepare quarterly report"));

    // The workflow tracker should have a workflow with 2 subtask refs
    // We can't easily get the parent ref code, but we can check via the subtask refs
    // that were used in the delegation records
    var analystDelegations = await _delegationTracker.GetByAssigneeAsync("analyst");
    Assert.Single(analystDelegations);
    var subtaskRef = analystDelegations[0].ReferenceCode;

    var workflow = await _workflowTracker.FindBySubtaskAsync(subtaskRef);
    Assert.NotNull(workflow);
    Assert.Equal("Quarterly report", workflow.Summary);
    Assert.Equal(2, workflow.SubtaskReferenceCodes.Count);
}

[Fact]
public async Task ProcessAsync_MultiTask_CreatesDelegationPerSubtask()
{
    RegisterDecomposeSkill();
    SetDecomposeResult(new
    {
        tasks = new[]
        {
            new { capability = "data-analysis", description = "Gather metrics", authorityTier = "JustDoIt" },
            new { capability = "drafting", description = "Write narrative", authorityTier = "DoItAndShowMe" }
        },
        summary = "Quarterly report",
        confidence = 0.9
    });
    await RegisterSpecialistAgent("analyst", "data-analysis");
    await RegisterSpecialistAgent("writer", "drafting");

    await _bus.StartConsumingAsync("agent.analyst", _ => Task.CompletedTask);
    await _bus.StartConsumingAsync("agent.writer", _ => Task.CompletedTask);

    var agent = CreateAgent();
    await agent.ProcessAsync(CreateEnvelope("Prepare quarterly report"));

    var analystDelegations = await _delegationTracker.GetByAssigneeAsync("analyst");
    var writerDelegations = await _delegationTracker.GetByAssigneeAsync("writer");
    Assert.Single(analystDelegations);
    Assert.Single(writerDelegations);
    Assert.Equal("Gather metrics", analystDelegations[0].Description);
    Assert.Equal("Write narrative", writerDelegations[0].Description);
}

[Fact]
public async Task ProcessAsync_MultiTask_SetsReplyToCoS()
{
    RegisterDecomposeSkill();
    SetDecomposeResult(new
    {
        tasks = new[]
        {
            new { capability = "data-analysis", description = "Gather metrics", authorityTier = "JustDoIt" },
            new { capability = "drafting", description = "Write narrative", authorityTier = "DoItAndShowMe" }
        },
        summary = "Quarterly report",
        confidence = 0.9
    });
    await RegisterSpecialistAgent("analyst", "data-analysis");
    await RegisterSpecialistAgent("writer", "drafting");

    var routedToAnalyst = new TaskCompletionSource<MessageEnvelope>();
    await _bus.StartConsumingAsync("agent.analyst", e =>
    {
        routedToAnalyst.SetResult(e);
        return Task.CompletedTask;
    });
    await _bus.StartConsumingAsync("agent.writer", _ => Task.CompletedTask);

    var agent = CreateAgent();
    await agent.ProcessAsync(CreateEnvelope("Prepare quarterly report", replyTo: "agent.requester"));

    var msg = await routedToAnalyst.Task.WaitAsync(TimeSpan.FromSeconds(5));
    // Sub-task replies should come back to the CoS, not the original requester
    Assert.Equal("agent.cos", msg.Context.ReplyTo);
}

[Fact]
public async Task ProcessAsync_MultiTask_PartialCapabilityFailure_Escalates()
{
    RegisterDecomposeSkill();
    SetDecomposeResult(new
    {
        tasks = new[]
        {
            new { capability = "data-analysis", description = "Gather metrics", authorityTier = "JustDoIt" },
            new { capability = "nonexistent", description = "Unknown task", authorityTier = "JustDoIt" }
        },
        summary = "Mixed report",
        confidence = 0.9
    });
    await RegisterSpecialistAgent("analyst", "data-analysis");
    // Don't register agent for "nonexistent"

    var escalated = new TaskCompletionSource<MessageEnvelope>();
    await _bus.StartConsumingAsync("agent.founder", e =>
    {
        escalated.SetResult(e);
        return Task.CompletedTask;
    });

    var agent = CreateAgent();
    await agent.ProcessAsync(CreateEnvelope("Mixed request"));

    var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.NotNull(msg);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SkillDrivenAgentDecompositionTests&FullyQualifiedName~MultiTask"`
Expected: FAIL — `RouteWorkflowAsync` throws `NotImplementedException`

**Step 3: Implement RouteWorkflowAsync**

Replace the placeholder in `SkillDrivenAgent.cs`:

```csharp
private async Task<MessageEnvelope?> RouteWorkflowAsync(
    MessageEnvelope envelope,
    DecompositionResult decomposition,
    CancellationToken cancellationToken)
{
    var maxInbound = GetMaxAuthorityTier(envelope);
    var parentRefCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
    var subtaskRefCodes = new List<ReferenceCode>();

    // Pre-validate: ensure all capabilities have agents before creating any delegations
    foreach (var task in decomposition.Tasks)
    {
        var candidates = await _agentRegistry.FindByCapabilityAsync(task.Capability, cancellationToken);
        var filtered = candidates.Where(a => a.AgentId != AgentId).ToList();

        if (filtered.Count == 0)
        {
            await EscalateAsync(
                envelope,
                $"Cannot decompose: no agent with capability '{task.Capability}'",
                cancellationToken);
            return null;
        }
    }

    // All capabilities valid — create delegations and publish
    foreach (var task in decomposition.Tasks)
    {
        if (!Enum.TryParse<AuthorityTier>(task.AuthorityTier, ignoreCase: true, out var taskAuthority))
        {
            taskAuthority = AuthorityTier.JustDoIt;
        }

        var effectiveTier = (AuthorityTier)Math.Min((int)taskAuthority, (int)maxInbound);

        var candidates = await _agentRegistry.FindByCapabilityAsync(task.Capability, cancellationToken);
        var target = candidates.First(a => a.AgentId != AgentId);

        var childRefCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
        subtaskRefCodes.Add(childRefCode);

        await _delegationTracker.DelegateAsync(new DelegationRecord
        {
            ReferenceCode = childRefCode,
            DelegatedBy = AgentId,
            DelegatedTo = target.AgentId,
            Description = task.Description,
            Status = DelegationStatus.Assigned,
            AssignedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        var childEnvelope = envelope with
        {
            ReferenceCode = childRefCode,
            AuthorityClaims =
            [
                new AuthorityClaim
                {
                    GrantedBy = AgentId,
                    GrantedTo = target.AgentId,
                    Tier = effectiveTier,
                    GrantedAt = DateTimeOffset.UtcNow
                }
            ],
            Context = envelope.Context with
            {
                ParentMessageId = envelope.Message.MessageId,
                FromAgentId = AgentId,
                ReplyTo = $"agent.{AgentId}",
                OriginalGoal = decomposition.Summary
            }
        };

        await _messagePublisher.PublishAsync(childEnvelope, $"agent.{target.AgentId}", cancellationToken);

        _logger.LogInformation(
            "Workflow {ParentRef}: dispatched {ChildRef} to {Target} (capability: {Capability})",
            parentRefCode, childRefCode, target.AgentId, task.Capability);
    }

    // Create workflow record
    var workflow = new WorkflowRecord
    {
        ReferenceCode = parentRefCode,
        OriginalEnvelope = envelope,
        SubtaskReferenceCodes = subtaskRefCodes,
        Summary = decomposition.Summary
    };
    await _workflowTracker.CreateAsync(workflow, cancellationToken);

    _logger.LogInformation(
        "Created workflow {ParentRef} with {Count} sub-tasks",
        parentRefCode, subtaskRefCodes.Count);

    return null;
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SkillDrivenAgentDecompositionTests"`
Expected: All tests pass

Run full test suite:

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass

**Step 5: Commit**

```bash
git add src/Cortex.Agents/SkillDrivenAgent.cs tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs
git commit -m "feat(agents): add multi-task workflow routing in SkillDrivenAgent"
```

---

### Task 11: SkillDrivenAgent — Aggregation Path

**Files:**
- Modify: `src/Cortex.Agents/SkillDrivenAgent.cs` (add aggregation check at start of ProcessAsync)
- Test: `tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs` (add aggregation tests)

**Step 1: Add aggregation tests**

Append to `SkillDrivenAgentDecompositionTests`:

```csharp
// --- Aggregation path ---

[Fact]
public async Task ProcessAsync_SubtaskReply_StoresResultAndWaits()
{
    // Set up a workflow with 2 subtasks manually
    var parentRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 50);
    var childRef1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 51);
    var childRef2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 52);

    var originalEnvelope = CreateEnvelope("Original goal", replyTo: "agent.requester");
    await _workflowTracker.CreateAsync(new WorkflowRecord
    {
        ReferenceCode = parentRef,
        OriginalEnvelope = originalEnvelope,
        SubtaskReferenceCodes = [childRef1, childRef2],
        Summary = "Test workflow"
    });

    // Simulate first sub-task reply arriving
    RegisterDecomposeSkill(); // Needed so pipeline doesn't error
    var reply1 = new MessageEnvelope
    {
        Message = new TestMessage { Content = "Metrics gathered" },
        ReferenceCode = childRef1,
        Context = new MessageContext { FromAgentId = "analyst" }
    };

    var agent = CreateAgent();
    var result = await agent.ProcessAsync(reply1);

    // Should return null (waiting for child2)
    Assert.Null(result);

    // Result should be stored
    Assert.False(await _workflowTracker.AllSubtasksCompleteAsync(parentRef));
}

[Fact]
public async Task ProcessAsync_AllSubtasksComplete_AssemblesAndPublishes()
{
    // Set up a workflow with 2 subtasks
    var parentRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 60);
    var childRef1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 61);
    var childRef2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 62);

    var originalEnvelope = CreateEnvelope("Original goal", replyTo: "agent.requester");
    await _workflowTracker.CreateAsync(new WorkflowRecord
    {
        ReferenceCode = parentRef,
        OriginalEnvelope = originalEnvelope,
        SubtaskReferenceCodes = [childRef1, childRef2],
        Summary = "Quarterly report"
    });

    // Store first result directly
    await _workflowTracker.StoreSubtaskResultAsync(childRef1, new MessageEnvelope
    {
        Message = new TestMessage { Content = "Metrics gathered" },
        ReferenceCode = childRef1,
        Context = new MessageContext { FromAgentId = "analyst" }
    });

    // Set up consumer for final assembled result
    var assembledResult = new TaskCompletionSource<MessageEnvelope>();
    await _bus.StartConsumingAsync("agent.requester", e =>
    {
        assembledResult.SetResult(e);
        return Task.CompletedTask;
    });

    // Simulate second sub-task reply — this should trigger assembly
    RegisterDecomposeSkill();
    var reply2 = new MessageEnvelope
    {
        Message = new TestMessage { Content = "Narrative written" },
        ReferenceCode = childRef2,
        Context = new MessageContext { FromAgentId = "writer" }
    };

    var agent = CreateAgent();
    await agent.ProcessAsync(reply2);

    var assembled = await assembledResult.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.NotNull(assembled);
    Assert.Equal(parentRef, assembled.ReferenceCode);
}

[Fact]
public async Task ProcessAsync_AllSubtasksComplete_AssembledResultContainsAllContent()
{
    var parentRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 70);
    var childRef1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 71);
    var childRef2 = ReferenceCode.Create(DateTimeOffset.UtcNow, 72);

    var originalEnvelope = CreateEnvelope("Original goal", replyTo: "agent.requester");
    await _workflowTracker.CreateAsync(new WorkflowRecord
    {
        ReferenceCode = parentRef,
        OriginalEnvelope = originalEnvelope,
        SubtaskReferenceCodes = [childRef1, childRef2],
        Summary = "Quarterly report"
    });

    await _workflowTracker.StoreSubtaskResultAsync(childRef1, new MessageEnvelope
    {
        Message = new TestMessage { Content = "Metrics gathered" },
        ReferenceCode = childRef1,
        Context = new MessageContext { FromAgentId = "analyst" }
    });

    var assembledResult = new TaskCompletionSource<MessageEnvelope>();
    await _bus.StartConsumingAsync("agent.requester", e =>
    {
        assembledResult.SetResult(e);
        return Task.CompletedTask;
    });

    RegisterDecomposeSkill();
    var reply2 = new MessageEnvelope
    {
        Message = new TestMessage { Content = "Narrative written" },
        ReferenceCode = childRef2,
        Context = new MessageContext { FromAgentId = "writer" }
    };

    var agent = CreateAgent();
    await agent.ProcessAsync(reply2);

    var assembled = await assembledResult.Task.WaitAsync(TimeSpan.FromSeconds(5));

    // The assembled message content should contain both sub-task results
    var content = ((TestMessage)assembled.Message).Content;
    Assert.Contains("Metrics gathered", content);
    Assert.Contains("Narrative written", content);
}

[Fact]
public async Task ProcessAsync_AllSubtasksComplete_WorkflowMarkedCompleted()
{
    var parentRef = ReferenceCode.Create(DateTimeOffset.UtcNow, 80);
    var childRef1 = ReferenceCode.Create(DateTimeOffset.UtcNow, 81);

    var originalEnvelope = CreateEnvelope("Goal", replyTo: "agent.requester");
    await _workflowTracker.CreateAsync(new WorkflowRecord
    {
        ReferenceCode = parentRef,
        OriginalEnvelope = originalEnvelope,
        SubtaskReferenceCodes = [childRef1],
        Summary = "Single-subtask workflow"
    });

    await _bus.StartConsumingAsync("agent.requester", _ => Task.CompletedTask);

    RegisterDecomposeSkill();
    var reply = new MessageEnvelope
    {
        Message = new TestMessage { Content = "Done" },
        ReferenceCode = childRef1,
        Context = new MessageContext { FromAgentId = "worker" }
    };

    var agent = CreateAgent();
    await agent.ProcessAsync(reply);

    var workflow = await _workflowTracker.GetAsync(parentRef);
    Assert.NotNull(workflow);
    Assert.Equal(WorkflowStatus.Completed, workflow.Status);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SkillDrivenAgentDecompositionTests&FullyQualifiedName~SubtaskReply or FullyQualifiedName~AllSubtasksComplete"`
Expected: FAIL — no aggregation logic yet

**Step 3: Add aggregation check at the start of ProcessAsync**

At the top of `ProcessAsync`, before the pipeline runs, add:

```csharp
// Check if this is a sub-task reply for a pending workflow
var workflow = await _workflowTracker.FindBySubtaskAsync(envelope.ReferenceCode, cancellationToken);
if (workflow is not null)
{
    return await HandleSubtaskReplyAsync(envelope, workflow, cancellationToken);
}
```

Then add the handler method:

```csharp
private async Task<MessageEnvelope?> HandleSubtaskReplyAsync(
    MessageEnvelope subtaskReply,
    WorkflowRecord workflow,
    CancellationToken cancellationToken)
{
    _logger.LogInformation(
        "Workflow {ParentRef}: received sub-task result {ChildRef}",
        workflow.ReferenceCode, subtaskReply.ReferenceCode);

    // Store the result
    await _workflowTracker.StoreSubtaskResultAsync(
        subtaskReply.ReferenceCode, subtaskReply, cancellationToken);

    // Update delegation status
    await _delegationTracker.UpdateStatusAsync(
        subtaskReply.ReferenceCode, DelegationStatus.Complete, cancellationToken);

    // Check if all sub-tasks are done
    if (!await _workflowTracker.AllSubtasksCompleteAsync(workflow.ReferenceCode, cancellationToken))
    {
        _logger.LogInformation(
            "Workflow {ParentRef}: waiting for more sub-tasks",
            workflow.ReferenceCode);
        return null;
    }

    // All complete — assemble result
    var results = await _workflowTracker.GetCompletedResultsAsync(
        workflow.ReferenceCode, cancellationToken);

    var assembledContent = AssembleResults(workflow, results);

    var assembledEnvelope = new MessageEnvelope
    {
        Message = new TestMessage { Content = assembledContent },
        ReferenceCode = workflow.ReferenceCode,
        Context = new MessageContext
        {
            ParentMessageId = workflow.OriginalEnvelope.Message.MessageId,
            FromAgentId = AgentId,
            ReplyTo = workflow.OriginalEnvelope.Context.ReplyTo
        }
    };

    // Publish to original requester
    if (workflow.OriginalEnvelope.Context.ReplyTo is not null)
    {
        await _messagePublisher.PublishAsync(
            assembledEnvelope,
            workflow.OriginalEnvelope.Context.ReplyTo,
            cancellationToken);
    }

    // Mark workflow as completed
    await _workflowTracker.UpdateStatusAsync(
        workflow.ReferenceCode, WorkflowStatus.Completed, cancellationToken);

    _logger.LogInformation(
        "Workflow {ParentRef}: completed, assembled result published to {ReplyTo}",
        workflow.ReferenceCode, workflow.OriginalEnvelope.Context.ReplyTo);

    return null;
}

private static string AssembleResults(
    WorkflowRecord workflow,
    IReadOnlyDictionary<ReferenceCode, MessageEnvelope> results)
{
    var builder = new System.Text.StringBuilder();
    builder.AppendLine($"# {workflow.Summary}");
    builder.AppendLine();

    foreach (var subtaskRef in workflow.SubtaskReferenceCodes)
    {
        if (results.TryGetValue(subtaskRef, out var result))
        {
            var content = result.Message switch
            {
                TestMessage tm => tm.Content,
                _ => result.Message.ToString() ?? string.Empty
            };
            builder.AppendLine($"## {subtaskRef}");
            builder.AppendLine(content);
            builder.AppendLine();
        }
    }

    return builder.ToString().TrimEnd();
}
```

**Important note on `AssembleResults`:** The method above references `TestMessage` directly, which couples the agent to a test type. In production, you would use a proper message content extraction pattern. For this phase, since all messages in tests are `TestMessage`, this is acceptable. When a real `IMessage` content contract is established, update this method.

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal --filter "FullyQualifiedName~SkillDrivenAgentDecompositionTests"`
Expected: All tests pass

Run full test suite:

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass

**Step 5: Commit**

```bash
git add src/Cortex.Agents/SkillDrivenAgent.cs tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs
git commit -m "feat(agents): add sub-task aggregation path in SkillDrivenAgent"
```

---

### Task 12: Full Build and Test Verification

**Step 1: Build the entire solution**

Run: `dotnet build --configuration Release`
Expected: Build succeeded, 0 warnings (warnings are errors)

**Step 2: Run all unit tests**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass, 0 failures

**Step 3: Commit any remaining changes**

If any cleanup was needed:

```bash
git add -A
git commit -m "chore: final cleanup for multi-task decomposition (#26)"
```
