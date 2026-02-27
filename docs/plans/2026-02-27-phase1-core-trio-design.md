# Phase 1 Core Trio Design — Authority Enforcement, Plan Approval Gating, Delegation Supervision

## Overview

Three remaining Phase 1 infrastructure features that complete the authority and delegation story:

- **#8** Authority claim validation and enforcement
- **#27** AskMeFirst plan approval gating
- **#28** Delegation supervision service — overdue detection and retry

These build directly on the existing authority model, delegation tracking, and SkillDrivenAgent.

## #8 — Authority Claim Validation and Enforcement

### Problem

`IAuthorityProvider` exists as an interface but has no implementation. Authority claims flow through `MessageEnvelope.AuthorityClaims` but are never validated. Any agent can claim any tier.

### Design

#### InMemoryAuthorityProvider

Concrete `IAuthorityProvider` in `Cortex.Agents`:

- `ConcurrentDictionary<(agentId, action), AuthorityClaim>` store
- `GrantAsync(AuthorityClaim)` / `RevokeAsync(agentId, action)` for management
- `GetClaimAsync` and `HasAuthorityAsync` implement the existing interface
- Expired claims rejected automatically via `ExpiresAt` check

#### IAuthorityProvider interface extension

Add write path to the existing read-only interface:

- `GrantAsync(AuthorityClaim, CancellationToken)`
- `RevokeAsync(string agentId, string action, CancellationToken)`

#### Enforcement point: AgentHarness

Before calling `agent.ProcessAsync(envelope)`:

1. Extract claims from `envelope.AuthorityClaims`
2. For each claim, validate: not expired, `GrantedTo` matches agent, tier is sufficient
3. If `IAuthorityProvider` is injected (optional), cross-check against stored grants
4. Reject invalid envelopes — publish error reply to `ReplyTo` or log and drop

Rationale: the harness is the natural enforcement point. Every agent runs through a harness, it doesn't pollute the bus abstraction, and it's easy to test.

#### Delegation chain narrowing verification

Already implemented in `SkillDrivenAgent` (outbound tier <= inbound tier). The enforcement layer adds the verification side: the receiving agent's harness confirms the claim is legitimate.

#### Team-level ceiling

When an agent belongs to a team, the harness checks that the agent's claims don't exceed the team's authority ceiling. Team ceiling stored as a claim on the team ID.

## #27 — AskMeFirst Plan Approval Gating

### Problem

When the CoS receives a message carrying `AskMeFirst` authority, it should propose a plan and await approval before dispatching. Currently, authority narrowing is implemented but there is no approval gate.

### Design

#### New message types in Cortex.Core.Messages

- **`PlanProposal`** — `IMessage`. Carries: task list (from `DecompositionResult`), summary, original goal, reference code
- **`PlanApprovalResponse`** — `IMessage`. Carries: approved/rejected status, optional amendments, reference code

#### New pipeline skill: cos-plan-gate

Inserted after `cos-decompose` in the CoS pipeline:

```
1. cos-context-query
2. cos-decompose
3. cos-plan-gate
```

Behaviour:

- Reads `DecompositionResult` from pipeline context
- Reads max authority tier from envelope claims
- **JustDoIt** or **DoItAndShowMe**: pass through, no change
- **AskMeFirst**: store pending workflow, publish `PlanProposal` to escalation target (default `agent.founder`), return `null` to halt pipeline

#### Pending workflow state

**`IPendingPlanStore`** — new interface in `Cortex.Agents`:

- `StoreAsync(ReferenceCode, PendingPlan)`
- `GetAsync(ReferenceCode) -> PendingPlan?`
- `RemoveAsync(ReferenceCode)`

**`PendingPlan`** — record: original envelope, decomposition result, stored timestamp.

**`InMemoryPendingPlanStore`** — `ConcurrentDictionary` implementation.

#### Approval flow in SkillDrivenAgent

New check at the top of `ProcessAsync`, before the existing sub-task reply check:

