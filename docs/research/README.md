# Multi-Agent Orchestration Research -- Master Synthesis

**Date:** 2026-02-24
**Purpose:** Unified reference document tying together all agent orchestration research. Provides thematic cross-references across the research corpus, a consolidated Cortex architecture alignment, gap analysis, and implementation roadmap.

---

## Table of Contents

1. [Research Corpus](#1-research-corpus)
2. [Thematic Index](#2-thematic-index)
   - [Orchestration Patterns](#21-orchestration-patterns)
   - [Agent Discovery & Registration](#22-agent-discovery--registration)
   - [Task Decomposition](#23-task-decomposition)
   - [Communication & Messaging](#24-communication--messaging)
   - [Team Building & Composition](#25-team-building--composition)
   - [Self-Organisation & Swarm Intelligence](#26-self-organisation--swarm-intelligence)
   - [Agent-Building Agents & Self-Improvement](#27-agent-building-agents--self-improvement)
   - [Error Resilience & Recovery](#28-error-resilience--recovery)
   - [Observability & Knowledge Management](#29-observability--knowledge-management)
   - [Safety & Alignment](#210-safety--alignment)
   - [Agent Economies & Marketplaces](#211-agent-economies--marketplaces)
   - [Memory Architecture](#212-memory-architecture)
3. [Framework Landscape](#3-framework-landscape)
4. [Cortex Architecture Alignment](#4-cortex-architecture-alignment)
5. [Consolidated Gap Analysis](#5-consolidated-gap-analysis)
6. [Implementation Roadmap](#6-implementation-roadmap)
7. [Key Takeaways](#7-key-takeaways)

---

## 1. Research Corpus

Seven research documents totalling ~6,100 lines and ~290KB of analysis. Each document is a deep-dive into a specific facet of multi-agent orchestration.

| # | Document | Lines | Focus | Key Topics |
|---|----------|-------|-------|------------|
| 1 | [Initial Orchestration Patterns](./2026-02-23-agent-orchestration-patterns.md) | 318 | Foundation | Claude Code TeammateTool primitives, 6 orchestration patterns, Cortex mapping, gap analysis, Issue #2 recommendations |
| 2 | [Deep Orchestration Research](./2026-02-24-agent-orchestration-deep-research.md) | 866 | Frameworks & Standards | Orchestrator-worker across 6 frameworks, agent registry patterns (A2A, ANS, SKILL.md), task decomposition (HTDAG, TDAG, ChatHTN), communication patterns |
| 3 | [Agent Swarm Frameworks](./2026-02-24-agent-swarm-frameworks.md) | 1,215 | Framework Survey | OpenAI Swarm/Agents SDK, CrewAI, AutoGen, LangGraph, Google A2A, Anthropic, MetaGPT, ChatDev -- code examples, architecture, strengths/weaknesses |
| 4 | [Prompt-Based Meta-Orchestration Patterns](./2026-02-24-prompt-based-meta-orchestration-patterns.md) | 1,514 | Role Archetypes | 11 orchestration role archetypes, operational checklists with SLO targets, integration map, model tier strategy, three-phase execution, knowledge management, observability |
| 5 | [Self-Building & Self-Organising Swarms](./2026-02-24-self-building-self-organising-agent-swarms.md) | 740 | Emergent Systems | Emergent behaviour, stigmergy, ACO, self-healing networks, agent-building agents (Voyager, ADAS, AgentVerse), network topologies, Boids rules, gossip protocols |
| 6 | [Team-Building Agents](./2026-02-24-team-building-agents.md) | 780 | Team Composition | CrewAI/AutoGen/MetaGPT/ChatDev team patterns, dynamic optimisation, capability gap analysis, templates, end-to-end "build me a team" flows, TeamArchitectAgent design |
| 7 | [Cutting-Edge Multi-Agent Systems](./2026-02-24-cutting-edge-multi-agent-systems.md) | 701 | Frontier Research | Agent marketplaces (AEX, AITP), recursive self-improvement (STOP, Godel Agent, DARWIN), communication protocols (FIPA, A2A, Agora, IoA), memory systems, safety |

### Reading Order

**For understanding the landscape:** 1 → 2 → 3 (foundations to frameworks)

**For building Cortex's team composition:** 6 → 4 → 5 (team building → role archetypes → self-organisation)

**For future vision:** 7 → 5 (frontier research → emergent systems)

---

## 2. Thematic Index

Each theme below lists where in the corpus to find detailed coverage, with the primary reference listed first.

### 2.1 Orchestration Patterns

Six core patterns identified across all frameworks:

| Pattern | Description | Primary Reference | Also Covered In |
|---------|-------------|-------------------|-----------------|
| **Sequential Pipeline** | Tasks with linear `blockedBy` dependencies | [Initial Patterns §1.2](./2026-02-23-agent-orchestration-patterns.md#12-orchestration-patterns-identified) | [Deep Research §1.2](./2026-02-24-agent-orchestration-deep-research.md#12-langgraph) (LangGraph), [Meta-Orchestration §3.4](./2026-02-24-prompt-based-meta-orchestration-patterns.md#34-coordination-patterns) |
| **Parallel Specialists (Council)** | Leader + N specialists reviewing concurrently | [Initial Patterns §1.2](./2026-02-23-agent-orchestration-patterns.md#12-orchestration-patterns-identified) | [Swarm Frameworks §3.3](./2026-02-24-agent-swarm-frameworks.md#33-team-patterns) (AutoGen teams) |
| **Self-Organising Swarm** | Workers poll shared task pool, claim work | [Initial Patterns §1.2](./2026-02-23-agent-orchestration-patterns.md#12-orchestration-patterns-identified) | [Self-Building Swarms §1](./2026-02-24-self-building-self-organising-agent-swarms.md#1-self-organising-multi-agent-systems), [Swarm Frameworks §3.3](./2026-02-24-agent-swarm-frameworks.md#33-team-patterns) (AutoGen Swarm) |
| **Orchestrator-Worker** | Manager decomposes and delegates to workers | [Deep Research §1](./2026-02-24-agent-orchestration-deep-research.md#1-orchestrator-worker-pattern) | [Swarm Frameworks §6.4](./2026-02-24-agent-swarm-frameworks.md#64-multi-agent-research-system-production) (Anthropic production), [Team-Building §1.2](./2026-02-24-team-building-agents.md#12-autogen----team-assembly-patterns) (Magentic-One) |
| **Handoff** | Agent transfers control to another agent | [Deep Research §1.1](./2026-02-24-agent-orchestration-deep-research.md#11-openai-swarm--agents-sdk) | [Swarm Frameworks §1](./2026-02-24-agent-swarm-frameworks.md#1-openai-swarm-and-agents-sdk), [Swarm Frameworks §3.3](./2026-02-24-agent-swarm-frameworks.md#33-team-patterns) (AutoGen) |
| **Coordinated Refactoring** | Parallel file work with join point for validation | [Initial Patterns §1.2](./2026-02-23-agent-orchestration-patterns.md#12-orchestration-patterns-identified) | [Meta-Orchestration §3.5](./2026-02-24-prompt-based-meta-orchestration-patterns.md#35-workflow-patterns) (workflow patterns) |

**Additional patterns from deep research:**

| Pattern | Reference |
|---------|-----------|
| Maker-Checker (generator + validator loop) | [Deep Research §6.2](./2026-02-24-agent-orchestration-deep-research.md#62-patterns-to-prioritise-for-implementation) |
| Magentic/Task Ledger (dynamic plan with agent consultation) | [Deep Research §6.2](./2026-02-24-agent-orchestration-deep-research.md#62-patterns-to-prioritise-for-implementation), [Team-Building §1.2](./2026-02-24-team-building-agents.md#12-autogen----team-assembly-patterns) |
| Blackboard (shared memory for coordination) | [Deep Research §4.2](./2026-02-24-agent-orchestration-deep-research.md#42-shared-blackboard--memory), [Cutting-Edge §5.2](./2026-02-24-cutting-edge-multi-agent-systems.md#52-shared-memory-blackboard-systems) |
| Saga (distributed transactions with compensation) | [Meta-Orchestration §3.5](./2026-02-24-prompt-based-meta-orchestration-patterns.md#35-workflow-patterns), [Initial Patterns §1.6](./2026-02-23-agent-orchestration-patterns.md#16-multi-agent-coordination-patterns) |
| Map-Reduce (fan-out work, collect and merge) | [Meta-Orchestration §3.4](./2026-02-24-prompt-based-meta-orchestration-patterns.md#34-coordination-patterns) |
| Scatter-Gather (broadcast request, collect all responses) | [Meta-Orchestration §3.4](./2026-02-24-prompt-based-meta-orchestration-patterns.md#34-coordination-patterns) |
| DAG Execution (topological sort, parallel where possible) | [Initial Patterns §1.6](./2026-02-23-agent-orchestration-patterns.md#16-multi-agent-coordination-patterns), [Deep Research §3.5](./2026-02-24-agent-orchestration-deep-research.md#35-dag-based-dependency-planning-techniques) |

### 2.2 Agent Discovery & Registration

How agents advertise capabilities and how orchestrators find the right agent.

| Mechanism | Reference |
|-----------|-----------|
| **A2A Agent Cards** (JSON at `/.well-known/agent-card.json`) | [Deep Research §2.1](./2026-02-24-agent-orchestration-deep-research.md#21-google-a2a-agent-to-agent-protocol), [Swarm Frameworks §5.3](./2026-02-24-agent-swarm-frameworks.md#53-agent-cards) |
| **Anthropic SKILL.md** (capability declaration via markdown) | [Deep Research §2.4](./2026-02-24-agent-orchestration-deep-research.md#24-anthropic-agent-skills-specification) |
| **Agent Name Service (ANS)** (OWASP standard, capability-aware resolution) | [Deep Research §2.3](./2026-02-24-agent-orchestration-deep-research.md#23-agent-name-service-ans----owasp-standard) |
| **MCP Registry** (tool-centric, near-universal adoption) | [Deep Research §2.5](./2026-02-24-agent-orchestration-deep-research.md#25-five-registry-approaches-compared-2025-landscape) |
| **Capability matching dimensions** (8-factor model: skill, performance, cost, availability, load, specialisation, compatibility, backup) | [Meta-Orchestration §3.2](./2026-02-24-prompt-based-meta-orchestration-patterns.md#32-agent-selection--capability-matching-patterns) |
| **Domain-specific routing** ("task smells" for selecting specialist agents) | [Meta-Orchestration §3.2](./2026-02-24-prompt-based-meta-orchestration-patterns.md#32-agent-selection--capability-matching-patterns) |
| **AgentRank** (decentralised reputation scoring) | [Cutting-Edge §1.2](./2026-02-24-cutting-edge-multi-agent-systems.md#12-reputation-and-trust-systems) |
| **W3C DIDs for agents** (zero-trust identity framework) | [Cutting-Edge §1.2](./2026-02-24-cutting-edge-multi-agent-systems.md#12-reputation-and-trust-systems) |
| **Cortex mapping:** `IAgentRegistry.FindByCapabilityAsync` + `AgentCapability` | [Initial Patterns §2.1](./2026-02-23-agent-orchestration-patterns.md#21-what-cortex-already-has), [Meta-Orchestration §10.4](./2026-02-24-prompt-based-meta-orchestration-patterns.md#104-agent-selection----iagentregistryfindbyCapabilityasync) |

### 2.3 Task Decomposition

How complex tasks are broken into subtasks for agent execution.

| Strategy | Reference |
|----------|-----------|
| **HTDAG** (Hierarchical Task DAG -- static pre-planning) | [Deep Research §3.1](./2026-02-24-agent-orchestration-deep-research.md#31-hierarchical-task-dag-htdag----deep-agent) |
| **TDAG** (Dynamic Task Decomposition and Agent Generation -- generates agents alongside tasks) | [Deep Research §3.2](./2026-02-24-agent-orchestration-deep-research.md#32-tdag-dynamic-task-decomposition-and-agent-generation) |
| **ChatHTN** (Hierarchical Task Networks with LLM integration) | [Deep Research §3.3](./2026-02-24-agent-orchestration-deep-research.md#33-llm-integrated-hierarchical-task-networks-chathtn) |
| **8-step decomposition procedure** (requirement analysis → subtask identification → dependency mapping → complexity assessment → resource estimation → timeline planning → risk evaluation → success criteria) | [Meta-Orchestration §3.1](./2026-02-24-prompt-based-meta-orchestration-patterns.md#31-task-decomposition-patterns) |
| **Domain-specific decomposition** (ambiguous task → sub-problems → specialist agents) | [Meta-Orchestration §3.1](./2026-02-24-prompt-based-meta-orchestration-patterns.md#31-task-decomposition-patterns) |
| **Static vs dynamic comparison** | [Deep Research §3.4](./2026-02-24-agent-orchestration-deep-research.md#34-static-vs-dynamic-decomposition-comparison) |
| **DAG-based dependency planning** (topological sort, parallel vs interleaved vs streaming execution) | [Deep Research §3.5](./2026-02-24-agent-orchestration-deep-research.md#35-dag-based-dependency-planning-techniques) |
| **Cortex mapping:** `DelegationTracker` with `BlockedBy`/`Blocks` extensions needed | [Initial Patterns §3.2](./2026-02-23-agent-orchestration-patterns.md#32-document-now-build-later-phase-2), [Deep Research §6.3](./2026-02-24-agent-orchestration-deep-research.md#63-specific-technical-recommendations) |

### 2.4 Communication & Messaging

How agents exchange information and coordinate.

| Pattern | Reference |
|---------|-----------|
| **Direct messaging vs message bus** | [Deep Research §4.1](./2026-02-24-agent-orchestration-deep-research.md#41-direct-messaging-vs-message-bus) |
| **Shared blackboard / memory** | [Deep Research §4.2](./2026-02-24-agent-orchestration-deep-research.md#42-shared-blackboard--memory), [Cutting-Edge §5.2](./2026-02-24-cutting-edge-multi-agent-systems.md#52-shared-memory-blackboard-systems) |
| **Event-driven coordination (Kafka patterns)** | [Deep Research §4.3](./2026-02-24-agent-orchestration-deep-research.md#43-event-driven-coordination-kafkaconfluent-patterns) |
| **Pub/sub (AutoGen topic system)** | [Deep Research §4.4](./2026-02-24-agent-orchestration-deep-research.md#44-pubsub-patterns-autogen-topic-system) |
| **JSON context query protocol** (per-agent request types) | [Meta-Orchestration §3.3](./2026-02-24-prompt-based-meta-orchestration-patterns.md#33-communication-protocols-json-message-formats) |
| **MetaGPT shared message pool** + `_watch()` subscriptions | [Swarm Frameworks §7.4](./2026-02-24-agent-swarm-frameworks.md#74-message-based-communication) |
| **A2A protocol** (HTTP+SSE, JSON-RPC, streaming) | [Swarm Frameworks §5](./2026-02-24-agent-swarm-frameworks.md#5-google-a2a-protocol), [Cutting-Edge §3.2](./2026-02-24-cutting-edge-multi-agent-systems.md#32-google-a2a-agent2agent-protocol) |
| **Internet of Agents layered architecture** (Layer 8 ACL, Layer 9 Semantic) | [Cutting-Edge §3.3](./2026-02-24-cutting-edge-multi-agent-systems.md#33-the-internet-of-agents-a-layered-architecture) |
| **Agora meta-protocol** (negotiate communication format at runtime) | [Cutting-Edge §3.4](./2026-02-24-cutting-edge-multi-agent-systems.md#34-agora-the-meta-protocol) |
| **FIPA ACL** (original standard, performative verbs) | [Cutting-Edge §3.1](./2026-02-24-cutting-edge-multi-agent-systems.md#31-fipa-acl-the-original-standard) |
| **8 communication topologies** (master-worker, peer-to-peer, hierarchical, pub-sub, request-reply, pipeline, scatter-gather, consensus) | [Meta-Orchestration §3.4](./2026-02-24-prompt-based-meta-orchestration-patterns.md#34-coordination-patterns) |
| **RabbitMQ as Cortex implementation** | [Initial Patterns §2.3](./2026-02-23-agent-orchestration-patterns.md#23-key-insight-rabbitmq--file-based-inboxes), [Initial Patterns §2.4](./2026-02-23-agent-orchestration-patterns.md#24-pattern-implementation-via-rabbitmq) |

### 2.5 Team Building & Composition

How teams of agents are assembled, optimised, and dissolved.

| Topic | Reference |
|-------|-----------|
| **CrewAI crew composition** (YAML configs, hierarchical manager, planning mode) | [Team-Building §1.1](./2026-02-24-team-building-agents.md#11-crewai----crew-composition-mechanisms), [Swarm Frameworks §2](./2026-02-24-agent-swarm-frameworks.md#2-crewai) |
| **AutoGen team assembly** (RoundRobin, SelectorGroupChat, Swarm, Magentic-One) | [Team-Building §1.2](./2026-02-24-team-building-agents.md#12-autogen----team-assembly-patterns), [Swarm Frameworks §3](./2026-02-24-agent-swarm-frameworks.md#3-microsoft-autogen) |
| **ChatDev CEO agent** (assembles teams from role definitions) | [Team-Building §1.3](./2026-02-24-team-building-agents.md#13-chatdev----ceo-agent-assembles-the-team), [Swarm Frameworks §8](./2026-02-24-agent-swarm-frameworks.md#8-chatdev) |
| **MetaGPT SOP-driven pipeline** (structured roles, shared message pool) | [Team-Building §1.4](./2026-02-24-team-building-agents.md#14-metagpt----sop-driven-pipeline), [Swarm Frameworks §7](./2026-02-24-agent-swarm-frameworks.md#7-metagpt) |
| **AgentVerse expert recruitment** (dynamic agent creation for task) | [Team-Building §1.5](./2026-02-24-team-building-agents.md#15-agentverse----dynamic-expert-recruitment) |
| **ADAS meta-agent search** (discovers optimal agent architectures via code) | [Team-Building §1.6](./2026-02-24-team-building-agents.md#16-adas----meta-agent-that-designs-agent-architectures), [Cutting-Edge §4.2](./2026-02-24-cutting-edge-multi-agent-systems.md#42-swarmagentic-fully-automated-system-generation) |
| **Swarms AutoSwarmBuilder** (template-free generation from description) | [Team-Building §2.5](./2026-02-24-team-building-agents.md#25-swarms-framework----autoswarmbuilder) |
| **MASFly** (dynamic adaptation at test time, Feb 2026) | [Team-Building §2.3](./2026-02-24-team-building-agents.md#23-masfly----dynamic-adaptation-at-test-time-feb-2026) |
| **SwarmAgentic** (fully automated generation + optimisation via PSO) | [Team-Building §2.4](./2026-02-24-team-building-agents.md#24-swarmagentic----fully-automated-generation-and-optimisation-emnlp-2025), [Self-Building §1.1](./2026-02-24-self-building-self-organising-agent-swarms.md#11-emergent-behaviour-in-agent-swarms) |
| **Dynamic team optimisation** (hot-swap, scaling, re-planning) | [Team-Building §2](./2026-02-24-team-building-agents.md#2-dynamic-team-optimisation) |
| **Capability gap analysis** | [Team-Building §3](./2026-02-24-team-building-agents.md#3-capability-gap-analysis) |
| **Team templates and blueprints** | [Team-Building §4](./2026-02-24-team-building-agents.md#4-team-templates-and-blueprints) |
| **End-to-end team-building lifecycle** | [Team-Building §6](./2026-02-24-team-building-agents.md#6-end-to-end-team-building-flows) |
| **Team assembly criteria** (8-factor: composition, skill coverage, roles, communication, coordination, backup, budget, timeline) | [Meta-Orchestration §3.2](./2026-02-24-prompt-based-meta-orchestration-patterns.md#32-agent-selection--capability-matching-patterns) |
| **Composition patterns** (complex problem solving, large-scale ops, workflow automation, knowledge management) | [Meta-Orchestration §2.3](./2026-02-24-prompt-based-meta-orchestration-patterns.md#23-recommended-composition-patterns-from-readme) |
| **Cortex TeamArchitectAgent design** | [Team-Building §8.4](./2026-02-24-team-building-agents.md#84-recommended-architecture) |
| **Coordination topologies for teams** | [Team-Building §7](./2026-02-24-team-building-agents.md#7-coordination-topologies-for-teams) |

### 2.6 Self-Organisation & Swarm Intelligence

How agents self-organise without centralised control.

| Topic | Reference |
|-------|-----------|
| **Emergent behaviour in LLM agent swarms** | [Self-Building §1.1](./2026-02-24-self-building-self-organising-agent-swarms.md#11-emergent-behaviour-in-agent-swarms) |
| **Stigmergy** (indirect coordination through environment modification) | [Self-Building §1.2](./2026-02-24-self-building-self-organising-agent-swarms.md#12-stigmergy----indirect-coordination-through-environment-modification) |
| **Ant Colony Optimisation for agents** (MACO-Sync, pheromone formulas) | [Self-Building §1.3](./2026-02-24-self-building-self-organising-agent-swarms.md#13-ant-colony-optimisation-applied-to-ai-agents) |
| **Self-healing networks** | [Self-Building §1.4](./2026-02-24-self-building-self-organising-agent-swarms.md#14-self-healing-networks) |
| **Autonomous team formation** | [Self-Building §1.5](./2026-02-24-self-building-self-organising-agent-swarms.md#15-autonomous-team-formation) |
| **Particle Swarm Optimisation for agents** | [Self-Building §5.1](./2026-02-24-self-building-self-organising-agent-swarms.md#51-particle-swarm-optimisation-for-agent-coordination) |
| **Bee Colony algorithms for task allocation** | [Self-Building §5.2](./2026-02-24-self-building-self-organising-agent-swarms.md#52-bee-colony-algorithms-for-task-allocation) |
| **Boids flocking for agents** (separation, alignment, cohesion) | [Self-Building §5.3](./2026-02-24-self-building-self-organising-agent-swarms.md#53-flocking-behaviour-boids-for-agent-coordination) |
| **Network topologies** (mesh, hierarchical, star, ring, DAG, small-world, scale-free) | [Self-Building §4](./2026-02-24-self-building-self-organising-agent-swarms.md#4-network-topology-patterns-for-agent-swarms) |
| **Gossip protocols for agent state** (O(log N) convergence) | [Self-Building §4.4](./2026-02-24-self-building-self-organising-agent-swarms.md#44-gossip-protocols-for-agent-state-propagation) |
| **Agentic mesh architecture** | [Self-Building §4.5](./2026-02-24-self-building-self-organising-agent-swarms.md#45-agentic-mesh-architecture) |
| **GPTSwarm** (agents as optimisable graphs) | [Cutting-Edge §4.1](./2026-02-24-cutting-edge-multi-agent-systems.md#41-gptswarm-agents-as-optimizable-graphs) |
| **Heterogeneous Swarms** (multi-LLM optimisation) | [Cutting-Edge §4.3](./2026-02-24-cutting-edge-multi-agent-systems.md#43-heterogeneous-swarms-multi-llm-optimization) |
| **RabbitMQ as digital stigmergy** (Cortex mapping) | [Self-Building §6.1](./2026-02-24-self-building-self-organising-agent-swarms.md#61-message-bus-as-digital-stigmergy) |

### 2.7 Agent-Building Agents & Self-Improvement

Agents that create, improve, or evolve other agents.

| Topic | Reference |
|-------|-----------|
| **Voyager** (agent that writes its own skills) | [Self-Building §3.6](./2026-02-24-self-building-self-organising-agent-swarms.md#36-voyager----agent-that-writes-its-own-skills) |
| **AgentVerse** (dynamic agent creation for task solving) | [Self-Building §3.4](./2026-02-24-self-building-self-organising-agent-swarms.md#34-agentverse----dynamic-agent-creation-for-task-solving), [Team-Building §1.5](./2026-02-24-team-building-agents.md#15-agentverse----dynamic-expert-recruitment) |
| **DSPy** (programmatic agent generation) | [Self-Building §3.5](./2026-02-24-self-building-self-organising-agent-swarms.md#35-dspy----programmatic-agent-generation) |
| **CAMEL** (role-playing multi-agent framework) | [Self-Building §3.3](./2026-02-24-self-building-self-organising-agent-swarms.md#33-camel----communicative-agents-for-mind-exploration), [Cutting-Edge §4.5](./2026-02-24-cutting-edge-multi-agent-systems.md#45-camel-role-playing-for-agent-coordination) |
| **AgentNet** (decentralised evolutionary coordination) | [Self-Building §3.7](./2026-02-24-self-building-self-organising-agent-swarms.md#37-agentnet----decentralised-evolutionary-agent-coordination) |
| **Sub-agent spawning pattern** (lightweight agent creation) | [Self-Building §3.2](./2026-02-24-self-building-self-organising-agent-swarms.md#32-sub-agent-spawning-pattern) |
| **STOP** (Self-Taught Optimiser for prompts) | [Cutting-Edge §2.2](./2026-02-24-cutting-edge-multi-agent-systems.md#22-stop-self-taught-optimizer-for-prompts) |
| **TextGrad** (gradient-based prompt optimisation) | [Cutting-Edge §2.3](./2026-02-24-cutting-edge-multi-agent-systems.md#23-textgrad-gradient-based-prompt-optimisation) |
| **Godel Agent** (self-referential self-improvement) | [Cutting-Edge §2.4](./2026-02-24-cutting-edge-multi-agent-systems.md#24-godel-agent-self-referential-framework) |
| **DARWIN** (evolutionary self-rewriting network) | [Cutting-Edge §2.5](./2026-02-24-cutting-edge-multi-agent-systems.md#25-darwin-evolutionary-self-improvement) |
| **SICA** (self-improving coding agent) | [Cutting-Edge §2.6](./2026-02-24-cutting-edge-multi-agent-systems.md#26-sica-self-improving-coding-agent) |
| **Self-improvement taxonomy** (5 levels: reflection → curriculum → code modification → architecture search → weight editing) | [Cutting-Edge §2.1](./2026-02-24-cutting-edge-multi-agent-systems.md#21-the-self-improvement-taxonomy) |
| **Evolutionary approaches** (EvoMAS, ADAS as evolution) | [Self-Building §2.3](./2026-02-24-self-building-self-organising-agent-swarms.md#23-evolutionary-approaches----agents-that-mutate-and-evolve) |
| **Constitutional AI for agent teams** | [Self-Building §2.5](./2026-02-24-self-building-self-organising-agent-swarms.md#25-constitutional-ai-applied-to-agent-teams) |
| **Cortex: Voyager-style skill library** | [Self-Building §6.5](./2026-02-24-self-building-self-organising-agent-swarms.md#65-voyager-style-skill-library) |
| **Cortex: evolutionary agent improvement** | [Self-Building §6.4](./2026-02-24-self-building-self-organising-agent-swarms.md#64-evolutionary-agent-improvement) |

### 2.8 Error Resilience & Recovery

How agent systems handle failures, cascades, and recovery.

| Topic | Reference |
|-------|-----------|
| **Circuit breaker patterns** (closed → open → half-open) | [Initial Patterns §1.5](./2026-02-23-agent-orchestration-patterns.md#15-error-coordination-patterns), [Meta-Orchestration §3.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#36-error-handling-patterns) |
| **Cascade prevention** (bulkhead isolation, rate limiting, backpressure, load shedding) | [Meta-Orchestration §3.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#36-error-handling-patterns) |
| **Retry strategies** (exponential backoff, jitter, retry budgets, dead letter queues) | [Meta-Orchestration §3.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#36-error-handling-patterns) |
| **Recovery orchestration** (automated recovery flows, rollback, state restoration) | [Meta-Orchestration §3.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#36-error-handling-patterns) |
| **Error taxonomy** (8 types: infrastructure, application, integration, data, timeout, permission, resource exhaustion, external) | [Initial Patterns §1.5](./2026-02-23-agent-orchestration-patterns.md#15-error-coordination-patterns), [Meta-Orchestration §3.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#36-error-handling-patterns) |
| **Chaos engineering** (proactive resilience testing) | [Meta-Orchestration §3.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#36-error-handling-patterns) |
| **Kill switches and circuit breakers** (runtime supervisor, graduated response) | [Cutting-Edge §6.3](./2026-02-24-cutting-edge-multi-agent-systems.md#63-kill-switches-and-circuit-breakers) |
| **Self-healing agent runtime** | [Self-Building §6.3](./2026-02-24-self-building-self-organising-agent-swarms.md#63-self-healing-agent-runtime) |
| **Cortex mapping:** RabbitMQ DLX + authority model | [Initial Patterns §2.1](./2026-02-23-agent-orchestration-patterns.md#21-what-cortex-already-has), [Meta-Orchestration §10.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#106-error-handling----authority-model--dead-letter-exchange) |

### 2.9 Observability & Knowledge Management

How agent systems are monitored, and how knowledge is extracted and shared.

| Topic | Reference |
|-------|-----------|
| **Metric collection architecture** (8-layer stack) | [Meta-Orchestration §9.1](./2026-02-24-prompt-based-meta-orchestration-patterns.md#91-metric-collection-architecture) |
| **Real-time monitoring** (dashboards, streaming, alerts) | [Meta-Orchestration §9.3](./2026-02-24-prompt-based-meta-orchestration-patterns.md#93-real-time-monitoring) |
| **Anomaly detection** (statistical, ML, pattern recognition) | [Meta-Orchestration §9.4](./2026-02-24-prompt-based-meta-orchestration-patterns.md#94-anomaly-detection) |
| **SLO management** (SLI definition, error budgets, burn rate) | [Meta-Orchestration §9.10](./2026-02-24-prompt-based-meta-orchestration-patterns.md#910-slo-management) |
| **Distributed tracing** (request flow, latency breakdown) | [Meta-Orchestration §9.9](./2026-02-24-prompt-based-meta-orchestration-patterns.md#99-distributed-tracing) |
| **Operational SLO targets per role** (agent-organizer >95% accuracy, error-coordinator <5min MTTR, etc.) | [Meta-Orchestration §4.1](./2026-02-24-prompt-based-meta-orchestration-patterns.md#41-target-metrics-by-agent) |
| **Knowledge extraction pipeline** (8-stage: interaction mining → outcome analysis → pattern detection → success/failure extraction → insights → collaboration patterns → innovation capture) | [Meta-Orchestration §8.1](./2026-02-24-prompt-based-meta-orchestration-patterns.md#81-knowledge-extraction-pipeline) |
| **Knowledge graph building** (entity extraction, relationship mapping, 50k+ entities) | [Meta-Orchestration §8.3](./2026-02-24-prompt-based-meta-orchestration-patterns.md#83-knowledge-graph-building) |
| **Learning distribution** (push to agents via updates, guides, alerts, tips) | [Meta-Orchestration §8.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#86-learning-distribution) |
| **Cortex mapping:** `ReferenceCode` + `ParentMessageId` as trace context | [Meta-Orchestration §10.11](./2026-02-24-prompt-based-meta-orchestration-patterns.md#1011-observability----future-monitoring-infrastructure) |

### 2.10 Safety & Alignment

How to prevent harm in multi-agent systems.

| Topic | Reference |
|-------|-----------|
| **Steganographic collusion** (agents hiding information in plain text) | [Cutting-Edge §6.1](./2026-02-24-cutting-edge-multi-agent-systems.md#61-preventing-agent-collusion) |
| **Multi-agent risk taxonomy** (6 categories) | [Cutting-Edge §6.2](./2026-02-24-cutting-edge-multi-agent-systems.md#62-multi-agent-risk-taxonomy) |
| **Kill switches and circuit breakers** | [Cutting-Edge §6.3](./2026-02-24-cutting-edge-multi-agent-systems.md#63-kill-switches-and-circuit-breakers) |
| **Sandboxing and permission models** | [Cutting-Edge §6.4](./2026-02-24-cutting-edge-multi-agent-systems.md#64-sandboxing-and-permission-models) |
| **Constitutional approaches to multi-agent alignment** | [Cutting-Edge §6.5](./2026-02-24-cutting-edge-multi-agent-systems.md#65-constitutional-approaches-to-multi-agent-alignment), [Self-Building §2.5](./2026-02-24-self-building-self-organising-agent-swarms.md#25-constitutional-ai-applied-to-agent-teams) |
| **Constitutional governance for agent teams** | [Self-Building §6.8](./2026-02-24-self-building-self-organising-agent-swarms.md#68-constitutional-governance-for-agent-teams) |
| **Cortex safety architecture** (authority tiers as safety controls) | [Cutting-Edge §7.5](./2026-02-24-cutting-edge-multi-agent-systems.md#75-safety-architecture) |

### 2.11 Agent Economies & Marketplaces

Economic models for agent coordination and task allocation.

| Topic | Reference |
|-------|-----------|
| **Agent Exchange (AEX)** (RTB-inspired agent marketplace) | [Cutting-Edge §1.1](./2026-02-24-cutting-edge-multi-agent-systems.md#11-agent-marketplaces-and-bidding-systems) |
| **AITP** (blockchain-based agent interaction protocol) | [Cutting-Edge §1.1](./2026-02-24-cutting-edge-multi-agent-systems.md#11-agent-marketplaces-and-bidding-systems) |
| **Agent-to-agent negotiation** (AgenticPay, multi-round bargaining) | [Cutting-Edge §1.3](./2026-02-24-cutting-edge-multi-agent-systems.md#13-agent-to-agent-negotiation-protocols) |
| **Free market vs planned economy** for agent coordination | [Cutting-Edge §1.4](./2026-02-24-cutting-edge-multi-agent-systems.md#14-economic-models-free-market-vs-planned-economy) |
| **Cortex economy opportunities** (skills as tradeable capabilities, authority tiers as economic constraints) | [Cutting-Edge §7.1](./2026-02-24-cutting-edge-multi-agent-systems.md#71-agent-economy-opportunities) |

### 2.12 Memory Architecture

How agents store, share, and retrieve knowledge.

| Topic | Reference |
|-------|-----------|
| **Five-layer memory taxonomy** (working, short-term, long-term, latent, shared) | [Cutting-Edge §5.1](./2026-02-24-cutting-edge-multi-agent-systems.md#51-memory-architecture-taxonomy) |
| **Blackboard systems** (shared memory coordination) | [Cutting-Edge §5.2](./2026-02-24-cutting-edge-multi-agent-systems.md#52-shared-memory-blackboard-systems), [Deep Research §4.2](./2026-02-24-agent-orchestration-deep-research.md#42-shared-blackboard--memory) |
| **Collaborative memory with dynamic access controls** | [Cutting-Edge §5.3](./2026-02-24-cutting-edge-multi-agent-systems.md#53-collaborative-memory-with-dynamic-access-controls) |
| **Memory consolidation** (Databricks "From Lakehouse to Digital Mind") | [Cutting-Edge §5.4](./2026-02-24-cutting-edge-multi-agent-systems.md#54-memory-consolidation) |
| **Knowledge architecture** (8-layer: extraction → processing → storage → analysis → synthesis → distribution → feedback → evolution) | [Meta-Orchestration §8.4](./2026-02-24-prompt-based-meta-orchestration-patterns.md#84-knowledge-architecture-layered) |
| **Cortex memory recommendations** | [Cutting-Edge §7.4](./2026-02-24-cutting-edge-multi-agent-systems.md#74-memory-architecture-recommendations) |

---

## 3. Framework Landscape

Quick-reference comparison of production frameworks studied.

| Framework | Architecture | Agent Discovery | Delegation | Communication | State Management | Best For |
|-----------|-------------|----------------|------------|---------------|-----------------|----------|
| **OpenAI Agents SDK** | Handoff-based | Tool name matching | Return Agent from function | Shared conversation history | Context variables dict | Simple routing, customer service |
| **CrewAI** | Role-based crews | YAML config + hierarchical manager | Process types (sequential, hierarchical) | Task output chaining | Crew memory (short/long/entity) | Structured team workflows |
| **AutoGen** | Team-based chat | Agent descriptions + selector | SelectorGroupChat / Swarm / Magentic-One | Topic pub/sub | Shared message history | Research, flexible orchestration |
| **LangGraph** | Graph-based | Node definitions | Send API (dynamic fan-out) | State channels | Persistent checkpoints | Complex stateful workflows |
| **Google A2A** | Protocol standard | Agent Cards (JSON) | Task lifecycle (submitted→working→done) | HTTP+SSE, JSON-RPC | Task artifacts | Cross-vendor interoperability |
| **Anthropic** | Building blocks | Tool descriptions | Orchestrator-worker via tool calls | Message passing | Conversation context | Production simplicity |
| **MetaGPT** | SOP pipeline | Role definitions | Shared message pool + `_watch()` | Publish/subscribe | SharedEnvironment + documents | Software engineering teams |
| **ChatDev** | Chat chain | Phase-role mapping | CEO → phase chain → role pairs | Inception prompting | Version-controlled documents | Software development simulation |

**Detailed framework analysis:** [Agent Swarm Frameworks](./2026-02-24-agent-swarm-frameworks.md)

---

## 4. Cortex Architecture Alignment

Consolidated mapping of research patterns to Cortex's existing architecture, drawn from all 7 documents.

### 4.1 What Cortex Already Has

| Industry Pattern | Cortex Implementation | Validation Source |
|-----------------|----------------------|-------------------|
| Agent with identity and capabilities | `IAgent`, `AgentRegistration`, `AgentCapability` | [All frameworks](./2026-02-24-agent-swarm-frameworks.md#10-relevance-to-cortex) |
| Message-driven coordination | `IMessageBus`, RabbitMQ topic exchange | [Deep Research §6.1](./2026-02-24-agent-orchestration-deep-research.md#61-validation-of-existing-architecture) |
| Agent registry with capability lookup | `IAgentRegistry.FindByCapabilityAsync` | [A2A Agent Cards](./2026-02-24-agent-orchestration-deep-research.md#21-google-a2a-agent-to-agent-protocol), [SKILL.md](./2026-02-24-agent-orchestration-deep-research.md#24-anthropic-agent-skills-specification) |
| Per-agent lifecycle wrapper | `AgentHarness` | [OpenAI runner](./2026-02-24-agent-orchestration-deep-research.md#11-openai-swarm--agents-sdk), [Strands agent-as-tool](./2026-02-24-agent-orchestration-deep-research.md#14-aws-strands-agents-sdk-10-2025) |
| Team grouping | `ITeam`, `TeamStatus`, `AgentRuntime.StartAgentAsync(agent, teamId)` | [CrewAI crews](./2026-02-24-agent-swarm-frameworks.md#2-crewai), [AutoGen teams](./2026-02-24-agent-swarm-frameworks.md#3-microsoft-autogen) |
| Task delegation with tracking | `IDelegationTracker`, `DelegationRecord`, `DelegationStatus` | [A2A task lifecycle](./2026-02-24-agent-swarm-frameworks.md#54-task-lifecycle) |
| Plan approval gating | Authority tiers (JustDoIt/DoItAndShowMe/AskMeFirst) | [All frameworks](./2026-02-23-agent-orchestration-patterns.md#22-key-insight-authority-model-is-plan-approval) |
| Human-AI parity | `IAgent` (shared interface for human and AI) | [Anthropic patterns](./2026-02-24-agent-swarm-frameworks.md#6-anthropic-agent-patterns) |
| Direct messaging | `IMessageBus.PublishAsync` to `agent.{agentId}` queue | [Initial Patterns §2.4](./2026-02-23-agent-orchestration-patterns.md#24-pattern-implementation-via-rabbitmq) |
| Dead letter handling | RabbitMQ dead letter exchange | [Meta-Orchestration §10.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#106-error-handling----authority-model--dead-letter-exchange) |
| Reference tracking / distributed tracing | `ReferenceCode` (CTX-YYYY-MMDD-NNN) + `ParentMessageId` chain | [Meta-Orchestration §10.11](./2026-02-24-prompt-based-meta-orchestration-patterns.md#1011-observability----future-monitoring-infrastructure) |
| Message priority and SLA | `MessagePriority`, `MessageEnvelope.Sla` | [Initial Patterns §2.1](./2026-02-23-agent-orchestration-patterns.md#21-what-cortex-already-has) |
| Sender identity | `MessageContext.FromAgentId` | [Initial Patterns §4.1](./2026-02-23-agent-orchestration-patterns.md#41-add-to-messagecontext-minimal-non-breaking) |

### 4.2 Key Architectural Insights

1. **Authority Model IS Plan Approval.** The research describes a plan-approval workflow (teammate submits plan → leader approves/rejects). Cortex already has this via authority tiers. `AskMeFirst` = submit plan and await approval. `DoItAndShowMe` = execute and present results. No separate approval system needed. ([Initial Patterns §2.2](./2026-02-23-agent-orchestration-patterns.md#22-key-insight-authority-model-is-plan-approval))

2. **RabbitMQ > File-Based Inboxes.** Most research patterns use file-based inboxes with polling. Cortex's RabbitMQ provides push-based delivery, atomic fanout, competing consumers, durability, backpressure, and dead letter routing -- strictly superior for every orchestration pattern. ([Initial Patterns §2.3](./2026-02-23-agent-orchestration-patterns.md#23-key-insight-rabbitmq--file-based-inboxes))

3. **RabbitMQ as Digital Stigmergy.** Cortex's message bus already functions as a stigmergic medium -- agents modify the shared environment (publish) and others react (consume). Message priority maps to pheromone strength, TTL to pheromone evaporation, dead letters to negative stigmergy. ([Self-Building §6.1](./2026-02-24-self-building-self-organising-agent-swarms.md#61-message-bus-as-digital-stigmergy))

4. **Skills as the Unit of Evolution.** Cortex's markdown skill files (wrapping C#, Python, CLI, or API) are a natural unit for Voyager-style self-improvement. Agents can generate new skill definitions, index them semantically, compose complex skills from simpler ones, and self-verify before admission. ([Self-Building §6.5](./2026-02-24-self-building-self-organising-agent-swarms.md#65-voyager-style-skill-library))

5. **Context-Centric > Role-Centric Decomposition.** Anthropic's production experience shows that splitting by context boundaries (not by job type) produces better multi-agent systems. Each subagent should handle a distinct context with minimal overlap. ([Team-Building §9](./2026-02-24-team-building-agents.md#9-key-takeaways))

---

## 5. Consolidated Gap Analysis

Gaps identified across all research documents, deduplicated and prioritised.

### 5.1 Foundation Gaps (Address Now)

| Gap | Current State | Needed | References |
|-----|--------------|--------|------------|
| **Task dependency graphs** | `DelegationRecord` is flat | `BlockedBy`/`Blocks` relationships, DAG edges, auto-unblock | [Initial Patterns §3.2](./2026-02-23-agent-orchestration-patterns.md#32-document-now-build-later-phase-2), [Deep Research §6.3](./2026-02-24-agent-orchestration-deep-research.md#63-specific-technical-recommendations) |
| **Broadcast messaging** | Point-to-point only | Team fanout exchange, `BroadcastAsync(envelope, teamId)` | [Initial Patterns §3.2](./2026-02-23-agent-orchestration-patterns.md#32-document-now-build-later-phase-2) |
| **Scatter-gather** | No collection mechanism | Correlation to collect multiple replies into single result | [Meta-Orchestration §10.2](./2026-02-24-prompt-based-meta-orchestration-patterns.md#102-orchestration-patterns----imessagebus--rabbitmq-topology) |
| **Barrier synchronisation** | No mechanism to wait for N agents | Fork-join synchronisation points | [Meta-Orchestration §10.2](./2026-02-24-prompt-based-meta-orchestration-patterns.md#102-orchestration-patterns----imessagebus--rabbitmq-topology) |

### 5.2 Agent Intelligence Gaps (High Value)

| Gap | Current State | Needed | References |
|-----|--------------|--------|------------|
| **Model tier / reasoning tier** | Not in `AgentRegistration` | `ModelTier` property for cost-aware routing | [Meta-Orchestration §10.1](./2026-02-24-prompt-based-meta-orchestration-patterns.md#101-agent-catalogue----iagent--agentregistration--agentcapability), [Meta-Orchestration §6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#6-model-tier-strategy) |
| **Agent performance tracking** | No metrics on agent success | `PerformanceScore`, `CostPerTask`, `CurrentWorkload` | [Meta-Orchestration §10.4](./2026-02-24-prompt-based-meta-orchestration-patterns.md#104-agent-selection----iagentregistryfindbyCapabilityasync) |
| **Tool/capability restrictions** | Authority model only | Per-agent tool allowlist (principle of least privilege) | [Meta-Orchestration §10.1](./2026-02-24-prompt-based-meta-orchestration-patterns.md#101-agent-catalogue----iagent--agentregistration--agentcapability) |
| **Richer agent cards** | `AgentCapability` has Name/Description/SkillIds | Input/output content types, tool declarations, environment requirements | [Deep Research §6.3](./2026-02-24-agent-orchestration-deep-research.md#63-specific-technical-recommendations) |

### 5.3 Workflow Gaps (Phase 2+)

| Gap | Current State | Needed | References |
|-----|--------------|--------|------------|
| **State machine engine** | Not implemented | Workflow state machines, transition validation | [Meta-Orchestration §10.9](./2026-02-24-prompt-based-meta-orchestration-patterns.md#109-workflow-patterns----future-workflow-engine) |
| **Saga coordination** | Not implemented | Compensation tracking, rollback procedures | [Meta-Orchestration §10.9](./2026-02-24-prompt-based-meta-orchestration-patterns.md#109-workflow-patterns----future-workflow-engine) |
| **Checkpoint/restart** | Not implemented | Persistent workflow state, resumption | [Meta-Orchestration §10.9](./2026-02-24-prompt-based-meta-orchestration-patterns.md#109-workflow-patterns----future-workflow-engine) |
| **Circuit breaker per agent** | Only DLX-level | Per-agent health tracking, automatic route-away | [Meta-Orchestration §10.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#106-error-handling----authority-model--dead-letter-exchange), [Cutting-Edge §6.3](./2026-02-24-cutting-edge-multi-agent-systems.md#63-kill-switches-and-circuit-breakers) |
| **Error taxonomy** | No structured error types | Classified error types in message model | [Meta-Orchestration §10.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#106-error-handling----authority-model--dead-letter-exchange) |
| **Priority queues** | Not implemented | RabbitMQ priority headers, harness priority awareness | [Meta-Orchestration §10.7](./2026-02-24-prompt-based-meta-orchestration-patterns.md#107-queue-management----rabbitmq-topology) |
| **Weighted/affinity routing** | Not implemented | Task router service above raw queues | [Meta-Orchestration §10.8](./2026-02-24-prompt-based-meta-orchestration-patterns.md#108-load-balancing----agentruntime--team-support) |

### 5.4 Future Vision Gaps (Phase 3+)

| Gap | Current State | Needed | References |
|-----|--------------|--------|------------|
| **Knowledge graph / knowledge service** | Not implemented | Pattern extraction from message history, agent learning loops | [Meta-Orchestration §10.10](./2026-02-24-prompt-based-meta-orchestration-patterns.md#1010-knowledge-management----skills-registry--future-knowledge-service), [Cutting-Edge §5](./2026-02-24-cutting-edge-multi-agent-systems.md#5-agent-memory-and-shared-knowledge) |
| **Agent memory system** | No persistent memory | Working, shared, institutional memory layers | [Cutting-Edge §7.4](./2026-02-24-cutting-edge-multi-agent-systems.md#74-memory-architecture-recommendations) |
| **Self-improvement loops** | Not implemented | Skill-level reflection, prompt optimisation, self-generated curricula | [Cutting-Edge §7.2](./2026-02-24-cutting-edge-multi-agent-systems.md#72-self-improvement-integration-points), [Self-Building §6.4](./2026-02-24-self-building-self-organising-agent-swarms.md#64-evolutionary-agent-improvement) |
| **Voyager-style skill generation** | Not implemented | Agents write new skill definitions, semantic indexing, composition | [Self-Building §6.5](./2026-02-24-self-building-self-organising-agent-swarms.md#65-voyager-style-skill-library) |
| **Gossip-based discovery** | Not implemented | Decentralised agent discovery for multi-instance deployments | [Self-Building §6.6](./2026-02-24-self-building-self-organising-agent-swarms.md#66-gossip-for-agent-discovery) |
| **Agent economy** | Not implemented | Task bidding, reputation scoring, economic constraints | [Cutting-Edge §7.1](./2026-02-24-cutting-edge-multi-agent-systems.md#71-agent-economy-opportunities) |
| **Consensus mechanisms** | Not implemented | Voting, quorum-based decisions | [Meta-Orchestration §10.2](./2026-02-24-prompt-based-meta-orchestration-patterns.md#102-orchestration-patterns----imessagebus--rabbitmq-topology) |

---

## 6. Implementation Roadmap

Based on the consolidated research, recommended implementation order for Cortex.

### Phase 1: Foundation (Current -- Issue #2)

Already implemented or in progress. Informed by [Initial Patterns](./2026-02-23-agent-orchestration-patterns.md#4-recommended-changes-to-issue-2-design):

- [x] `IAgent`, `IAgentRegistry`, `AgentRegistration`, `AgentCapability`
- [x] `AgentHarness` (per-agent lifecycle wrapper)
- [x] `AgentRuntime` (IHostedService, team support)
- [x] `IDelegationTracker`, `DelegationRecord`
- [x] `MessageContext.FromAgentId` (sender identity)
- [x] Per-consumer lifecycle (`IAsyncDisposable` from `StartConsumingAsync`)
- [x] Team-aware agent startup (`StartAgentAsync(agent, teamId)`)

### Phase 2: Orchestration Primitives

Enable the core orchestration patterns. Informed by [Deep Research §6.2](./2026-02-24-agent-orchestration-deep-research.md#62-patterns-to-prioritise-for-implementation):

- [ ] **Task dependency DAG** -- extend `DelegationRecord` with `BlockedBy`/`Blocks`, auto-unblock logic
- [ ] **Broadcast messaging** -- team fanout exchange, `BroadcastAsync(envelope, teamId)`
- [ ] **Scatter-gather** -- correlation mechanism to collect N replies into one result
- [ ] **Coordination messages** -- standard message types for shutdown, task completion, plan approval
- [ ] **Competing consumers** -- shared work queue for swarm pattern

### Phase 3: Team Composition

Enable "build me a team" capability. Informed by [Team-Building §8](./2026-02-24-team-building-agents.md#8-implications-for-cortex):

- [ ] **TeamArchitectAgent** -- analyses requirements, queries registry, assembles teams
- [ ] **Model tier on AgentRegistration** -- cost-aware routing (haiku/sonnet/opus equivalent)
- [ ] **Team templates** -- YAML/JSON configs for common team shapes
- [ ] **Dynamic delegation** -- runtime task assignment based on capabilities
- [ ] **Progress monitoring** -- track team execution and re-plan if stuck

### Phase 4: Resilience & Observability

Make agent systems production-grade. Informed by [Meta-Orchestration §3.6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#36-error-handling-patterns) and [§9](./2026-02-24-prompt-based-meta-orchestration-patterns.md#9-observability-patterns):

- [ ] **Circuit breaker per agent** -- health tracking, automatic route-away from failing agents
- [ ] **Error classification** -- structured error types in message model
- [ ] **Self-healing runtime** -- heartbeat monitoring, graduated recovery
- [ ] **Metrics collection** -- agent performance scoring, SLO tracking
- [ ] **Distributed tracing** -- enhanced `ReferenceCode` + `ParentMessageId` chain

### Phase 5: Self-Organisation & Intelligence

Enable adaptive, self-improving agent systems. Informed by [Self-Building](./2026-02-24-self-building-self-organising-agent-swarms.md#6-implications-for-cortex) and [Cutting-Edge](./2026-02-24-cutting-edge-multi-agent-systems.md#7-relevance-to-cortex):

- [ ] **Self-improving skills** -- reflection loops on skill execution traces
- [ ] **Voyager-style skill library** -- agents generate new skill definitions
- [ ] **Boids-inspired coordination** -- separation, alignment, cohesion rules for agent swarms
- [ ] **Constitutional governance** -- team-level constitutions defining behavioural norms
- [ ] **Knowledge graph** -- pattern extraction from message history
- [ ] **Agent economy** -- task bidding, reputation scoring

---

## 7. Key Takeaways

Distilled from all 7 research documents:

1. **Cortex's architecture is validated.** Every production framework converges on the same primitives Cortex already implements: agents with capabilities, message-driven coordination, delegation tracking, team grouping, and authority-gated approval. ([Deep Research §6.1](./2026-02-24-agent-orchestration-deep-research.md#61-validation-of-existing-architecture), [Swarm Frameworks §10](./2026-02-24-agent-swarm-frameworks.md#10-relevance-to-cortex))

2. **No framework has a true autonomous team-builder.** The closest are AgentVerse (dynamic expert recruitment), ADAS (meta-agent search), and Swarms AutoSwarmBuilder (template-free generation). This is Cortex's opportunity. ([Team-Building §9](./2026-02-24-team-building-agents.md#9-key-takeaways))

3. **The hierarchical manager pattern works best today.** CrewAI's hierarchical process and Magentic-One's Orchestrator both delegate tasks dynamically at runtime, even with fixed agent rosters. Start here. ([Team-Building §9](./2026-02-24-team-building-agents.md#9-key-takeaways))

4. **Context-centric > role-centric decomposition.** Split by context boundaries, not job types. Each subagent should handle a distinct context with minimal overlap. ([Team-Building §9](./2026-02-24-team-building-agents.md#9-key-takeaways))

5. **Multi-agent overhead is real.** 3-15x token usage, increased latency, more failure points. Only use multi-agent when context pollution, parallelisation, or specialisation justify it. ([Team-Building §9](./2026-02-24-team-building-agents.md#9-key-takeaways), [Swarm Frameworks §10.3](./2026-02-24-agent-swarm-frameworks.md#103-what-to-avoid))

6. **Memory is the differentiator.** The gap between demo agents and production agents is almost entirely about memory -- consolidation, retrieval, provenance, and permission-aware access. ([Cutting-Edge §8](./2026-02-24-cutting-edge-multi-agent-systems.md#8-key-takeaways))

7. **Self-improvement works in constrained domains.** Start with reflection loops, add self-generated exemplars, then introduce persistent skill modification gated by rigorous tests. ([Cutting-Edge §8](./2026-02-24-cutting-edge-multi-agent-systems.md#8-key-takeaways))

8. **RabbitMQ gives Cortex a structural advantage.** Every framework studied uses weaker coordination (file-based inboxes, shared dicts, simple function calls). Cortex's message bus provides push-based delivery, atomic fanout, competing consumers, durability, backpressure, and dead letter routing. ([Initial Patterns §2.3](./2026-02-23-agent-orchestration-patterns.md#23-key-insight-rabbitmq--file-based-inboxes))

9. **Model tier strategy saves money.** Use heavyweight reasoning (Opus) for orchestrators and planners, balanced (Sonnet) for domain specialists and analysts, lightweight (Haiku) for high-volume operations. ([Meta-Orchestration §6](./2026-02-24-prompt-based-meta-orchestration-patterns.md#6-model-tier-strategy))

10. **The verification subagent is the one pattern that consistently works.** A dedicated verifier with focused tools, operating in a clean context, reliably improves output quality across all frameworks. ([Team-Building §9](./2026-02-24-team-building-agents.md#9-key-takeaways))
