# Multi-Task Decomposition Design

Issue: #26

## Problem

The CoS (`SkillDrivenAgent`) performs 1:1 routing — one inbound message maps to one delegation. Goals like "prepare a quarterly report" require research, drafting, and formatting by different specialists. The CoS needs to decompose a goal into N independent sub-tasks, delegate each to the right agent, collect the results, and assemble a single response.

## Design Decisions

### Sub-task reply detection — convention-based, single queue

When a reply arrives at the CoS inbox, it checks whether the reply's `ReferenceCode` belongs to a pending workflow via `IWorkflowTracker.FindBySubtaskAsync`. If yes, it's a sub-task reply routed to the aggregation path. If no, it's a new message routed to the triage/decompose pipeline.

This keeps the "one agent, one inbox" convention and avoids new queue topology. The CoS is a routing agent — distinguishing new work from returning results is exactly what it does.

### Workflow tracking — separate `WorkflowRecord`, not extended `DelegationRecord`

A workflow is a distinct concept from a delegation. A delegation is "agent A asked agent B to do X." A workflow is "one goal decomposed into N delegations, with a join condition." The workflow record holds state that doesn't belong on individual delegations — the original envelope, the list of child reference codes, and the assembled results.

This follows the existing pattern of small, focused interfaces (`IDelegationTracker`, `IContextProvider`).

### Skill design — `cos-decompose` replaces `cos-triage`

