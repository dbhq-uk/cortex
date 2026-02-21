# Cortex — Digital Company Operating System

## Vision

Cortex is an open-source, message-driven framework for running a business through a unified system where humans and AI agents collaborate as equals. Built in C# under the `dbhq-uk` organisation.

It is not a chatbot, not a coding tool — it is a digital operating system for a company. Every real-world organisational concept (delegation, authority, dispute resolution, succession, arbitration) is encoded as executable software.

The long-term ambition is to open source the framework, prove it internally, and eventually provide it as a managed service for non-technical users.

---

## Organisational Model: Delegation with Override

The structure is a hybrid — flat enough for speed, hierarchical enough for control.

### Principles

- **The founder/CEO sits at the top** — highest authority in the system. Primary role is decision-making and delegation, not execution.
- **Every executive has a Chief of Staff (CoS) agent** — the CoS is the gateway between the human and the rest of the system.
- **The CoS handles operations** — routing, coordination, triage, gatekeeping attention. It is deliberately NOT a strategic thinker. It doesn't have opinions about content. It's smart about logistics.
- **Teams self-assemble around goals** — the CoS (or a builder agent) identifies what skills are needed, pulls in the right agents, creates a team, and manages the lifecycle.
- **Humans and AI agents are interchangeable at the routing layer** — a task goes to whoever can do it, human or AI. The system doesn't fundamentally distinguish between them.
- **The founder can bypass the chain at any time** — direct channels to any agent, but the CoS stays informed via summaries.
- **Authority flows downward and is definable per-action** — not per-agent, not binary.

### Authority Model

Authority is a set of claims that flow with every message through the queue. Claims can be granted, revoked, or escalated by anyone above.

Three tiers of action authority:

| Tier                  | Description                      | Examples                                                    |
|-----------------------|----------------------------------|-------------------------------------------------------------|
| **Just do it**        | Internal, no external footprint  | Log email, update task board, file a document, tag a record |
| **Do it and show me** | Prepare and present for approval | Draft email reply, prepare analysis, create deployment plan |
| **Ask me first**      | Novel, high-stakes, or uncertain | Send an email, publish content, spend money, commit to main |

Authority is NOT static:

- Can be elevated over time as trust builds
- Can be delegated to other humans
- Can be inherited (succession)
- Flows as claims/tokens attached to every message in the queue

### Dispute Resolution

When two agents (or their humans) have conflicting instructions:

- CoS agents employ **dispute management agents** as a skill
- The dispute agent analyses both instructions, checks context, checks authority levels, attempts reconciliation
- Only escalates to humans if it cannot resolve — and presents the conflict clearly with options
- Supports real-world patterns: arbitration, mediation, escalation

---

## Architecture

### Core: Message Queues (RabbitMQ)

Everything communicates through message queues. RabbitMQ chosen for durability — messages persist, survive restarts, and wait for consumers.

Every message carries:

- **Reference code** — unique traceable ID (e.g. `CTX-2026-0221-001`)
- **Authority claims** — who authorised this, what level, what actions are permitted
- **Context chain** — parent message, original goal, team ID, channel
- **Priority and SLA** — how long before this escalates

### Queue Topology Maps to Org Chart

- **Founder's inbox queue** — highest priority
- **CoS inbox queue** — everything flows through here first
- **Specialist queues** — each agent type has a queue
- **Team queues** — ephemeral, created when a team assembles
- **Dead letter / escalation queue** — failures and timeouts escalate upward

### Infrastructure

| Component                     | Where                   | Always On? |
|-------------------------------|-------------------------|------------|
| Email listener (webhook)      | Managed platform        | Yes        |
| Web UI                        | Managed platform        | Yes        |
| RabbitMQ                      | VM                      | Yes        |
| Agent execution / Claude Code | VM                      | Yes        |
| Mobile UI                     | Phase 2                 | —          |

### Framework Language

- **C# is the framework language**
- **Other languages are skills, not framework citizens**
- **Python may become first-class later**
- **Claude Code integration** — the framework wraps Claude CLI

### Cost Awareness

- Claude Code usage on subscription is effectively free at point of use
- Direct API calls have per-call cost
- The CoS is cost-aware
- Cost tracking per reference code

---

## Channels (Context Spaces)

**Channel IS context.**

- **Default/main channel** — catch-all
- **Named channels** — per-project, per-domain, per-concern
- **Direct channels** — bypass CoS for specialist access
- **CoS awareness** — summaries when direct channel sessions end

---

## The CoS Agent

### What the CoS IS

- The user's operational interface
- Triage and routing engine
- Gatekeeper of attention
- Coordinator of teams and task lifecycles
- Authority enforcer

### What the CoS IS NOT

- A strategic thinker
- A creative agent
- A bottleneck

---

## Everything is a Skill

- A skill is a markdown file
- Skills can wrap anything
- Skills are discoverable
- Skills are composable
- Skills can be authored by agents
- Skills are language-agnostic

---

## Teams — Self-Assembly

1. A goal arrives
2. CoS analyses what skills are needed
3. Team is created with reference code
4. Agents pulled in by capability matching
5. Work progresses through team queue
6. Results land for human approval
7. Team dissolves

---

## Build Phases

### Phase 1 — The Spine

- RabbitMQ + C# message bus abstraction
- Agent interface with human and AI implementations
- Authority claims model
- Reference code tracking
- CoS agent — basic triage and routing
- Email listener and skill
- Web UI

### Phase 2 — Teams and Mobile

- Dynamic team creation
- Claude Code CLI wrapper
- Multi-repo context loading
- Mobile UI
- Cost tracking

### Phase 3 — Self-Improvement and Scale

- Skill authoring agents
- Team performance tracking
- Dispute resolution skills

### Phase 4 — Productisation

- Managed service infrastructure
- Multi-tenant architecture
- Pricing model
