# Agent Harness and Runtime Design

**Date:** 2026-02-22
**Issue:** #2 — Implement agent harness and base agent types
**Status:** Approved

## Problem

The existing `IAgent`, `IAgentRegistry`, and `IDelegationTracker` interfaces define what agents look like but have no runtime to connect them to message queues. We need the execution harness that wires agents to the message bus and manages their lifecycle.

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
- `StartAsync`: register agent in `IAgentRegistry`, start consuming from `agent.{agentId}` queue
- Message handler: dispatch to `IAgent.ProcessAsync`, publish reply to `ReplyTo` queue if response returned
- `StopAsync`: stop consuming, mark agent unavailable in registry, drain in-flight work

#### AgentRuntime : IHostedService, IAgentRuntime

Singleton daemon service that manages all agent harnesses.

```csharp
public interface IAgentRuntime
{
    Task<string> StartAgentAsync(IAgent agent, CancellationToken ct = default);
    Task StopAgentAsync(string agentId, CancellationToken ct = default);
    IReadOnlyList<string> RunningAgentIds { get; }
}
```

- On `IHostedService.StartAsync`: boots all DI-registered agents
- Exposes `IAgentRuntime` for dynamic agent creation (injectable into agents)
- On `IHostedService.StopAsync`: drains all harnesses gracefully
- Tracks both startup-registered and dynamically created agents identically

#### Dynamic Agent Lifecycle

Agents can create subagents at runtime via `IAgentRuntime`:

- **Ephemeral agents**: spun up for a single task, parent stops them after receiving reply
- **Long-standing agents**: spun up dynamically but persist indefinitely (e.g., a dev team that stays active), stopped when team dissolves or runtime shuts down

Both patterns use the same `StartAgentAsync`/`StopAgentAsync` mechanism. The difference is in who calls stop and when.

#### InMemoryAgentRegistry

Thread-safe `ConcurrentDictionary`-based implementation of `IAgentRegistry`. Supports register, find-by-id, find-by-capability.

#### InMemoryDelegationTracker

Thread-safe implementation of `IDelegationTracker`. Supports delegate, update status, query by assignee, query overdue.

### Message Flow

```
Sender                  AgentHarness                    IAgent
  │                         │                             │
  │  PublishAsync(envelope  │                             │
  │   + ReplyTo="sender-q") │                             │
  │ ───────────────────────>│                             │
  │                         │  ProcessAsync(envelope)     │
  │                         │ ───────────────────────────>│
  │                         │                             │
  │                         │  return response envelope   │
  │                         │ <───────────────────────────│
  │                         │                             │
  │  PublishAsync(response, │                             │
  │   queueName="sender-q") │                             │
  │ <───────────────────────│                             │
```

### ReplyTo Mechanism

Add `ReplyTo` to `MessageContext`:

```csharp
public sealed record MessageContext
{
    public string? ParentMessageId { get; init; }
    public string? OriginalGoal { get; init; }
    public string? TeamId { get; init; }
    public string? ChannelId { get; init; }
    public string? ReplyTo { get; init; }       // queue name for responses
}
```

Harness dispatch logic:
1. Message arrives on agent's queue
2. Harness calls `agent.ProcessAsync(envelope)`
3. If response is not null and `envelope.Context.ReplyTo` is set: publish response to that queue, carrying the same `ReferenceCode` and setting `ParentMessageId` to the original `MessageId`
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

## Out of Scope

- `HumanAgent` base type (depends on web UI — issue #6)
- `AiAgent` base type (depends on AI model integration)
- Agent type resolution from registry (Phase 2)
- Persistent agent registry (Phase 2 — database-backed)
- Persistent delegation tracker (Phase 2 — database-backed)
