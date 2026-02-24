# Playbooks & Systematisation Agent Design

## Problem

When AI agents perform ad-hoc work, the results can be excellent but the approach is lost. There is no mechanism to capture what worked — the process, the decomposition, the communication patterns, the actual code — and turn it into something repeatable. Every time a similar task arises, agents start from scratch.

## Solution

Three new first-class concepts in Cortex:

1. **Workflow Trace** — structured recording of multi-agent collaborations
2. **Playbook** — self-contained, language-agnostic, git-versioned package defining a repeatable business function
3. **Systematisation Agent** — an `IAgent` that consumes workflow traces and produces playbooks

The relationship between a Skill and a Playbook:

| Concept | Represents | Example |
|---------|-----------|---------|
| **Skill** | What one agent can do | "Draft email," "Analyse CSV," "Query API" |
| **Playbook** | What a team does together | "Client onboarding," "Monthly reporting," "Incident response" |

## Flow

```
Ad-hoc collaboration happens
        ↓
Workflow Trace captures what happened
        ↓
Human says "systematise this"
        ↓
Systematisation Agent reads the trace
        ↓
Analyses & decomposes into granular skills
        ↓
Generates a Playbook (DoItAndShowMe)
        ↓
Human reviews & approves
        ↓
Playbook committed to repo, registered, reusable
```

## Workflow Trace

The trace collector is a silent participant on the message bus. It does not interfere — it watches and records.

### Trace event fields

- Trace ID (correlates the whole collaboration)
- Timestamp
- Source agent ID and target agent ID
- Message envelope (full message including authority claims)
- Skill invoked (if applicable)
- Delegation record (if applicable)
- Inputs and outputs
- Duration
- Outcome (success, failure, escalation)

### Trace lifecycle

- A trace starts when a new top-level message enters the system (no parent message ID)
- Subsequent messages correlated back to the root attach to the same trace
- A trace ends when the final reply reaches the originator or a timeout expires
- Traces are stored as structured, queryable records — not log files

### Design choices

- **Opt-in per collaboration**, not always-on. A traced collaboration is started explicitly, or the system defaults to tracing when multiple agents are involved. This keeps it lightweight.
- **Ephemeral storage.** Initially in-memory with serialisation to JSON files in a `traces/` directory. Traces are working data — the playbook is what survives.

## Playbook Structure

A playbook is a directory in the repo under `playbooks/`. Fully self-contained, fully portable.

```
playbooks/
  client-onboarding/
    playbook.md                  — orchestration definition
    requirements.md              — runtime dependencies
    skills/
      verify-identity.md         — skill definitions (what, not how)
      create-crm-record.md
      send-welcome-email.md
      schedule-kickoff.md
    contracts/
      onboarding-request.schema.json
      client-profile.schema.json
      verification-result.schema.json
    code/
      verify_identity.py
      crm_integration.cs
      welcome_email.py
      kickoff_scheduler.py
    tests/
      test_verify_identity.py
      CrmIntegrationTests.cs
      test_welcome_email.py
```

### playbook.md

The heart of the package. Defines:

- **Name and description** — what business function this serves
- **Trigger** — what kicks it off (message type, human command, schedule)
- **Agents** — the roles involved, what skills each needs
- **DAG** — workflow steps, ordering, parallelism, branching conditions
- **Authority gates** — which steps are JustDoIt, DoItAndShowMe, AskMeFirst
- **Message flow** — what data passes between steps, referencing contract schemas
- **Error handling** — what happens when a step fails, retry policies, escalation

### Skill markdown files

Each skill file references its code file and declares:

- What the skill does
- Which code file implements it and in what language
- Input/output schemas (referencing contracts)
- Executor type (python, csharp, cli, api)

### Contracts

JSON Schema — language-neutral, validatable, readable by humans and agents. No C# records, no Python dataclasses. The runtime generates typed objects from these as needed.

### Requirements

Lists everything needed to run: Python version, .NET version, API keys (by name, not value), external services, Docker images. Everything someone needs to set up the environment.

### Language agnosticism

A playbook contains no language-specific framework code. Skills can wrap C#, Python, CLI, or API calls. The playbook orchestration definition is pure markup. Code lives in the `code/` directory in whatever language is appropriate for each skill. The playbook could be executed by a C# runtime today and a Rust runtime tomorrow without changing a line.

## Systematisation Agent

Implements `IAgent`. Lives on the message bus like any other agent.

### Trigger

Receives a message containing a trace ID and the command "systematise this." Can come from a human via the web UI or from another agent.

### Analysis process — five phases

