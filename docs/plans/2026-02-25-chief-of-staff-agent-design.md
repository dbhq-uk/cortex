# Chief of Staff Agent Design

**Date:** 2026-02-25
**Issue:** #3 — Implement Chief of Staff (CoS) agent — basic triage and routing
**Status:** Approved

## Problem

The agent harness, message bus, and delegation tracker are in place but there are no real agents. We need the first production agent: a Chief of Staff that triages incoming messages and routes them to specialist agents by capability.

The CoS is not architecturally special. It is an agent configured with triage and routing skills. The same agent class should support any persona — two companies could run two CoS agents with different config. The "chief of staff" is a persona, not a privileged type.

## Design Decisions

### Key Principles (from Q&A)

- **CoS is just another agent.** No special base class, no hardcoded routing. Triage and routing are skills any agent could use.
- **LLM-assisted triage.** The CoS calls an LLM (via skill) to classify messages and determine routing. Not rule-based.
- **Skills, not methods.** Triage and routing logic live in registered skills invoked through `ISkillExecutor`. The agent is thin orchestration glue.
- **Not a mandatory gateway.** Default channel messages go through the CoS. Direct channels bypass it. The publishing layer decides based on `ChannelType`.
- **CoS steps aside after delegation.** Replies from specialists go directly to the original requester via `ReplyTo`. The CoS tracks via `IDelegationTracker` but is not in the reply path.
- **1:1 routing in Phase 1.** One message maps to one delegation. 1:N decomposition deferred to Phase 2 task dependency DAG (#11).
- **Escalation target is config.** Defaults to `agent.founder`, configurable per persona.
- **Claude CLI wrapper brought forward.** Pro Max subscription makes CLI usage free. The CoS needs real LLM triage to be useful.

## Architecture

### SkillDrivenAgent

A single concrete `IAgent` implementation that any persona uses. Configured with identity and a skill pipeline.

```
SkillDrivenAgent : IAgent
  +-- Persona config (agentId, name, capabilities)
  +-- Skill pipeline (ordered list of skill IDs to execute per message)
  +-- Dependencies (ISkillExecutor, IAgentRegistry, IDelegationTracker, IReferenceCodeGenerator)
```

`ProcessAsync` flow:

1. Receives `MessageEnvelope`
2. Runs through its skill pipeline in order
3. Each skill gets the envelope and output from all previous skills
4. The pipeline produces a routing decision (or no decision)
5. Agent acts on the decision: route, respond directly, or escalate

### Skill Pipeline Execution

Pipeline context accumulates as skills execute:

```
SkillPipelineContext
  +-- Envelope        (the original incoming MessageEnvelope)
  +-- Results         (dictionary of skill ID -> output from each prior skill)
  +-- Decision        (the final routing/response decision, set by any skill)
```

Execution rules:

- Skills run in declared order. Each receives the full context including all prior skill outputs.
- Any skill can set a `Decision` (route, delegate, escalate, respond directly).
- Once a decision is set, remaining skills still run and can override or enrich it.
- If the pipeline finishes with no decision, escalate to the configured escalation target.

### Routing and Delegation

Three possible outcomes from a pipeline:

**Route and Delegate** (happy path):

1. `IAgentRegistry.FindByCapabilityAsync` with the capability from the triage skill
2. Pick the first available agent (no load balancing in Phase 1)
3. Create a `DelegationRecord` via `IDelegationTracker.DelegateAsync`
4. Generate a `ReferenceCode` for the delegation
5. Publish the envelope to `agent.{targetAgentId}` with `ReplyTo` set to the original sender's queue

**Respond Directly** (skill produces a response, no delegation needed):

1. Pipeline sets a response envelope as the decision
2. `ProcessAsync` returns it and the `AgentHarness` publishes to `ReplyTo`

**Escalate** (no capability match or no decision):

1. Publish to the configured escalation target (default `agent.founder`)
2. Track as a delegation with a description of why it escalated

**Authority narrowing:**

- The triage skill suggests an `AuthorityTier` for the outbound message
- Outbound authority can never be higher than what the inbound message carried
- If the inbound message is `DoItAndShowMe`, the delegation can be `DoItAndShowMe` or `JustDoIt`, never `AskMeFirst`

### Persona Configuration

A persona is a markdown file under `personas/`:

```markdown
# Chief of Staff

## Identity
- **agent-id**: cos
- **name**: Chief of Staff
- **type**: ai

## Capabilities
- triage: Analyses incoming messages and determines routing
- routing: Routes messages to specialist agents by capability
- delegation: Tracks delegated work and monitors completion

## Pipeline
1. cos-triage
2. cos-route

## Configuration
- **escalation-target**: agent.founder
- **model-tier**: balanced
```

Parsed into a `PersonaDefinition` record. The `AgentRuntimeBuilder` gains `AddPersona(path)` for loading persona files and registering `SkillDrivenAgent` instances.

### Skills

**cos-triage** (executor type: `llm`):

- Receives message content and available capabilities from the registry
- Calls an LLM via the Claude CLI wrapper
- Returns structured output: `{ capability, authorityTier, summary, confidence }`
- The skill definition markdown contains the system prompt and output schema
- Below-threshold confidence triggers escalation

**cos-route** (executor type: `csharp`):

- Receives the triage output and the original envelope
- Calls `IAgentRegistry.FindByCapabilityAsync` with the matched capability
- Returns a Route decision (target agent, delegation details) or an Escalate decision

### Claude CLI Wrapper

Brought forward from Phase 2 to support LLM-assisted triage.

```
ILlmClient
  +-- CompleteAsync(prompt, cancellationToken) -> string

ClaudeCliClient : ILlmClient
  +-- Shells out to `claude` CLI with the prompt
  +-- Returns the text response
  +-- Handles timeouts and process errors
  +-- Stateless, one-shot completions (no conversation memory)
```

`LlmSkillExecutor : ISkillExecutor` handles executor type `"llm"`:

- Reads the system prompt from the skill definition markdown
- Appends message content and context
- Calls `ILlmClient.CompleteAsync`
- Parses the structured output

## Components

### New Code

| Component | Location | Purpose |
|-----------|----------|---------|
| `SkillDrivenAgent : IAgent` | `Cortex.Agents` | Generic agent that runs a skill pipeline per message |
| `SkillPipelineContext` | `Cortex.Agents` | Accumulates context through a skill pipeline |
| `SkillPipelineRunner` | `Cortex.Agents` | Executes skill pipelines, manages context flow |
| `PersonaDefinition` | `Cortex.Agents` | Parsed persona config record |
| `PersonaParser` | `Cortex.Agents` | Reads persona markdown files |
| `ILlmClient` | `Cortex.Skills` | Single-method LLM abstraction |
| `ClaudeCliClient : ILlmClient` | `Cortex.Skills` | Claude CLI process wrapper |
| `LlmSkillExecutor : ISkillExecutor` | `Cortex.Skills` | Executor type `"llm"` |
| `cos-triage` skill definition | `skills/` | Triage prompt and output schema |
| `cos-route` skill definition | `skills/` | Capability-based routing logic |
| Chief of Staff persona | `personas/` | First persona file |

### Modifications to Existing Code

| Component | Change |
|-----------|--------|
| `AgentRuntimeBuilder` | `AddPersona(path)` method |
| `ServiceCollectionExtensions` | Register `ILlmClient`, `LlmSkillExecutor` |

### Not in Scope

- Conversation memory or multi-turn in `ClaudeCliClient`
- Load balancing across multiple matching agents
- 1:N task decomposition (Phase 2 DAG, issue #11)
- Skill authoring by agents (Phase 3, issue #20)
- HumanAgent implementation (needs Web UI, issue #6)

## Testing Strategy

### Unit Tests — SkillDrivenAgent

- Mock `ISkillExecutor`: pipeline execution, ordering, context accumulation
- Mock `IAgentRegistry`: capability matching, no-match escalation
- Mock `IDelegationTracker`: delegation records created correctly
- Mock `ILlmClient`: no real CLI calls in unit tests
- Authority narrowing enforcement: outbound never exceeds inbound

### Unit Tests — Pipeline

- Empty pipeline escalates
- Single skill produces decision
- Multi-skill context flows, later skill can override
- Skill returns no decision escalates

### Unit Tests — cos-route Skill

- Capability found produces route decision with correct target queue
- No capability match produces escalate decision
- Multiple matches picks first available

### Integration Tests — ClaudeCliClient

- Marked `Category=Integration` (require CLI installed)
- Simple prompt returns non-empty response
- Timeout handling: process killed if it hangs
- Error handling: CLI not found, non-zero exit

### End-to-End Test

- `SkillDrivenAgent` with `InMemoryMessageBus`, `InMemoryAgentRegistry`, mock `ILlmClient`
- Message routes to correct specialist queue
- Unroutable message escalates to founder queue
- Delegation record created with correct fields