1. Is this a `PlanApprovalResponse`? Match by `ParentMessageId` to reference code
2. **Approved**: retrieve `PendingPlan`, resume routing with stored decomposition
3. **Rejected**: remove pending plan, update delegation status to `Complete`, publish rejection summary to original `ReplyTo`

#### Why in-pipeline, not a separate service

The gating is a routing decision made by the same agent that decomposes work. A separate service would need the full routing context duplicated. Keeping it in-pipeline means the existing routing logic is reused on resume.

## #28 — Delegation Supervision Service

### Problem

The CoS delegates and steps aside. There is no mechanism to detect stalled work, retry failed tasks, or escalate when agents are unresponsive.

### Design

#### DelegationSupervisionService : IHostedService

Background service in `Cortex.Agents`. Runs on a `PeriodicTimer` (configurable interval, default 60s).

Each tick:

1. Call `IDelegationTracker.GetOverdueAsync()`
2. For each overdue delegation, look up retry count
3. Below max retries (default 3): publish `SupervisionAlert` to `agent.cos`
4. At max retries: publish `EscalationAlert` to configurable escalation target (default `agent.founder`)
5. Check `IAgentRuntime.RunningAgentIds` against `DelegatedTo` — flag dead agents
6. Log supervision summary

#### Retry counting

**`IRetryCounter`** — new interface in `Cortex.Agents`:

- `IncrementAsync(ReferenceCode) -> int` (returns new count)
- `GetCountAsync(ReferenceCode) -> int`
- `ResetAsync(ReferenceCode)`

**`InMemoryRetryCounter`** — `ConcurrentDictionary<string, int>`.

Retry counts are separate from `DelegationRecord`. The record is an immutable snapshot; the retry count is operational state of the supervision loop.

#### New message types in Cortex.Core.Messages

- **`SupervisionAlert`** — `IMessage`. Carries: reference code, delegated agent ID, retry count, `DueAt`, description, `IsAgentRunning`
- **`EscalationAlert`** — `IMessage`. Carries: reference code, delegated agent ID, retry count, reason, original description

#### Dead agent detection

Each tick checks `IAgentRuntime.RunningAgentIds` against overdue `DelegatedTo`. If the target agent isn't running, the alert includes `IsAgentRunning = false`.

#### CoS handling of alerts

Handled in `SkillDrivenAgent.ProcessAsync` as message type checks:

- `SupervisionAlert`: re-dispatch to alternative agent, create new delegation, store lesson via `IContextProvider`
- `EscalationAlert`: forward to escalation target with full context

#### Testability

Constructor dependencies: `IDelegationTracker`, `IRetryCounter`, `IMessagePublisher`, `IAgentRuntime`. Expose `CheckOverdueAsync()` method — timer calls it, tests call it directly. No timer dependency in tests.

## Dependency Order

```
#8 (authority enforcement) — no dependencies on #27 or #28
        ↓
#27 (plan approval gating) — uses authority validation from #8
        ↓
#28 (delegation supervision) — independent of #27 but benefits from #8 enforcement
```

#8 first. Then #27 and #28 can be built in either order.

## New Types Summary

| Type | Namespace | Feature |
|------|-----------|---------|
| `InMemoryAuthorityProvider` | `Cortex.Agents` | #8 |
| `PlanProposal` | `Cortex.Core.Messages` | #27 |
| `PlanApprovalResponse` | `Cortex.Core.Messages` | #27 |
| `IPendingPlanStore` | `Cortex.Agents` | #27 |
| `PendingPlan` | `Cortex.Agents` | #27 |
| `InMemoryPendingPlanStore` | `Cortex.Agents` | #27 |
| `DelegationSupervisionService` | `Cortex.Agents` | #28 |
| `IRetryCounter` | `Cortex.Agents` | #28 |
| `InMemoryRetryCounter` | `Cortex.Agents` | #28 |
| `SupervisionAlert` | `Cortex.Core.Messages` | #28 |
| `EscalationAlert` | `Cortex.Core.Messages` | #28 |