**1. Trace Reading** — Pull the full workflow trace. Reconstruct the timeline: who did what, in what order, what data flowed where, what succeeded, what was retried.

**2. Decomposition** — The architect step. For each step in the trace, ask: "Is this one concern or several?" A single skill that did research, transformation, and validation becomes three separate skills with clear boundaries. Identify natural seams — where data format changes, where responsibility shifts, where different expertise is needed. A monolithic skill executing once might need to become multiple agents working at a more granular level, communicating with each other.

**3. DAG Design** — Lay out the decomposed skills as a workflow. Identify what can run in parallel, what is sequential, where branching occurs, where authority gates belong. This may look different from what actually happened — the ad-hoc collaboration might have been serial when parts could have been parallel.

**4. Code Extraction & Generation** — Pull actual code from the trace where it exists. Generate new code where decomposition created new boundaries. Write contract schemas based on what actually flowed between steps. Generate tests.

**5. Package Assembly** — Write playbook.md, skill definitions, contracts, code files, tests, requirements. The full playbook directory.

### Output behaviour

Default: **DoItAndShowMe**. The agent presents the complete playbook to the human for review. It never commits without approval.

On request: **Collaborative refinement**. The human can ask questions and the agent revises. "Should step B always happen or only when X? What if C fails?"

Once approved, the agent commits the playbook to the repo and registers it in the playbook registry.

### Aspiration: heuristic detection

The initial trigger is explicit ("systematise this"). The aspiration is heuristic detection — the agent watches traces over time and proactively suggests codifying patterns when it spots similar ad-hoc collaborations recurring. Explicit systematisation builds the corpus that makes heuristic detection possible.

## Playbook Execution

A playbook in the repo is a definition. The Playbook Runner executes it.

### Execution steps

1. **Parse** playbook.md — build the DAG in memory
2. **Validate** requirements — dependencies available, API keys configured, runtimes present
3. **Provision agents** — for each role, find or spin up an agent via `IAgentRuntime.StartAgentAsync`, loaded with the relevant skills
4. **Execute the DAG** — step through the workflow, passing data between agents using contract schemas. Respect authority gates — pause at AskMeFirst, present at DoItAndShowMe
5. **Trace the execution** — every playbook run produces a workflow trace automatically
6. **Report** — summarise the outcome to whoever triggered it

### Feedback loop

Step 5 is critical. Playbook executions produce traces. Traces can be systematised. This means playbook runs can improve the playbook itself — the system refines with use.

```
Run playbook → trace the run → spot improvements → re-systematise → better playbook
```

### Authority in execution

The playbook definition declares authority tiers per step. The invocation also carries authority claims. A junior team member triggering a playbook may hit more AskMeFirst gates than a senior one.

## Integration With Existing Cortex

### Unchanged concepts

- `IAgent` — the systematisation agent implements this directly
- `IMessageBus` — traces are collected from bus traffic, no bus changes needed
- `AuthorityClaim` / `AuthorityTier` — playbooks use the existing authority model
- `ISkillRegistry` — playbook skills register here alongside standalone skills
- `DelegationRecord` — playbook execution creates delegations through the existing tracker
- `IAgentRuntime` — playbook runner provisions agents through the existing runtime

### New concepts

| Concept | Location | Purpose |
|---------|----------|---------|
| `WorkflowTrace` | `Cortex.Core.Tracing` | Trace event records, trace ID correlation |
| `ITraceCollector` | `Cortex.Messaging` | Sits on the bus, captures trace events |
| `Playbook` | `Cortex.Skills.Playbooks` | Playbook definition model, parser for playbook.md |
| `IPlaybookRegistry` | `Cortex.Skills.Playbooks` | Discover and retrieve registered playbooks |
| `IPlaybookRunner` | `Cortex.Skills.Playbooks` | Parse, validate, provision, execute DAG |
| `SystematisationAgent` | `Cortex.Agents` | Reads traces, produces playbooks |

### Relationship to roadmap

- Phase 2 **coordination message types** (#13) — playbook execution will use these
- Phase 2 **task dependency DAG** (#11) — the playbook DAG model builds on this
- Phase 3 **orchestration engine** (#17) — the playbook runner is this, or a major part of it
- Phase 3 **self-improving skills** (#20) — the feedback loop from traced playbook runs enables this
- Phase 3 **TeamArchitectAgent** (#16) — the systematisation agent is a specialisation of this concept

The playbook concept does not replace the roadmap — it gives it purpose. The orchestration engine exists to run playbooks. The DAG model exists to define playbooks. The team architect exists to build and improve playbooks.
