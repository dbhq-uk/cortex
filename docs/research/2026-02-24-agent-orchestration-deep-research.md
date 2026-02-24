# Agent Orchestration Deep Research -- Frameworks, Protocols, and Patterns

**Date:** 2026-02-24
**Purpose:** Comprehensive industry research on agent orchestration patterns, subagent management, task decomposition, and inter-agent communication. Supplements the initial research synthesis (2026-02-23) with deep implementation details from production frameworks and emerging standards.

> **Part of the [Multi-Agent Orchestration Research Corpus](./README.md).** Builds on the [Initial Patterns](./2026-02-23-agent-orchestration-patterns.md) document. For framework code examples see [Swarm Frameworks](./2026-02-24-agent-swarm-frameworks.md). For role archetype details see [Meta-Orchestration Patterns](./2026-02-24-prompt-based-meta-orchestration-patterns.md).

---

## Table of Contents

1. [Orchestrator-Worker Pattern](#1-orchestrator-worker-pattern)
2. [Agent Registry Patterns](#2-agent-registry-patterns)
3. [Task Decomposition Strategies](#3-task-decomposition-strategies)
4. [Communication Patterns Between Agents](#4-communication-patterns-between-agents)
5. [Cross-Cutting Concerns](#5-cross-cutting-concerns)
6. [Implications for Cortex](#6-implications-for-cortex)

---

## 1. Orchestrator-Worker Pattern

### 1.1 OpenAI Swarm / Agents SDK

**Status:** Swarm is now deprecated as a reference/educational framework. The OpenAI Agents SDK (released March 2025, Python and TypeScript) is the production successor.

**Core primitives:**
- **Agent** -- instructions (system prompt) + tools + handoffs + model defaults
- **Handoff** -- a tool call that returns another Agent; the runner switches the `active_agent`, preserves conversation history, and continues the loop

**Handoff implementation (Agents SDK):**

```python
from agents import Agent, handoff

class EscalationData(BaseModel):
    reason: str

async def on_handoff(ctx: RunContextWrapper[None], input_data: EscalationData):
    print(f"Escalation reason: {input_data.reason}")

billing_agent = Agent(name="Billing", instructions="Handle billing queries.")
support_agent = Agent(
    name="Support",
    instructions="Handle support queries.",
    handoffs=[
        handoff(
            agent=billing_agent,
            tool_name_override="transfer_to_billing",
            tool_description_override="Transfer when user has a billing issue",
            on_handoff=on_handoff,
            input_type=EscalationData
        )
    ]
)
```

**Key `handoff()` function parameters:**
| Parameter | Purpose |
|-----------|---------|
| `agent` | Target agent for delegation |
| `tool_name_override` | Custom tool name (default: `transfer_to_<agent_name>`) |
| `tool_description_override` | Custom tool description |
| `on_handoff` | Callback executed when handoff invoked (receives context + optional structured input) |
| `input_type` | Pydantic BaseModel defining structured data the LLM provides during handoff |
| `input_filter` | Function filtering `HandoffInputData` passed to next agent |
| `is_enabled` | Boolean or dynamic function controlling availability |
| `nest_handoff_history` | Controls conversation history nesting (collapses into summary wrapped in `<CONVERSATION HISTORY>` tags) |

**Handoff as tool call:** Handoffs appear to the LLM as regular tools. If there is a handoff to "Refund Agent", the model sees a tool called `transfer_to_refund_agent`. When the model calls it, the SDK runner switches `active_agent`, keeps the shared conversation history, and continues the loop.

**Production safety patterns:**
- Put write tools behind a narrow "action agent" with `execute_tools=False` as an approval gate
- Server-side argument validation
- Tool allowlists per agent
- Strict `max_turns` caps on the runner
- JSON logs with trace IDs for every message, score, and handoff

**Recommended multi-agent separation of concerns:**
1. Triage/routing agent
2. Retrieval/grounding agent
3. Action agent (write operations)
4. Review/check/approval agent

---

### 1.2 LangGraph

**Architecture:** Graph-based stateful workflows where nodes are agents/functions and edges define control flow.

**Orchestrator-Worker pattern implementation:**

The pattern uses LangGraph's `Send` API to dynamically fan out work at runtime:

1. **Planner node** -- An LLM analyses the input and produces a structured plan (list of subtasks)
2. **Dispatch** -- For each subtask, the orchestrator issues `Send("worker", payload)` to create a parallel worker instance
3. **Worker nodes** -- Each executes its subtask independently
4. **Synthesiser node** -- Aggregates all worker outputs into a final result

```
START -> planner -> [Send("worker", subtask_1), Send("worker", subtask_2), ...] -> synthesizer -> END
```

**Send API mechanics:**
- `Send(node_name, payload)` is returned from a conditional edge function instead of a string node name
- LangGraph recognises `Send` objects and automatically creates parallel executions
- Each `Send` creates an independent state branch for the worker
- Workers write to disjoint state keys to avoid race conditions
- Results converge at a downstream node for aggregation

**Performance:** In benchmarks, parallel execution via Send showed 137x speedup over sequential execution (61.46s vs 0.45s for a research paper use case with two independent tools).

**Map-Reduce variant:**
- **Map phase:** `Send` dispatches to N workers in parallel
- **Reduce phase:** A collector node receives all results and aggregates
- Workers feed into a shared `Annotated[list, operator.add]` state key that automatically merges

---

### 1.3 CrewAI

**Architecture:** Role-based agent orchestration with explicit hierarchy.

**Process types:**
| Process | Description |
|---------|-------------|
| `Process.sequential` | Agents execute in predefined order (default) |
| `Process.hierarchical` | Manager agent orchestrates, decomposes, delegates |

**Hierarchical process details:**
- A **manager agent** acts as orchestrator, automatically created when `Process.hierarchical` is set
- Manager decomposes global objectives into subtasks and delegates to workers
- Workers execute via structured tool interfaces and respond with machine-readable (JSON) reports
- Communication is **hub-and-spoke** -- subagents communicate only with the orchestrator, never directly with each other
- Senior agents can override junior decisions and redistribute resources

**`allowed_agents` parameter (2025 enhancement):**
- Controls which agents can delegate to which other agents
- Enables fine-grained delegation graphs within a crew
- Prevents unintended delegation chains

---

### 1.4 AWS Strands Agents SDK (1.0, 2025)

**Four multi-agent collaboration patterns:**

| Pattern | Description | Execution Model |
|---------|-------------|-----------------|
| **Agents as Tools** | Specialised agents become callable tools for an orchestrator | Hierarchical -- orchestrator retains control |
| **Swarms** | Autonomous agent teams coordinate through shared memory | Sequential with agent-decided handoffs |
| **Agent Graphs** | Structured workflows via `GraphBuilder` | Controlled but dynamic -- LLM decisions at each node |
| **Workflows** | Pre-defined task DAG executed as a single non-conversational tool | Deterministic -- automatic parallel execution, no cycles |

**Agents-as-Tools pattern:** An orchestrator agent has specialist agents registered as tools. When a query requires domain expertise, the orchestrator calls the specialist as a tool, receives the response, and synthesises the final answer. The specialist never sees the full conversation -- only the specific query.

**Arbiter pattern (advanced):** A next-generation supervisory model extending the Supervisor with:
- Dynamic agent generation
- Semantic task routing
- Blackboard-model-based coordination
- Manages complexity in large, evolving agent ecosystems

**Shared state:** All patterns support `invocation_state` parameter propagation:
```python
shared_state = {"user_id": "123", "debug_mode": True}
result = graph("Analyse data", invocation_state=shared_state)
```
Tools access this via `@tool(context=True)` decorator with `ToolContext`.

---

### 1.5 Google Agent Development Kit (ADK)

**Eight patterns identified:**

| Pattern | Primitive | Execution |
|---------|-----------|-----------|
| Sequential Pipeline | `SequentialAgent` | Linear, deterministic |
| Coordinator/Dispatcher | `LlmAgent` + `sub_agents` | LLM-driven routing via AutoFlow |
| Parallel Fan-Out/Gather | `ParallelAgent` | Concurrent sub-agents, unique `output_key` per agent |
| Hierarchical Decomposition | Sub-agents wrapped as `AgentTool` | Parent treats sub-agent workflow as function call |
| Generator and Critic | `SequentialAgent` inside `LoopAgent` | Draft-review loop, exits on "PASS" |
| Iterative Refinement | `LoopAgent` with `max_iterations` | Agents signal early exit via `escalate=True` |
| Human-in-the-Loop | Custom tools triggering approval | Execution pauses awaiting authorisation |
| Composite | Nested combinations | e.g., Coordinator -> Parallel -> Generator/Critic |

**State management:** ADK uses `session.state` as shared whiteboard. Descriptive `output_key` fields enable downstream agent awareness. Each agent writes to its own key to prevent race conditions during parallel execution.

**Task routing:** Handled via AutoFlow mechanism for Coordinator pattern. Agent `description` fields serve as documentation for the LLM router to decide which sub-agent to invoke.

---

### 1.6 Microsoft Azure Architecture Center (Feb 2026)

**Five canonical orchestration patterns:**

| Pattern | Also Known As | Coordination | Routing |
|---------|--------------|--------------|---------|
| **Sequential** | Pipeline, prompt chaining | Linear -- each agent processes previous output | Deterministic, predefined order |
| **Concurrent** | Fan-out/fan-in, scatter-gather, map-reduce | Parallel -- agents work independently on same input | Deterministic or dynamic agent selection |
| **Group Chat** | Roundtable, multi-agent debate, council | Conversational -- agents contribute to shared thread | Chat manager controls turn order |
| **Handoff** | Routing, triage, transfer, dispatch | Dynamic delegation -- one active agent at a time | Agents decide when to transfer control |
| **Magentic** | Dynamic orchestration, task-ledger-based, adaptive planning | Plan-build-execute -- manager builds and adapts task ledger | Manager assigns and reorders tasks dynamically |

**Magentic orchestration (novel pattern):**
- Manager agent builds a **task ledger** as it iterates
- Consults specialised agents to gather information and refine the plan
- Agents in this pattern have tools that make **direct changes in external systems**
- Manager iterates, backtracks, and delegates as many times as needed
- Regularly checks whether the original request is satisfied or stalled
- Updates the ledger to adjust the plan dynamically

**Maker-Checker loops (sub-pattern of Group Chat):**
- One agent (maker) creates/proposes; another (checker) evaluates against criteria
- If checker identifies gaps, pushes back with specific feedback
- Repeats until checker approves or iteration cap reached
- Requires clear acceptance criteria and iteration cap with fallback (escalate to human or return best result with quality warning)

**Critical implementation considerations from Microsoft:**
- Use the **lowest level of complexity** that reliably meets requirements
- Monitor accumulated context size; use compaction (summarisation, selective pruning) between agents
- Persist shared state externally for long-running tasks
- Implement timeout and retry mechanisms
- Validate agent output before passing to next agent
- Circuit breaker patterns for agent dependencies
- Each agent can use distinct models matched to task complexity

---

## 2. Agent Registry Patterns

### 2.1 Google A2A (Agent-to-Agent) Protocol

**Status:** Open protocol under the Linux Foundation, released April 2025. Version 0.3 released with gRPC support and signed agent cards. Over 50 partners including Atlassian, Salesforce, SAP, and ServiceNow.

**Agent Card -- the discovery mechanism:**

Agents publish a JSON metadata document at a well-known URL (typically `/.well-known/agent.json`):

```json
{
  "name": "Document Translator",
  "description": "Translates documents between languages",
  "version": "1.0",
  "provider": { "organization": "AcmeCorp" },
  "capabilities": {
    "streaming": true,
    "pushNotifications": true,
    "extendedCard": true
  },
  "skills": [
    {
      "name": "translate",
      "description": "Translate text between languages",
      "inputModes": ["text/plain"],
      "outputModes": ["text/plain"]
    }
  ],
  "serviceInterfaces": [
    { "protocol": "jsonrpc", "endpoint": "https://agent.example.com/a2a" }
  ],
  "securitySchemes": {
    "bearer": { "type": "http", "scheme": "bearer" }
  }
}
```

**Agent Card fields:**
| Field | Purpose |
|-------|---------|
| Identity & Provider | Agent name, version, provider organisation |
| Capabilities | Boolean flags: streaming, push notifications, extended cards |
| Skills | Discrete operations the agent can perform, with input/output modes |
| Service Interfaces | Protocol bindings (JSON-RPC, gRPC, HTTP/REST) |
| Security Schemes | Authentication methods (API keys, OAuth2, mutual TLS) |
| Agent Card Signature | Cryptographic proof of authenticity (v0.3+) |

**Task lifecycle states:**
| State | Meaning | Terminal? |
|-------|---------|-----------|
| `working` | Processing in progress | No |
| `input-required` | Awaiting additional user input | No |
| `auth-required` | Client must provide credentials | No |
| `completed` | Successfully finished | Yes |
| `failed` | Error occurred | Yes |
| `canceled` | Client requested cancellation | Yes |
| `rejected` | Agent declined the task | Yes |

**Core JSON-RPC operations:**
| Operation | Purpose |
|-----------|---------|
| `SendMessage` | Submit task, receive Task or direct Message response |
| `SendStreamingMessage` | Submit task with real-time update stream (SSE for HTTP, gRPC streams) |
| `GetTask` | Poll task status and artifacts |
| `ListTasks` | Retrieve filtered task collection with pagination |
| `CancelTask` | Request task cancellation |
| `SubscribeToTask` | Open persistent stream for existing task updates |
| `GetExtendedAgentCard` | Fetch authenticated card with additional details |

**Message format:**
- **Role:** `user` (client) or `agent` (remote system)
- **Parts:** Array of content units -- plain text, file reference (with media type), structured data (JSON)
- **Metadata:** Optional key-value context
- **References:** Optional links to related tasks

**Three-layer authentication:**
1. Primary: TLS + scheme from Agent Card (API key header, OAuth bearer, mTLS cert)
2. Authorisation: Server validates client permissions per resource/operation
3. In-task: Secondary credentials for agent-specific services during execution

**Push notifications:** Client registers webhook URL via `CreatePushNotificationConfig`; agent sends HTTP POST with `StreamResponse` payload on events.

**Version negotiation:** Client sends `A2A-Version` parameter (e.g., "0.3") with each request. Unsupported version returns `VersionNotSupportedError`.

---

### 2.2 Microsoft Agent Framework Registry

**Evolution:** AutoGen and Semantic Kernel are now in maintenance mode. The Microsoft Agent Framework (public preview October 2025, GA Q1 2026) converges both into a unified SDK.

**AutoGen 0.4 topic-and-subscription system (now migrating to Agent Framework):**

**Topic structure:**
- `Topic Type` -- application-defined classification (e.g., `"GitHub_Issues"`)
- `Topic Source` -- unique identifier within that type (e.g., `"github.com/{repo}/issues/{id}"`)
- String format: `Topic_Type/Topic_Source`

**Subscription types:**
| Type | Behaviour |
|------|-----------|
| `TypeSubscription` | Maps topic types to agent types (not specific IDs). Any topic matching the subscription's topic type maps to an agent ID with the subscription's agent type and the topic source as agent key. |
| `DefaultSubscription` | All agents subscribe to a default topic type |

**Agent identity:** Dual-component IDs (agent type + agent key), enabling the runtime to **instantiate agents dynamically** based on topic sources. If a message arrives for an agent that does not yet exist, the runtime creates a new instance.

**Multi-tenant routing:** Topic sources become data-dependent identifiers (user sessions, issue numbers), enabling isolated agent instances per tenant without hardcoding.

**Distributed runtime (experimental):**
- Host service maintains connections to all active worker runtimes
- Facilitates message delivery and keeps sessions for direct messages (RPCs)
- Subscription Manager manages which agents subscribe to which topics
- gRPC-backed for high-performance cross-process communication
- Workers register with host, receive forwarded messages

**Microsoft Entra Agent Registry (enterprise):**
- Single source of truth for all agents in an organisation
- IT leaders gain comprehensive inventory of all agents
- Agent Store for user discovery within Microsoft 365 Copilot and Teams
- Declarative agents: YAML/JSON definitions allow developers to specify prompts, roles, and tools declaratively

---

### 2.3 Agent Name Service (ANS) -- OWASP Standard

**Released:** May 2025 by OWASP. Inspired by DNS for secure agent discovery.

**Naming format:**
```
protocol://agentId.capability.provider.version.extension
```
Example: `a2a://textProcessor.DocumentTranslation.AcmeCorp.v2.1.hipaa`

**Key features:**
- Protocol-agnostic registry mechanism
- Public Key Infrastructure (PKI) for agent identity and trust
- Protocol adapter layer -- each external protocol (MCP, A2A, etc.) handled by dedicated adapter
- Cross-protocol discovery: an A2A agent can locate an agent advertising MCP tools, verify identity via PKI, and interact through a protocol gateway
- Capability-aware resolution: query the registry to find agents by capability, inspect abilities, obtain connection details

---

### 2.4 Anthropic Agent Skills Specification

**Released:** December 2025 as an open standard. Adopted by OpenAI for Codex CLI and ChatGPT.

**SKILL.md format:**

```yaml
---
name: pdf-processing
description: Extract text and tables from PDF files, fill forms, merge documents.
  Use when working with PDF documents or when the user mentions PDFs, forms,
  or document extraction.
license: Apache-2.0
compatibility: Requires poppler-utils and access to the internet
metadata:
  author: example-org
  version: "1.0"
allowed-tools: Bash(poppler:*) Read Write
---

# PDF Processing Instructions

## Steps
1. Identify the PDF file path
2. Use poppler-utils to extract text...
```

**Directory structure:**
```
skill-name/
  SKILL.md          # Required -- metadata + instructions
  scripts/          # Optional -- executable code
  references/       # Optional -- detailed docs loaded on demand
  assets/           # Optional -- templates, images, data files
```

**Frontmatter fields:**
| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Max 64 chars, lowercase + hyphens, must match directory name |
| `description` | Yes | Max 1024 chars, what it does and when to use it |
| `license` | No | License name or reference to bundled file |
| `compatibility` | No | Max 500 chars, environment requirements |
| `metadata` | No | Arbitrary key-value mapping |
| `allowed-tools` | No | Space-delimited list of pre-approved tools (experimental) |

**Progressive disclosure (3-tier context loading):**
1. **Metadata** (~100 tokens): `name` and `description` loaded at startup for ALL skills
2. **Instructions** (<5000 tokens recommended): Full `SKILL.md` body loaded when skill is activated
3. **Resources** (as needed): Files in `scripts/`, `references/`, `assets/` loaded only when required

**Capability matching:** The agent loads all skill names and descriptions at startup. When a user request arrives, the agent matches the request against skill descriptions using semantic similarity. Only the matched skill's full instructions are loaded into context. This is "progressive disclosure" -- minimising token usage while maximising capability.

---

### 2.5 Five Registry Approaches Compared (2025 Landscape)

| Approach | Discovery Model | Identity | Capability Matching |
|----------|----------------|----------|---------------------|
| **MCP Registry** | Centralised publication | Server-assigned | Tool schema matching |
| **A2A Agent Cards** | Decentralised JSON manifests at well-known URLs | Self-declared | Skills array with input/output modes |
| **AGNTCY Agent Directory** | IPFS-based with semantic discovery | Decentralised | Semantic similarity |
| **Microsoft Entra Agent ID** | Enterprise SaaS directory | AAD-backed | Role + tool declarations |
| **ANS (OWASP)** | DNS-inspired with PKI | PKI certificates | Capability field in naming scheme |

---

## 3. Task Decomposition Strategies

### 3.1 Hierarchical Task DAG (HTDAG) -- Deep Agent

**Architecture:** From the Autonomous Deep Agent paper (Feb 2025). Models complex tasks using a Hierarchical Task DAG where nodes represent sub-tasks and directed edges capture dependencies.

**Two-stage recursive cycle:**

```
High-level Task
    |
    v
Planner (Level N)
    |
    v
Sub-task DAG (Level N+1)
    |
    v
Executor (manages execution of DAG nodes)
    |
    v
For each non-atomic node: recurse (Planner Level N+1)
For each atomic node: execute directly
```

**Dynamic decomposition principle:** The planner creates next-level sub-task DAGs **only when necessary**, based on current context and requirements. This prevents the system from committing to overly detailed plans prematurely and losing LLM focus on irrelevant details.

**Dependency expression:** Directed edges capture both data flow and control flow relationships, enabling:
- Sequential execution (A -> B: B waits for A)
- Parallel execution (A and B have no edge: run concurrently)
- Fork-join (A -> [B, C] -> D: B and C run in parallel after A, D waits for both)

**Failure handling:**
- Hierarchical structure **localises failures** within specific levels, preventing cascade effects
- When errors occur, the validator halts pending nodes and signals the planner for targeted replanning
- Replanning triggers: user interventions, direct UI co-piloting, validator-detected errors, dynamic UI changes
- System determines whether errors are benign or require correction
- Can rescind unexecuted nodes while preserving successful work

**DAG expansion:** The DAG dynamically expands when new requirements arise. New nodes are added while preserving existing ones, maintaining the invariant that completed work is never undone.

---

### 3.2 TDAG: Dynamic Task Decomposition and Agent Generation

**From:** Neural Networks journal, January 2025.

**Core innovation:** Two coupled mechanisms:
1. **Dynamic task decomposition** -- adaptively decomposes based on evolving requirements, updating subtask lists based on completion status of preceding subtasks
2. **Agent generation** -- dynamically creates specialised subagents for specific subtasks rather than using a fixed pool

**Process:**
```
Complex Task
    |
    v
Decomposer: Break into subtasks [T1, T2, T3]
    |
    v
For each Ti:
    Generate specialised Agent_i (role, tools, instructions)
    Agent_i executes Ti
    On completion: update remaining subtask list based on results
    Potentially redecompose remaining work
```

**Key differentiator from static decomposition:** Remaining subtasks are re-evaluated after each completion. If T1 reveals that T3 is unnecessary or that a new T4 is needed, the decomposition adapts.

**Benchmark:** ItineraryBench -- evaluates memory, planning, and tool usage across tasks of varying complexity in travel planning scenarios.

---

### 3.3 LLM-Integrated Hierarchical Task Networks (ChatHTN)

**Approach:** Classical HTN planning augmented with LLM fallback:
- If the HTN planner has a known decomposition in its knowledge base, use it directly
- If no decomposition exists, ask the LLM for a plausible decomposition, then revert to HTN planning
- **Learning:** Successful LLM-generated decompositions are captured as new HTN methods
- **Result:** Sharp reductions in LLM call rates (often 50%+) over time, while preserving or increasing problem-solving rates

---

### 3.4 Static vs Dynamic Decomposition Comparison

| Aspect | Static Decomposition | Dynamic Decomposition |
|--------|---------------------|----------------------|
| Planning time | All upfront before execution | Incremental, just-in-time |
| Adaptability | None -- committed to initial plan | High -- redecomposes based on intermediate results |
| LLM focus | Risk of "losing focus" on premature details | Focused on immediately relevant subtasks |
| Error recovery | Must replan entire graph | Localised replanning at affected level |
| Parallelism | Known upfront from DAG structure | Discovered dynamically |
| Best for | Well-understood, repeatable workflows | Novel, complex, or unpredictable tasks |

---

### 3.5 DAG-Based Dependency Planning Techniques

**Node types:**
- **Atomic** -- indivisible operations (API call, file write, UI click)
- **Composite** -- require further decomposition into sub-DAGs

**Edge semantics:**
- **Data dependency** -- output of A is input to B
- **Control dependency** -- B cannot start until A completes (regardless of data)
- **Resource dependency** -- A and B share a resource requiring mutual exclusion

**Execution strategies:**
| Strategy | Description |
|----------|-------------|
| Topological sort | Execute nodes in dependency order, parallelise independent nodes |
| Critical path analysis | Identify longest dependency chain, optimise for overall latency |
| Level-based execution | Group nodes by depth in DAG, execute each level in parallel |
| Priority-based | Weight nodes by urgency/importance, schedule accordingly |

---

## 4. Communication Patterns Between Agents

### 4.1 Direct Messaging vs Message Bus

**Direct messaging (point-to-point):**
- Used by: OpenAI Agents SDK (handoffs), CrewAI (hub-and-spoke through manager)
- Agent A calls Agent B directly (as function call, tool call, or API request)
- Pros: Simple, low latency, easy to trace
- Cons: Tight coupling, no buffering, sender blocked until receiver responds

**Message bus (asynchronous):**
- Used by: AutoGen (topic/subscription), Confluent (Kafka), A2A protocol
- Agents publish to topics/queues; subscribers receive asynchronously
- Pros: Loose coupling, buffering, replay, horizontal scalability
- Cons: Added infrastructure, eventual consistency, harder debugging

**Hybrid approaches:**
- A2A protocol supports both synchronous request/response AND asynchronous streaming/push notifications
- Strands supports direct tool calls for simple delegation AND shared state for complex coordination
- Microsoft Agent Framework supports both direct `ChatAgent` invocation and topic-based pub/sub

---

### 4.2 Shared Blackboard / Memory

**Classical blackboard architecture (adapted for LLM agents):**

Three components:
1. **Blackboard** -- shared information space divided into public and private sections
2. **Agent Group** -- multiple specialised agents (planner, decider, critic, cleaner, conflict-resolver)
3. **Control Unit** -- LLM-based supervisor that dynamically selects which agents act in each cycle

**Communication flow:**
```
Cycle:
  1. Control Unit selects suitable agents based on blackboard state
  2. Each selected agent reads the entire blackboard
  3. Agent generates output based on blackboard contents + own expertise
  4. Agent writes output back to blackboard
  5. Repeat until consensus reached or iteration limit hit
```

**Key property:** Agents communicate **solely through the blackboard** without any direct contact. This eliminates redundant memory modules since all messages are stored on the blackboard.

**Specialised agent roles in blackboard systems:**
| Role | Function |
|------|----------|
| Decider | Assesses when sufficient information exists for final solution |
| Planner | Decomposes complex queries into subtasks |
| Critic | Identifies errors and hallucinations |
| Conflict-Resolver | Detects contradictions, facilitates discussion |
| Cleaner | Removes redundant messages to manage context size |

**Advantages over fixed multi-agent systems:** No pre-defined collaboration mechanisms required. The blackboard adjusts mechanisms "on-the-fly" according to current messages, avoiding expensive pre-training and manual construction of agent interactions.

---

### 4.3 Event-Driven Coordination (Kafka/Confluent Patterns)

**Four event-driven multi-agent patterns (Confluent, 2025):**

**Pattern 1: Orchestrator-Worker (Event-Driven)**
- Orchestrator publishes command events to a Kafka topic
- Uses key-based partitioning to distribute work across partitions
- Worker agents form a consumer group, pulling events from assigned partitions
- Workers publish output to a second topic for downstream consumption
- **Key benefit:** Orchestrator no longer manages connections to workers. Kafka handles worker failures, scaling, and rebalancing.

**Pattern 2: Hierarchical Agent (Event-Driven)**
- Multi-level orchestration via cascading topics
- Manager agents subscribe to high-level task topics and publish decomposed subtasks to lower-level topics
- Each level operates independently

**Pattern 3: Blackboard (Event-Driven)**
- Kafka topic serves as the shared blackboard
- Agents publish observations/analyses to the topic
- All agents consume from the same topic to see all contributions
- Compacted topics enable "latest state" semantics

**Pattern 4: Market-Based (Event-Driven)**
- Agents bid on tasks published to a marketplace topic
- Allocation agent evaluates bids and publishes assignments
- Enables dynamic, capability-aware task allocation

**Why event-driven for agents:**
| Benefit | Description |
|---------|-------------|
| Horizontal scalability | Add worker agents by adding consumers to consumer group |
| Loose coupling | Agents interact through topics, not direct dependencies |
| Event persistence | Durable message storage enables replay and audit |
| Backpressure | Consumer lag signals overload without dropping messages |
| Decoupled lifecycle | Agents start/stop independently without affecting others |

**Kafka + A2A + MCP convergence (2025):** The combination of Kafka (event backbone), MCP (tool discovery), and A2A (agent discovery) is emerging as a production architecture for multi-agent systems.

---

### 4.4 Pub/Sub Patterns (AutoGen Topic System)

**AutoGen's publish-subscribe model:**

```
Publisher Agent
    |
    v
Publish to Topic (type="code_review", source="repo-123/pr-456")
    |
    v
Runtime checks subscriptions:
    TypeSubscription("code_review" -> "SecurityReviewer")  -> SecurityReviewer/repo-123/pr-456
    TypeSubscription("code_review" -> "StyleChecker")      -> StyleChecker/repo-123/pr-456
    |
    v
Both agents receive the message
```

**Deployment scenarios:**
| Scenario | Topic Structure | Agent Instantiation |
|----------|----------------|---------------------|
| Single-tenant, single topic | All agents on one topic type | Shared instances |
| Single-tenant, multiple topics | Different agent types on distinct topics | Type-based routing |
| Multi-tenant | Topic sources = data-dependent IDs (user sessions, issue numbers) | Isolated instances per tenant -- runtime auto-creates |

**Key design decision:** TypeSubscription maps topic types to **agent types** (not specific agent IDs). This means the runtime can dynamically instantiate new agent instances when messages arrive for a topic source that has no existing agent. This is analogous to serverless function invocation.

---

### 4.5 LangChain Multi-Agent Architecture Selection Framework

**Four patterns with distinct communication characteristics:**

| Pattern | Communication | State | Best For |
|---------|--------------|-------|----------|
| **Subagents** | Centralised -- supervisor calls subagents as tools | Isolated per subagent | Multiple domains + parallel execution |
| **Skills** | Single agent loads specialised prompts on demand | Accumulated in conversation history | Many specialisations, lightweight |
| **Handoffs** | State-driven agent transitions via tool calls | Survives across turns | Sequential workflows + state transitions |
| **Router** | Parallel dispatch to specialist agents | Stateless per request | Parallel sources + result synthesis |

**Performance characteristics:**
- **Single requests:** Handoffs, Skills, Router use 3 LLM calls; Subagents use 4
- **Repeat requests:** Skills/Handoffs save 40% through context reuse
- **Multi-domain queries:** Subagents/Router use 67% fewer tokens through isolation

---

## 5. Cross-Cutting Concerns

### 5.1 Context and State Management

**Key challenge:** Context windows grow rapidly as agents accumulate reasoning, tool results, and intermediate outputs.

**Strategies observed across frameworks:**
| Strategy | Used By | Mechanism |
|----------|---------|-----------|
| Context compaction | Microsoft, LangGraph | Summarisation between agents |
| Nested history | OpenAI Agents SDK | `nest_handoff_history` collapses prior conversation into summary |
| Progressive disclosure | Anthropic Skills | Load only relevant skill context on demand |
| Shared state keys | Google ADK, Strands | Agents write to disjoint state keys |
| External persistence | Microsoft, A2A | Durable store for task progress; agents resume after interruptions |
| Blackboard | bMAS, Confluent | Shared memory space; cleaner agent removes redundant messages |

### 5.2 Human-in-the-Loop Patterns

**Approaches across frameworks:**
- **OpenAI:** `execute_tools=False` on action agents creates approval gates
- **CrewAI:** Senior agents can override junior decisions
- **Google ADK:** Custom tools triggering external approval systems; execution pauses
- **Microsoft Magentic:** Task ledger reviewed by human before agent execution
- **A2A:** `input-required` state pauses task until human provides additional context
- **Cortex:** Authority tiers (`AskMeFirst`) map directly to approval gating

### 5.3 Reliability and Error Handling

**Best practices across frameworks:**
1. **Timeout and retry** -- all agent invocations should have bounded execution time
2. **Circuit breakers** -- prevent cascade failures when downstream agents fail
3. **Output validation** -- validate agent output before passing to next agent
4. **Iteration caps** -- prevent infinite loops (especially in maker-checker and handoff patterns)
5. **Checkpoint/restart** -- persist state at key points to resume after interruptions
6. **Graceful degradation** -- handle agent faults without total system failure
7. **Dead letter handling** -- capture failed messages for later analysis

### 5.4 Security Considerations

**Multi-agent security concerns:**
- Each agent should follow **principle of least privilege** for tools and data access
- **Security trimming** -- agents must not return data inaccessible to the requesting user
- **Content safety guardrails** at multiple points: user input, tool calls, tool responses, final output
- **Agent identity verification** -- ANS uses PKI, A2A uses signed Agent Cards
- **Secure networking** -- TLS between agents, especially in distributed runtimes

---

## 6. Implications for Cortex

### 6.1 Validation of Existing Architecture

The research across 10+ frameworks and standards validates Cortex's existing design decisions:

| Cortex Feature | Industry Validation |
|---------------|---------------------|
| RabbitMQ message bus | Confluent patterns show event-driven messaging is the production choice for multi-agent systems |
| `IAgent` interface | Universal across all frameworks -- agents are first-class entities with identity and capabilities |
| `IAgentRegistry` with capabilities | Maps to A2A Agent Cards, ANS capability-aware resolution, ADK agent descriptions |
| `AgentHarness` per agent | Maps to OpenAI's per-agent runner, Strands' agent-as-tool pattern |
| `AgentRuntime` as IHostedService | Maps to AutoGen's distributed runtime host service |
| Authority tiers | Maps to approval gating across all frameworks (OpenAI execute_tools, ADK human-in-loop, Microsoft Magentic) |
| `DelegationTracker` | Maps to A2A task lifecycle, LangGraph state management |
| Team model | Maps to CrewAI crews, Claude Code TeammateTool teams, Strands swarms |
| Topic exchange routing | Maps directly to AutoGen TypeSubscription |

### 6.2 Patterns to Prioritise for Implementation

Based on the research, the following patterns have the highest industry convergence and practical value:

**Tier 1 -- Foundational (already partially in place):**
1. **Sequential pipeline** -- chain agents via `ReplyTo` queue routing
2. **Handoff** -- authority-based delegation with `FromAgentId` tracking
3. **Concurrent fan-out/gather** -- publish to N agent queues, collect replies

**Tier 2 -- High Value:**
4. **Orchestrator-Worker** -- manager agent decomposes tasks, delegates via `Send`-style dispatch
5. **Maker-Checker** -- generator + validator loop with iteration caps
6. **Capability-based routing** -- match task requirements to `AgentCapability` registrations

**Tier 3 -- Advanced:**
7. **Blackboard** -- shared Kafka/RabbitMQ topic as collective memory
8. **Magentic/Task Ledger** -- dynamic plan building with agent consultation
9. **Dynamic agent generation** -- create specialised agents on-the-fly (TDAG pattern)

### 6.3 Specific Technical Recommendations

**Agent Card equivalent for Cortex agents:**
The A2A Agent Card and Anthropic SKILL.md patterns suggest extending `AgentCapability` with richer metadata:
- Input/output content types (matching A2A skills)
- Tool declarations (matching SKILL.md `allowed-tools`)
- Compatibility/environment requirements
- Progressive disclosure -- summary loaded at registry time, full details loaded on invocation

**Task decomposition support:**
The HTDAG and TDAG research shows that `DelegationRecord` should evolve to support:
- `BlockedBy` / `Blocks` relationships (DAG edges)
- Task status: `pending` -> `in_progress` -> `completed` / `failed`
- Dynamic re-decomposition (update remaining tasks based on completed task results)
- Hierarchy: parent task / child subtask relationships

**Communication topology:**
| Pattern | RabbitMQ Implementation |
|---------|------------------------|
| Direct message | Publish to `agent.{targetId}` queue |
| Team broadcast | Fanout exchange `team.{teamId}` |
| Orchestrator-Worker dispatch | Topic exchange with routing key per worker type |
| Competing consumers (swarm) | Multiple agents consume from shared queue |
| Blackboard | Topic with compacted message retention |
| Event-driven coordination | Topic exchange with capability-based routing keys |

**Dynamic agent instantiation:**
AutoGen's pattern of auto-creating agent instances based on topic sources maps well to Cortex's `IAgentRuntime.StartAgentAsync`. When a message arrives for an agent type with no running instance, the runtime could automatically instantiate one.

---

## Sources

### Frameworks and SDKs
- [OpenAI Agents SDK -- Handoffs](https://openai.github.io/openai-agents-python/handoffs/)
- [OpenAI Agents SDK -- GitHub](https://github.com/openai/openai-agents-python)
- [LangGraph -- Agent Orchestration Framework](https://www.langchain.com/langgraph)
- [LangGraph Orchestrator-Worker Pattern (MLPills)](https://mlpills.substack.com/p/extra-1-orchestrator-worker-pattern)
- [LangGraph Orchestrator-Worker Design Pattern](https://ai.plainenglish.io/built-with-langgraph-31-orchestrator-worker-design-pattern-aa4ed663fc17)
- [LangGraph Send API for Dynamic Parallel Execution](https://dev.to/sreeni5018/leveraging-langgraphs-send-api-for-dynamic-and-parallel-workflow-execution-4pgd)
- [Scaling LangGraph Agents: Parallelization, Subgraphs, Map-Reduce](https://aipractitioner.substack.com/p/scaling-langgraph-agents-parallelization)
- [CrewAI Hierarchical Process](https://docs.crewai.com/en/learn/hierarchical-process)
- [CrewAI Hierarchical Delegation Guide (ActiveWizards)](https://activewizards.com/blog/hierarchical-ai-agents-a-guide-to-crewai-delegation)
- [CrewAI allowed_agents PR](https://github.com/crewAIInc/crewAI/pull/2068)
- [AWS Strands Agents Multi-Agent Patterns](https://strandsagents.com/latest/documentation/docs/user-guide/concepts/multi-agent/multi-agent-patterns/)
- [AWS Strands Agents 1.0 Introduction](https://aws.amazon.com/blogs/opensource/introducing-strands-agents-1-0-production-ready-multi-agent-orchestration-made-simple/)
- [AWS Strands Multi-Agent Collaboration](https://aws.amazon.com/blogs/devops/multi-agent-collaboration-with-strands/)
- [Google ADK Multi-Agent Patterns](https://developers.googleblog.com/developers-guide-to-multi-agent-patterns-in-adk/)
- [Microsoft Agent Framework Overview](https://learn.microsoft.com/en-us/agent-framework/overview/)
- [Microsoft Agent Framework -- C# Developer Guide](https://www.devleader.ca/2026/02/21/microsoft-agent-framework-in-c-complete-developer-guide)
- [Microsoft Agent Framework GitHub](https://github.com/microsoft/agent-framework)
- [AutoGen Topic and Subscription](https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/core-concepts/topic-and-subscription.html)
- [AutoGen Distributed Agent Runtime](https://microsoft.github.io/autogen/0.4.0.dev2//user-guide/core-user-guide/framework/distributed-agent-runtime.html)

### Standards and Protocols
- [Google A2A Protocol Specification](https://a2a-protocol.org/latest/specification/)
- [A2A Protocol -- Google Developers Blog](https://developers.googleblog.com/en/a2a-a-new-era-of-agent-interoperability/)
- [A2A Protocol Upgrade -- Google Cloud Blog](https://cloud.google.com/blog/products/ai-machine-learning/agent2agent-protocol-is-getting-an-upgrade)
- [A2A Protocol -- IBM](https://www.ibm.com/think/topics/agent2agent-protocol)
- [Anthropic Agent Skills Specification](https://agentskills.io/specification)
- [Anthropic Agent Skills -- Engineering Blog](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills)
- [Agent Skills GitHub](https://github.com/anthropics/skills)
- [Agent Name Service (ANS) -- OWASP](https://arxiv.org/abs/2505.10609)
- [ANS -- InfoQ](https://www.infoq.com/news/2025/06/secure-agent-discovery-ans/)

### Architecture Guidance
- [Microsoft Azure AI Agent Orchestration Patterns](https://learn.microsoft.com/en-us/azure/architecture/ai-ml/guide/ai-agent-design-patterns)
- [Choosing the Right Multi-Agent Architecture -- LangChain Blog](https://blog.langchain.com/choosing-the-right-multi-agent-architecture/)
- [Confluent: Four Design Patterns for Event-Driven Multi-Agent Systems](https://www.confluent.io/blog/event-driven-multi-agent-systems/)
- [Confluent: Multi-Agent Orchestrator Using Flink and Kafka](https://www.confluent.io/blog/multi-agent-orchestrator-using-flink-and-kafka/)
- [Kafka + A2A + MCP Architecture](https://www.kai-waehner.de/blog/2025/05/26/agentic-ai-with-the-agent2agent-protocol-a2a-and-mcp-using-apache-kafka-as-event-broker/)
- [The AI Agent Framework Landscape in 2025](https://medium.com/@hieutrantrung.it/the-ai-agent-framework-landscape-in-2025-what-changed-and-what-matters-3cd9b07ef2c3)
- [Agentic Frameworks in 2026: What Actually Works in Production](https://zircon.tech/blog/agentic-frameworks-in-2026-what-actually-works-in-production/)
- [AI Agent Registry -- Complete Guide](https://www.truefoundry.com/blog/ai-agent-registry)
- [Evolution of AI Agent Registry Solutions](https://arxiv.org/abs/2508.03095)

### Research Papers
- [Autonomous Deep Agent (HTDAG)](https://arxiv.org/html/2502.07056v1)
- [TDAG: Dynamic Task Decomposition and Agent Generation](https://arxiv.org/abs/2402.10178)
- [LLM-based Multi-Agent Blackboard System](https://arxiv.org/html/2507.01701v1)
- [Blackboard Architecture for Multi-Agent LLM Systems (PDF)](https://arxiv.org/pdf/2510.01285)
- [Multi-Agent Orchestration Survey](https://arxiv.org/html/2601.13671v1)
- [MCP for Multi-Agent Systems](https://arxiv.org/html/2504.21030v1)