A single skill handles both simple routing and complex decomposition. It produces a `DecompositionResult` with a `Tasks` list. One task = 1:1 routing (backward compatible with today's behaviour). Multiple tasks = workflow path. One LLM call regardless of complexity.

### Result assembly — mechanical, not LLM-powered

When all sub-tasks complete, the CoS concatenates results into sections (one per sub-task) prefixed with the workflow summary. No LLM re-interpretation — the original requester asked one question and gets one structured answer.

### No task dependencies in this scope

All sub-tasks are independent and parallel. Sequential dependencies (`dependsOn`) are a Phase 2 concern (#11 — task dependency DAG).

## Data Model

### New types in `Cortex.Core.Workflows`

```csharp
/// <summary>Tracks a decomposed goal as a coordinated unit of work.</summary>
public record WorkflowRecord
{
    /// <summary>Parent reference code for the entire workflow.</summary>
    public required ReferenceCode ReferenceCode { get; init; }

    /// <summary>The original inbound envelope, preserved for ReplyTo and context.</summary>
    public required MessageEnvelope OriginalEnvelope { get; init; }

    /// <summary>Reference codes for each sub-task in the workflow.</summary>
    public required IReadOnlyList<ReferenceCode> SubtaskReferenceCodes { get; init; }

    /// <summary>Human-readable summary of the decomposed goal.</summary>
    public required string Summary { get; init; }

    /// <summary>Current status of the workflow.</summary>
    public WorkflowStatus Status { get; init; } = WorkflowStatus.InProgress;

    /// <summary>When the workflow was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When the workflow completed, if applicable.</summary>
    public DateTimeOffset? CompletedAt { get; init; }
}

public enum WorkflowStatus { InProgress, Completed, Failed }
```

### `IWorkflowTracker` interface

```csharp
/// <summary>Manages workflow lifecycle for multi-task decomposition.</summary>
public interface IWorkflowTracker
{
    Task CreateAsync(WorkflowRecord workflow, CancellationToken cancellationToken = default);
    Task<WorkflowRecord?> FindBySubtaskAsync(ReferenceCode subtaskRefCode, CancellationToken cancellationToken = default);
    Task<WorkflowRecord?> GetAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(ReferenceCode workflowRefCode, WorkflowStatus status, CancellationToken cancellationToken = default);
}
```

### `InMemoryWorkflowTracker`

`ConcurrentDictionary`-backed implementation. Internally tracks mutable state for partial result collection:

```csharp
internal class WorkflowState
{
    public WorkflowRecord Record { get; set; }
    public Dictionary<ReferenceCode, MessageEnvelope> CompletedResults { get; } = new();
}
```

The `WorkflowRecord` stays immutable. The tracker holds mutable state internally — same pattern as `InMemoryDelegationTracker`.

Additional methods exposed for aggregation:

```csharp
Task StoreSubtaskResultAsync(ReferenceCode subtaskRefCode, MessageEnvelope result, CancellationToken cancellationToken = default);
Task<IReadOnlyDictionary<ReferenceCode, MessageEnvelope>> GetCompletedResultsAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default);
Task<bool> AllSubtasksCompleteAsync(ReferenceCode workflowRefCode, CancellationToken cancellationToken = default);
```

### `DecompositionResult` — new pipeline output type

```csharp
/// <summary>Result of the cos-decompose skill — single or multi-task.</summary>
public record DecompositionResult
{
    public required IReadOnlyList<DecompositionTask> Tasks { get; init; }
    public required string Summary { get; init; }
    public required double Confidence { get; init; }
}

/// <summary>A single routable sub-task within a decomposition.</summary>
public record DecompositionTask
{
    public required string Capability { get; init; }
    public required string Description { get; init; }
    public required string AuthorityTier { get; init; }
}
```

When `Tasks.Count == 1`, this is equivalent to today's `TriageResult`. The `SkillDrivenAgent` checks the count to decide which path to take.

## Message Flow

### Triage/decompose path (new messages)

```
Message arrives at CoS inbox
    |
    v
FindBySubtaskAsync(envelope.ReferenceCode) -> not found -> new message
    |
    v
Run skill pipeline: cos-context-query -> cos-decompose
    |
    v
Parse DecompositionResult
    |
    +-- Low confidence? -> escalation (unchanged)
    |
    +-- Tasks.Count == 1? -> existing 1:1 routing (unchanged logic)
    |
    +-- Tasks.Count > 1? -> workflow path:
            |
            v
        Generate parent ReferenceCode
            |
            v
        For each DecompositionTask:
            +-- Generate child ReferenceCode
            +-- Find agent by capability
            +-- Create DelegationRecord
            +-- Build child envelope:
            |     ReferenceCode = child ref code
            |     ReplyTo = "agent.cos"
            |     ParentMessageId = original message ID
            |     AuthorityClaims narrowed to task's tier
            |     OriginalGoal = original message content
            +-- Publish to agent.{targetAgentId}
            |
            v
        Create WorkflowRecord (parent ref, original envelope, child ref codes)
        Store via IWorkflowTracker.CreateAsync()
        Return null from ProcessAsync
```

### Aggregation path (sub-task replies)

```
Reply arrives at CoS inbox
    |
    v
FindBySubtaskAsync(envelope.ReferenceCode) -> found -> sub-task reply
    |
    v
Update DelegationRecord status -> Completed
Store sub-task result via StoreSubtaskResultAsync()
    |
    v
AllSubtasksCompleteAsync()?
    |
    +-- No -> return null (wait for more)
    |
    +-- Yes:
            |
            v
        Assemble combined result:
            "## [capability]: [description]\n[result content]"
            per sub-task, prefixed with workflow summary
            |
            v
        Build final response envelope:
            ReferenceCode = parent ref code
            ReplyTo = original envelope's ReplyTo
            |
            v
        Update WorkflowRecord status -> Completed
        Publish to original requester's queue
```

### ReplyTo handling

Today the CoS sets `ReplyTo` to the original sender's queue so the specialist replies directly. In the workflow path, `ReplyTo` is set to `agent.cos` instead — the CoS collects all replies before assembling and forwarding.

## Skill Definition

### `cos-decompose` (`skills/cos-decompose.md`)

Replaces `cos-triage` in the CoS persona pipeline. Single LLM call that decides whether a goal is simple (route to one agent) or complex (decompose into sub-tasks).

Output shape:

```json
{
  "tasks": [
    {
      "capability": "capability-name",
      "description": "what this sub-task should accomplish",
      "authorityTier": "JustDoIt | DoItAndShowMe | AskMeFirst"
    }
  ],
  "summary": "brief description of the overall goal",
  "confidence": 0.0-1.0
}
```

Constraints:
- Never invent capabilities not in `availableCapabilities`
- If unsure how to decompose, return low confidence for escalation
- Tasks are independent and parallel — no ordering

### Pipeline change

```
Before: cos-context-query -> cos-triage
After:  cos-context-query -> cos-decompose
```

`cos-triage` stays in the repo for other agents. The CoS pipeline references `cos-decompose`.

## Error Handling

- **Sub-task fails**: mark delegation as `Failed`, mark workflow as `Failed`, publish error response to original requester listing what succeeded and what failed
- **No agent found for a capability**: fail that sub-task at decomposition time before creating the workflow, escalate the whole goal
- **Sub-task timeout**: out of scope — handled by future delegation supervision (#28)

## Changes Summary

| Action | File | Why |
|--------|------|-----|
| Add | `Cortex.Core/Workflows/WorkflowRecord.cs` | New type |
| Add | `Cortex.Core/Workflows/WorkflowStatus.cs` | New enum |
| Add | `Cortex.Core/Workflows/IWorkflowTracker.cs` | New interface |
| Add | `Cortex.Agents/Workflows/InMemoryWorkflowTracker.cs` | Implementation |
| Add | `Cortex.Agents/Pipeline/DecompositionResult.cs` | New pipeline output type |
| Add | `skills/cos-decompose.md` | New skill definition |
| Modify | `Cortex.Agents/SkillDrivenAgent.cs` | Add workflow branch + aggregation |
| Add | `tests/Cortex.Core.Tests/Workflows/` | WorkflowRecord tests |
| Add | `tests/Cortex.Agents.Tests/Workflows/` | InMemoryWorkflowTracker tests |
| Add | `tests/Cortex.Agents.Tests/SkillDrivenAgentDecompositionTests.cs` | Decomposition + aggregation tests |

### Files NOT changed

- `IAgent.cs` — no signature change
- `AgentHarness.cs` — no change
- `MessageEnvelope.cs` — no change
- `DelegationRecord.cs` — no change
- `IDelegationTracker.cs` — no change
- `SkillPipelineRunner.cs` — no change
