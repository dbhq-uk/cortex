# Agent Harness and Runtime Design

**Date:** 2026-02-22 (updated 2026-02-23)
**Issue:** #2 — Implement agent harness and base agent types
**Status:** Approved
**Research:** See [Agent Orchestration Patterns](../research/2026-02-23-agent-orchestration-patterns.md)

## Problem

The existing `IAgent`, `IAgentRegistry`, and `IDelegationTracker` interfaces define what agents look like but have no runtime to connect them to message queues. We need the execution harness that wires agents to the message bus and manages their lifecycle.

The harness must be designed as the foundation for multi-agent orchestration patterns (swarm, pipeline, council, etc.) identified in external research, even though those patterns are implemented in later phases.

## Design Decisions

### Scope

Build the harness infrastructure with test/echo agents. Defer real `HumanAgent` (depends on web UI, issue #6) and `AiAgent` (depends on AI model integration) to later issues.

### Architecture

```
┌─────────────────────────────────────────────────┐
│  .NET Host (daemon process)                     │
│                                                 │
│  ┌───────────────────────────────────────────┐  │
│  │  AgentRuntime : IHostedService, IAgentRuntime│
│  │                                           │  │
│  │  ┌─────────────┐  ┌─────────────┐        │  │
│  │  │ AgentHarness │  │ AgentHarness │  ...   │  │
│  │  │  (agent-a)   │  │  (agent-b)   │        │  │
│  │  └──────┬───────┘  └──────┬───────┘        │  │
│  │         │                 │                │  │
│  └─────────┼─────────────────┼────────────────┘  │
│            │                 │                   │
│  ┌─────────┴─────────────────┴────────────────┐  │
│  │           IMessageBus                       │  │
│  └─────────────────────────────────────────────┘  │
│  ┌──────────────────┐ ┌───────────────────────┐  │
│  │  IAgentRegistry   │ │  IDelegationTracker   │  │
│  └──────────────────┘ └───────────────────────┘  │
└─────────────────────────────────────────────────┘
```

### Components

#### AgentHarness

Plain class (no hosting dependency) that connects one `IAgent` to its message queue.

Responsibilities:
- `StartAsync`: register agent in `IAgentRegistry`, start consuming from `agent.{agentId}` queue, store the consumer handle for independent lifecycle control
- Message handler: dispatch to `IAgent.ProcessAsync`, stamp `FromAgentId` on replies, publish reply to `ReplyTo` queue if response returned
- `StopAsync`: dispose consumer handle (stops only this harness's consumer), mark agent unavailable in registry, drain in-flight work

#### AgentRuntime : IHostedService, IAgentRuntime

Singleton daemon service that manages all agent harnesses.

```csharp
public interface IAgentRuntime
{
    Task<string> StartAgentAsync(IAgent agent, CancellationToken ct = default);
    Task<string> StartAgentAsync(IAgent agent, string teamId, CancellationToken ct = default);
    Task StopAgentAsync(string agentId, CancellationToken ct = default);
    Task StopTeamAsync(string teamId, CancellationToken ct = default);
    IReadOnlyList<string> RunningAgentIds { get; }
    IReadOnlyList<string> GetTeamAgentIds(string teamId);
}
```

- On `IHostedService.StartAsync`: boots all DI-registered agents
- Exposes `IAgentRuntime` for dynamic agent creation (injectable into agents)
- On `IHostedService.StopAsync`: drains all harnesses gracefully
- Tracks both startup-registered and dynamically created agents identically
- Maintains team-to-agent mapping for team lifecycle operations

#### Dynamic Agent Lifecycle

Agents can create subagents at runtime via `IAgentRuntime`:

- **Ephemeral agents**: spun up for a single task, parent stops them after receiving reply
- **Long-standing agents**: spun up dynamically but persist indefinitely (e.g., a dev team that stays active), stopped when team dissolves or runtime shuts down
- **Team-scoped agents**: started with a `teamId`, can be stopped individually or as a group via `StopTeamAsync`

Both patterns use the same `StartAgentAsync`/`StopAgentAsync` mechanism. The difference is in who calls stop and when.

#### InMemoryAgentRegistry

Thread-safe `ConcurrentDictionary`-based implementation of `IAgentRegistry`. Supports register, find-by-id, find-by-capability.

#### InMemoryDelegationTracker

Thread-safe implementation of `IDelegationTracker`. Supports delegate, update status, query by assignee, query overdue.

### Per-Consumer Lifecycle (IAsyncDisposable)

**Problem:** Multiple `AgentHarness` instances share one `IMessageBus`. The current `StopConsumingAsync()` stops ALL consumers on the bus — stopping one agent would stop every agent.

**Solution:** `StartConsumingAsync` returns an `IAsyncDisposable` handle. Each harness stores its handle and disposes only its own consumer on stop.

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

This is a breaking change to `IMessageConsumer`. Both `InMemoryMessageBus` and `RabbitMqMessageBus` need updating. We are pre-1.0 and this is foundational correctness — multiple agents on one bus is the primary use case.

### Message Flow

```
Sender                  AgentHarness                    IAgent
  │                         │                             │
  │  PublishAsync(envelope  │                             │
  │   + ReplyTo="sender-q"  │                             │
  │   + FromAgentId="x")    │                             │
  │ ───────────────────────>│                             │
  │                         │  ProcessAsync(envelope)     │
  │                         │ ───────────────────────────>│
  │                         │                             │
  │                         │  return response envelope   │
  │                         │ <───────────────────────────│
  │                         │                             │
  │  PublishAsync(response  │                             │
  │   + FromAgentId=agentId │                             │
  │   queueName="sender-q") │                             │
  │ <───────────────────────│                             │
```

### MessageContext Changes

Add `ReplyTo` and `FromAgentId` to `MessageContext`:

```csharp
public sealed record MessageContext
{
    public string? ParentMessageId { get; init; }
    public string? OriginalGoal { get; init; }
    public string? TeamId { get; init; }
    public string? ChannelId { get; init; }
    public string? ReplyTo { get; init; }         // queue name for responses
    public string? FromAgentId { get; init; }     // sender identity
}
```

`FromAgentId` is stamped by the harness on all outbound replies. Every coordination pattern (delegation, approval, broadcast, error reporting) requires knowing who sent a message.

### Harness Dispatch Logic

1. Message arrives on agent's queue
2. Harness calls `agent.ProcessAsync(envelope)`
3. If response is not null and `envelope.Context.ReplyTo` is set:
   - Publish response to that queue
   - Carry the same `ReferenceCode`
   - Set `ParentMessageId` to the original `MessageId`
   - Set `FromAgentId` to the agent's `AgentId`
4. If response is not null but no `ReplyTo`: log warning, drop response
5. If `ProcessAsync` throws: log error, message nacked to dead letter by bus layer

### Queue Naming Convention

Each agent gets a dedicated queue: `agent.{agentId}`. Deterministic — the harness derives the queue name from the agent's `AgentId` property.

### DI Registration

```csharp
services.AddCortexAgentRuntime(runtime =>
{
    runtime.AddAgent<EchoAgent>();
});
```

Registers: `AgentRuntime` as `IHostedService` + `IAgentRuntime`, agents as singletons, `InMemoryAgentRegistry` as `IAgentRegistry`, `InMemoryDelegationTracker` as `IDelegationTracker`.

### Testing Strategy

All unit tests use `InMemoryMessageBus` — no RabbitMQ required. Test with an `EchoAgent` that returns a response containing the original message content, proving the full dispatch + reply cycle works.

## Alignment with Multi-Agent Orchestration Patterns

Research into Claude Code's TeammateTool system and VoltAgent's meta-orchestration patterns validates that the harness architecture maps directly onto established multi-agent primitives:

| Research Primitive | Cortex Component |
|-------------------|------------------|
| Agent with Inbox | `IAgent` + RabbitMQ queue per agent |
| Team (leader + teammates) | `ITeam` + `IAgentRuntime` team operations |
| Direct message (write) | `IMessageBus.PublishAsync` to `agent.{targetId}` |
| Task assignment | `IDelegationTracker.DelegateAsync` |
| Plan approval gating | `AuthorityTier.AskMeFirst` on `AuthorityClaim` |
| Autonomous execution | `AuthorityTier.JustDoIt` |
| Request/reply | `MessageContext.ReplyTo` |
| Sender identity | `MessageContext.FromAgentId` |
| Priority scheduling | `MessagePriority` + `MessageEnvelope.Sla` |
| Dead letter handling | RabbitMQ dead letter exchange |
| Capability-based routing | `IAgentRegistry.FindByCapabilityAsync` |

### Authority Model as Approval Gating

The authority model already encodes the plan approval pattern from the research:

| Authority Tier | Orchestration Equivalent | Behaviour |
|---------------|-------------------------|-----------|
| `JustDoIt` | Autonomous agent | Execute without approval |
| `DoItAndShowMe` | Execute + report | Prepare, execute, present results |
| `AskMeFirst` | Plan approval gating | Propose plan, await approval before acting |

### RabbitMQ Advantages Over File-Based Coordination

The research patterns use file-based inboxes with polling. Cortex uses RabbitMQ, which is strictly superior for all six orchestration patterns:

| Pattern | RabbitMQ Implementation |
|---------|------------------------|
| Direct message | Publish to `agent.{targetId}` queue |
| Broadcast to team | Fanout exchange `team.{teamId}` bound to member queues |
| Swarm (competing consumers) | Multiple agents consume from shared `team.{teamId}.work` queue |
| Pipeline | Chain via `ReplyTo` — each stage publishes to next stage's queue |
| Council (parallel specialists) | Publish same message to N specialist queues; collect replies |
| Plan approval | Authority tier on message + `ReplyTo` for approval response |

## Out of Scope (This Issue)

- `HumanAgent` base type (depends on web UI — issue #6)
- `AiAgent` base type (depends on AI model integration)
- Agent type resolution from registry (Phase 2)
- Persistent agent registry (Phase 2 — database-backed)
- Persistent delegation tracker (Phase 2 — database-backed)

## Future Orchestration Roadmap

The following capabilities build on this harness foundation. Each is a separate future issue, documented here to ensure the foundation supports them.

### Task Dependency Graphs

**What:** Extend `DelegationRecord` with `BlockedBy` and `Blocks` collections. Add `FindBlockedAsync()` and auto-unblock logic when blockers complete.

**Why:** Pipeline and coordinated refactoring patterns require tasks that wait for upstream work to finish before starting. The current flat delegation model has no dependency concept.

**Enables:** Sequential Pipeline, Coordinated Multi-File Refactoring patterns.

### Team Lifecycle Management

**What:** Implement `ITeam` management — assemble teams, activate, dissolve. Track team membership in a `ITeamRegistry`. Wire team lifecycle to `AgentRuntime` (team assembly starts agents, team dissolution stops them).

**Why:** Multi-agent coordination requires grouping agents with shared context and coordinated lifecycle. `ITeam` and `TeamStatus` already exist as interfaces.

**Enables:** All team-based orchestration patterns.

### Broadcast Messaging

**What:** Team fanout exchange in RabbitMQ topology. Each agent in a team gets bound to `team.{teamId}` fanout exchange. Add `BroadcastAsync(envelope, teamId)` to message bus.

**Why:** Council pattern and team notifications require sending the same message to all team members simultaneously, rather than N individual publish calls.

**Enables:** Council (Parallel Specialists), team-wide notifications.

### Coordination Message Types

**What:** Define standard coordination message types in `Cortex.Core.Messages.Coordination`:
- `ShutdownRequest` / `ShutdownApproved` — graceful agent termination
- `TaskCompletedNotification` — broadcast when task finishes
- `PlanApprovalRequest` / `PlanApprovalResponse` — explicit plan gating
- `IdleNotification` — agent reports it has no work
- `JoinRequest` / `JoinApproved` — team membership requests

**Why:** Structured coordination messages enable reliable orchestration protocols beyond simple request/reply. Maps to the 9 structured message types in Claude Code's system.

**Enables:** Graceful shutdown, plan approval workflows, team assembly.

### Swarm Pattern (Competing Consumers)

**What:** Support shared work queue binding in `QueueTopology`. Multiple agents consume from `team.{teamId}.work` — RabbitMQ distributes messages round-robin across consumers. Workers claim tasks naturally through message consumption.

**Why:** Self-organising swarm is the simplest parallelism pattern. No central coordinator needed — RabbitMQ's competing consumer model provides natural load balancing.

**Enables:** Self-Organising Swarm pattern.

### Error Coordination

**What:** Circuit breaker state machine on `AgentHarness` (closed → open → half-open). Configurable failure thresholds. Cascade prevention via bulkhead isolation (errors in one agent don't propagate to others). Recovery strategies: immediate retry, delayed retry, alternative path, fallback.

**Why:** Multi-agent systems need resilience. A failing agent should not bring down the team. The current nack-to-dead-letter mechanism handles individual message failures but not systemic agent failures.

**Error taxonomy:** Infrastructure errors, application errors, integration failures, data errors, timeout errors, permission errors, resource exhaustion, external failures.

**Enables:** Resilient multi-agent systems, graceful degradation.

### Orchestration Engine

**What:** `IOrchestrator` interface for complex workflow execution:
- DAG execution with topological sorting
- Saga patterns with compensation logic (rollback on failure)
- Checkpoint/restart for workflow resumption
- Fork-join synchronisation barriers
- Conditional branching and loop handling

**Why:** Complex business processes need coordinated multi-step workflows that go beyond simple request/reply or task delegation.

**Enables:** Sequential Pipeline, Map-Reduce, Event-Driven Coordination, complex business workflows.

### Capability-Based Task Distribution

**What:** Runtime task routing that matches work to agents based on capabilities, availability, and load. Load-balanced assignment across agents with matching capabilities. Priority scheduling respecting SLA constraints.

**Why:** As the number of agents grows, manual routing becomes impractical. The registry already supports `FindByCapabilityAsync` — this adds intelligent routing on top.

**Enables:** Automatic work distribution, SLA enforcement, load balancing.
