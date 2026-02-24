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

Research into Claude Code's TeammateTool system and community-sourced meta-orchestration patterns validates that the harness architecture maps directly onto established multi-agent primitives:

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

The following capabilities build on this harness foundation. Each is tracked as a separate GitHub issue. Full research backing is in the [Multi-Agent Orchestration Research Corpus](../research/README.md).

### Phase 2 — Orchestration Primitives

| Issue | Capability | Enables |
|-------|-----------|---------|
| [#11](https://github.com/dbhq-uk/cortex/issues/11) | **Task dependency DAG** — `BlockedBy`/`Blocks` on `DelegationRecord`, auto-unblock, `Failed` status | Pipeline, coordinated refactoring, map-reduce |
| [#12](https://github.com/dbhq-uk/cortex/issues/12) | **Broadcast messaging** — team fanout exchange, `BroadcastAsync(envelope, teamId)` | Council (parallel specialists), team notifications |
| [#13](https://github.com/dbhq-uk/cortex/issues/13) | **Coordination message types** — `TaskCompleted`, `PlanApproval`, `Shutdown`, `Idle`, `Join`, `ProgressUpdate` | Graceful shutdown, plan approval, team assembly |
| [#14](https://github.com/dbhq-uk/cortex/issues/14) | **Team lifecycle management** — `ITeamRegistry`, assemble/activate/dissolve, team templates | All team-based orchestration patterns |
| [#15](https://github.com/dbhq-uk/cortex/issues/15) | **Agent capability enrichment** — `ModelTier`, `PerformanceScore`, `CurrentWorkload`, `CostPerTask` | Cost-aware routing, intelligent agent selection |

### Phase 3 — Team Composition and Intelligence

| Issue | Capability | Enables |
|-------|-----------|---------|
| [#16](https://github.com/dbhq-uk/cortex/issues/16) | **TeamArchitectAgent** — analyses requirements, queries registry, assembles teams, monitors, re-plans, dissolves | "Build me a team and build this" |
| [#17](https://github.com/dbhq-uk/cortex/issues/17) | **Orchestration engine** — DAG execution, saga patterns, checkpoint/restart, fork-join barriers | Complex multi-step business workflows |
| [#18](https://github.com/dbhq-uk/cortex/issues/18) | **Error coordination** — per-agent circuit breakers, cascade prevention, graduated recovery, error taxonomy | Resilient multi-agent systems |
| [#19](https://github.com/dbhq-uk/cortex/issues/19) | **Capability-based task distribution** — multi-factor routing, affinity, priority scheduling, load balancing | Automatic work distribution, SLA enforcement |
| [#20](https://github.com/dbhq-uk/cortex/issues/20) | **Self-improving skills** — agent-generated skill definitions (Voyager pattern), semantic indexing, composition, verification | Adaptive, self-improving agent capabilities |

### Design Principle: Foundation Supports All Patterns

The harness architecture (`AgentHarness` per agent, `AgentRuntime` as lifecycle manager, `IAgentRegistry` for discovery, `IDelegationTracker` for state, RabbitMQ for messaging) was designed to support all of the above without structural changes. Each Phase 2/3 capability adds new services and message types on top of the existing foundation — it does not require modifying the harness core.

The one exception is the per-consumer lifecycle (`IAsyncDisposable` from `StartConsumingAsync`) which was a foundational change made in Issue #2 specifically to enable multiple agents on one bus — a prerequisite for every multi-agent pattern.
