# Agent Orchestration Patterns — Research Synthesis

**Date:** 2026-02-23
**Purpose:** Analyse external multi-agent orchestration patterns and map them to Cortex's architecture before implementing issue #2 (agent harness).

> **Part of the [Multi-Agent Orchestration Research Corpus](./README.md).** This is the foundation document. For deeper framework analysis see [Deep Research](./2026-02-24-agent-orchestration-deep-research.md) and [Swarm Frameworks](./2026-02-24-agent-swarm-frameworks.md). For team composition see [Team-Building Agents](./2026-02-24-team-building-agents.md).

## Sources

- [Claude Code Swarm Orchestration Skill](https://gist.github.com/kieranklaassen/4f2aba89594a4aea4ad64d753984b2ea) — Kieran Klaassen
- [Claude Code's Hidden Multi-Agent System](https://paddo.dev/blog/claude-code-hidden-swarm/) — paddo.dev
- Community-sourced meta-orchestration prompt patterns (agent-organiser, multi-agent-coordinator, error-coordinator, workflow-orchestrator)

---

## 1. Research Summary

### 1.1 Core Primitives (Claude Code TeammateTool)

The TeammateTool system, found inside Claude Code's binary (feature-flagged off), defines **7 primitives** and **13 operations**:

| Primitive | Description |
|-----------|-------------|
| **Agent** | A running instance with tools and identity |
| **Team** | Named group with one leader + N teammates |
| **Teammate** | Agent joined to a team; has name, inbox |
| **Leader** | Team creator; approves plans, manages lifecycle |
| **Task** | Work item with subject, status, owner, dependencies |
| **Inbox** | Per-agent message queue (file-based in Claude Code) |
| **Message** | JSON payload — plain text or 9 structured types |

### 1.2 Orchestration Patterns Identified

Six patterns emerge from the research:

| Pattern | Structure | Use Case |
|---------|-----------|----------|
| **Parallel Specialists (Council)** | Leader + N specialists reviewing concurrently | Code review, security audit, architecture review |
| **Sequential Pipeline** | Tasks with linear `blockedBy` dependencies | Research → Plan → Implement → Test → Deploy |
| **Self-Organising Swarm** | Workers poll shared task pool, claim work | File reviews, parallel test runs, distributed analysis |
| **Research + Implementation** | Sync research feeds async implementation | Investigation feeding development decisions |
| **Plan Approval Gating** | Architect submits plan; leader approves/rejects | Architecture design review before coding |
| **Coordinated Refactoring** | Parallel file work with join point for validation | Distributed changes with test gate |

### 1.3 Task System

Tasks have dependency management via `blockedBy`/`blocks`:
- Auto-unblock when all blockers complete
- Status: `pending` → `in_progress` → `completed`
- Owner field for claiming tasks
- Multiple dependencies per task (fork-join)

### 1.4 Communication Protocol

**9 structured message types:**
`shutdown_request`, `shutdown_approved`, `idle_notification`, `task_completed`, `plan_approval_request`, `join_request`, `permission_request`, regular text, broadcast.

**Two messaging modes:**
- `write` — direct to specific agent inbox
- `broadcast` — to all team members (N messages)

### 1.5 Error Coordination Patterns

- **Circuit breakers**: closed → open → half-open state machine
- **Bulkhead isolation**: contain failures within service boundaries
- **Cascade prevention**: rate limiting, backpressure, load shedding
- **Recovery strategies**: immediate retry, delayed retry, alternative path, cached fallback, manual intervention
- **Error taxonomy**: infrastructure, application, integration, data, timeout, permission, resource exhaustion, external

### 1.6 Multi-Agent Coordination Patterns

- **DAG execution** with topological sorting
- **Saga patterns** with compensation logic
- **Checkpoint/restart** for workflow resumption
- **Barrier coordination** (fork-join synchronisation points)
- **Backpressure handling** via queue management and connection pooling

---

## 2. Mapping to Cortex Architecture

### 2.1 What Cortex Already Has

Cortex's existing design is remarkably well-aligned with these patterns:

| Research Concept | Cortex Equivalent | Status |
|-----------------|-------------------|--------|
| Agent | `IAgent` | ✅ Exists |
| Team | `ITeam` with `TeamStatus` (Assembling, Active, Dissolving, Complete) | ✅ Exists |
| Teammate membership | `ITeam.MemberIds` | ✅ Exists |
| Agent inbox | RabbitMQ queue `agent.{agentId}` | ✅ Designed |
| Direct message | `IMessageBus.PublishAsync(envelope, queueName)` | ✅ Exists |
| Task assignment | `IDelegationTracker.DelegateAsync` | ✅ Exists |
| Task status | `DelegationStatus` (Assigned, InProgress, AwaitingReview, Complete, Overdue) | ✅ Exists |
| Agent registry | `IAgentRegistry` with capability-based lookup | ✅ Exists |
| Team context | `MessageContext.TeamId` | ✅ Exists |
| Team channel | `ChannelType.Team` — ephemeral channel for team goal | ✅ Exists |
| Plan approval | `AuthorityTier.AskMeFirst` — must get approval first | ✅ Exists |
| Execute and report | `AuthorityTier.DoItAndShowMe` | ✅ Exists |
| Autonomous execution | `AuthorityTier.JustDoIt` | ✅ Exists |
| Message priority | `MessagePriority` (Low, Normal, High, Critical) | ✅ Exists |
| SLA enforcement | `MessageEnvelope.Sla` (TimeSpan) | ✅ Exists |
| Dead letter handling | RabbitMQ dead letter exchange + queue | ✅ Exists |
| Agent capabilities | `AgentCapability` (Name, Description, SkillIds) | ✅ Exists |
| Reference tracking | `ReferenceCode` (CTX-YYYY-MMDD-NNN) | ✅ Exists |

### 2.2 Key Insight: Authority Model IS Plan Approval

The research describes a plan approval workflow: teammate submits plan → leader approves/rejects. Cortex already has this through its authority tiers:

| Authority Tier | Research Equivalent | Behaviour |
|---------------|-------------------|-----------|
| `JustDoIt` | Autonomous agent | Execute without approval |
| `DoItAndShowMe` | Execute + report | Prepare, execute, present results |
| `AskMeFirst` | Plan approval gating | Must get approval before executing |

This means we don't need a separate approval system — the authority model on `MessageEnvelope.AuthorityClaims` already encodes this pattern. An agent receiving a message with `AskMeFirst` authority knows to propose a plan and await approval before acting.

### 2.3 Key Insight: RabbitMQ > File-Based Inboxes

The research patterns use file-based inboxes with polling. Cortex uses RabbitMQ queues, which is strictly superior:

| Capability | File-Based (Research) | RabbitMQ (Cortex) |
|-----------|----------------------|-------------------|
| Message delivery | Poll files periodically | Push-based, instant |
| Broadcast | N individual file writes | Fanout exchange (atomic) |
| Swarm pattern | Poll task list + claim race | Competing consumers on shared queue |
| Durability | File system | Message persistence + acknowledgements |
| Dead letters | Manual error tracking | Automatic dead letter routing |
| Backpressure | None | Consumer prefetch count |
| Team routing | Custom routing logic | Topic exchange patterns |

### 2.4 Pattern Implementation via RabbitMQ

| Orchestration Pattern | RabbitMQ Implementation |
|----------------------|------------------------|
| **Direct message** | Publish to `agent.{targetId}` queue |
| **Broadcast to team** | Fanout exchange `team.{teamId}` bound to each member's queue |
| **Swarm (competing consumers)** | Multiple agents consume from single shared queue `team.{teamId}.work` |
| **Pipeline** | Chain via `ReplyTo` — each stage publishes to next stage's queue |
| **Council (parallel specialists)** | Publish same message to N specialist queues; collect replies |
| **Plan approval** | Authority tier on message + `ReplyTo` for approval response |

---

## 3. Gaps — What the Current Design is Missing

### 3.1 Must Address in Issue #2 (Foundation)

**Gap A: Per-consumer lifecycle management**

The current `IMessageConsumer.StopConsumingAsync()` stops ALL consumers on the bus. When multiple `AgentHarness` instances share one `IMessageBus`, stopping one agent would stop all of them. The harness needs a way to stop only its own consumer.

Options:
1. `StartConsumingAsync` returns a handle (`IAsyncDisposable`) for stopping individual consumers
2. `StopConsumingAsync` takes a queue name parameter
3. Each harness gets its own `IMessageConsumer` instance

**Recommendation:** Option 1 — return `IAsyncDisposable`. Clean, composable, no API breaking change needed.

**Gap B: Sender identity on messages**

Messages currently carry no `FromAgentId`. Every coordination pattern requires knowing who sent a message — for reply routing, for delegation tracking, for audit trails.

**Recommendation:** Add `FromAgentId` to `MessageContext`.

**Gap C: Team-aware agent startup**

`IAgentRuntime.StartAgentAsync(IAgent)` has no concept of starting an agent as part of a team. For team orchestration, we need to associate agents with teams at startup time.

**Recommendation:** Add optional `TeamContext` parameter or overload.

### 3.2 Document Now, Build Later (Phase 2+)

**Gap D: Task dependency graphs**

`DelegationTracker` is flat — no `blockedBy`/`blocks` relationships. The research shows task dependency management is critical for pipeline and coordinated refactoring patterns.

**Future:** Extend `DelegationRecord` with `BlockedBy` collection. Add `FindBlockedAsync()` and auto-unblock logic.

**Gap E: Broadcast messaging**

No built-in broadcast mechanism. Individual point-to-point only.

**Future:** Team fanout exchange in RabbitMQ topology. `IMessageBus.BroadcastAsync(envelope, teamId)`.

**Gap F: Structured message types**

Messages are typed via `IMessage` implementations, but there's no convention for coordination messages (shutdown requests, task completion notifications, etc.).

**Future:** Define `Cortex.Core.Messages.Coordination` namespace with standard coordination message types.

**Gap G: Circuit breakers and error coordination**

Only basic nack-to-dead-letter. No circuit breaker state machine, no cascade prevention.

**Future:** `ICircuitBreaker` on agent harness, configurable failure thresholds.

**Gap H: Competing consumers (swarm pattern)**

Current design assumes one agent per queue. Swarm pattern needs multiple agents consuming from a shared work queue.

**Future:** Support shared queue binding in `QueueTopology`. Already possible with RabbitMQ — just bind multiple consumers to the same queue.

---

## 4. Recommended Changes to Issue #2 Design

### 4.1 Add to MessageContext (Minimal, Non-Breaking)

```csharp
public sealed record MessageContext
{
    public string? ParentMessageId { get; init; }
    public string? OriginalGoal { get; init; }
    public string? TeamId { get; init; }         // ✅ already exists
    public string? ChannelId { get; init; }
    public string? ReplyTo { get; init; }         // from current plan
    public string? FromAgentId { get; init; }     // NEW: sender identity
}
```

### 4.2 Change Consumer Lifecycle (IMessageConsumer)

```csharp
public interface IMessageConsumer
{
    /// <summary>
    /// Starts consuming from the specified queue. Returns a handle
    /// that can be disposed to stop only this consumer.
    /// </summary>
    Task<IAsyncDisposable> StartConsumingAsync(
        string queueName,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all consumers managed by this instance.
    /// </summary>
    Task StopConsumingAsync(CancellationToken cancellationToken = default);
}
```

This is a **breaking change** to `IMessageConsumer` — `StartConsumingAsync` returns `IAsyncDisposable` instead of `Task`. Both `InMemoryMessageBus` and `RabbitMqMessageBus` need updating. However, we're pre-1.0 and this is foundational correctness.

### 4.3 Enhance IAgentRuntime with Team Support

```csharp
public interface IAgentRuntime
{
    Task<string> StartAgentAsync(
        IAgent agent,
        CancellationToken cancellationToken = default);

    Task<string> StartAgentAsync(
        IAgent agent,
        string teamId,                              // NEW: team association
        CancellationToken cancellationToken = default);

    Task StopAgentAsync(string agentId, CancellationToken cancellationToken = default);

    Task StopTeamAsync(string teamId, CancellationToken cancellationToken = default);  // NEW

    IReadOnlyList<string> RunningAgentIds { get; }

    IReadOnlyList<string> GetTeamAgentIds(string teamId);  // NEW
}
```

### 4.4 AgentHarness Sets FromAgentId

When dispatching a reply, the harness should stamp `FromAgentId`:

```csharp
var replyEnvelope = response with
{
    ReferenceCode = envelope.ReferenceCode,
    Context = response.Context with
    {
        ParentMessageId = envelope.Message.MessageId,
        FromAgentId = _agent.AgentId    // stamp sender
    }
};
```

### 4.5 No Other Changes to Issue #2

Everything else in the current plan (InMemoryAgentRegistry, InMemoryDelegationTracker, EchoAgent, AgentHarness, AgentRuntime, DI extensions) remains correct. The harness architecture maps directly to the research's "teammate with inbox" pattern. The runtime maps to "team management."

---

## 5. Future Issue Roadmap (Informed by Research)

These are separate issues for Phase 2+, but informed by this research:

| Issue | Description | Patterns Enabled |
|-------|-------------|-----------------|
| **Task Dependencies** | Add `BlockedBy`/`Blocks` to delegation records, auto-unblock logic | Pipeline, Coordinated Refactoring |
| **Team Lifecycle** | Implement `ITeam` management — assemble, activate, dissolve teams | All team patterns |
| **Broadcast Messaging** | Team fanout exchange, `BroadcastAsync` on message bus | Council, Team notifications |
| **Coordination Messages** | Standard message types for shutdown, task completion, plan approval | Graceful shutdown, Plan approval |
| **Swarm Pattern** | Shared work queue with competing consumers | Self-organising swarm |
| **Error Coordination** | Circuit breaker on agent harness, cascade prevention | Resilient multi-agent systems |
| **Orchestration Engine** | `IOrchestrator` — DAG execution, saga patterns, checkpoint/restart | Complex workflows |

---

## 6. Conclusion

The research validates Cortex's existing architecture. The primitives identified in Claude Code's TeammateTool (Agent, Team, Task, Inbox, Message) map cleanly onto interfaces Cortex already defines. The key advantage is that Cortex uses RabbitMQ message queues instead of file-based inboxes, making every orchestration pattern more robust and performant.

**Three changes to the current Issue #2 plan:**
1. Add `FromAgentId` to `MessageContext`
2. Return `IAsyncDisposable` from `StartConsumingAsync` for per-consumer lifecycle
3. Add team-aware overloads to `IAgentRuntime`

These are minimal, additive changes that make the foundation extensible for all six orchestration patterns without over-engineering the initial implementation. The authority model already provides plan approval gating. The team and channel models already support team grouping. We're building on solid ground.
