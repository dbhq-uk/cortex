# Self-Building and Self-Organising Agent Swarms -- Deep Research

**Date:** 2026-02-24
**Purpose:** Comprehensive survey of self-organising, self-improving, and self-building agent swarm systems. Covers emergent behaviour, stigmergy, evolutionary approaches, agent-building-agents, network topology patterns, and swarm intelligence applied to AI agents. Informs Cortex's agent runtime evolution toward adaptive, self-organising capabilities.

> **Part of the [Multi-Agent Orchestration Research Corpus](./README.md).** For team-building agent designs see [Team-Building Agents](./2026-02-24-team-building-agents.md). For recursive self-improvement research see [Cutting-Edge Systems §2](./2026-02-24-cutting-edge-multi-agent-systems.md#2-recursive-self-improvement-in-agent-systems). For framework implementations see [Swarm Frameworks](./2026-02-24-agent-swarm-frameworks.md).

---

## Table of Contents

1. [Self-Organising Multi-Agent Systems](#1-self-organising-multi-agent-systems)
2. [Self-Improving Agent Systems](#2-self-improving-agent-systems)
3. [Agent-Building Agents](#3-agent-building-agents)
4. [Network Topology Patterns for Agent Swarms](#4-network-topology-patterns-for-agent-swarms)
5. [Real-World Swarm Intelligence Applied to AI](#5-real-world-swarm-intelligence-applied-to-ai)
6. [Implications for Cortex](#6-implications-for-cortex)

---

## 1. Self-Organising Multi-Agent Systems

### 1.1 Emergent Behaviour in Agent Swarms

**Core Principle:** Global order emerges from simple, localised interactions without centralised control. Rather than imposing hierarchies, self-organising systems develop macroscopic behaviours through bottom-up processes where agents modify only their immediate interactions based on local rules.

**Key Research: Multi-Agent Systems Powered by Large Language Models (Jimenez-Romero, 2025)**
- Paper: https://arxiv.org/html/2503.03800v1 (Frontiers in AI, cited 33 times)
- Demonstrates that LLMs can guide agents toward emergent collective behaviour without explicit centralised control
- Tested with ant foraging and bird flocking simulations using NetLogo + GPT-4o
- **Architecture:** Five-component toolchain:
  1. **Environment Encoding** -- NetLogo captures real-time state (positions, pheromone concentrations, neighbour data) into structured prompts
  2. **Python Extension Integration** -- NetLogo communicates with GPT-4o via OpenAI API
  3. **LLM Processing** -- Model interprets context-rich data and proposes actions
  4. **Output Decoding** -- LLM responses formatted as JSON convert to executable commands
  5. **Iterative Execution** -- Actions modify the environment, creating closed-loop feedback

**Performance Findings:**
- LLM-only ants collected ~85 food units (matching rule-based performance)
- **Hybrid colonies (50% LLM, 50% rule-based) collected ~95 units** -- outperforming either pure approach
- Critical insight: "Deterministic if-then logic efficiently manages well-understood aspects while LLM-driven components provide adaptability in more uncertain situations"

**Key Research: Emergent Behaviours in Multi-Agent Pursuit-Evasion Games (Xu, 2025)**
- Paper: https://www.nature.com/articles/s41598-025-15057-x (Nature Scientific Reports, cited 5 times)
- Studies emergent cooperative strategies in bounded 2D grid worlds
- Pursuers develop encirclement, herding, and ambush behaviours without explicit programming

**Key Research: SwarmAgentic (Zhang et al., EMNLP 2025)**
- Paper: https://arxiv.org/abs/2506.15672 (cited 4 times)
- Code: https://github.com/yaoz720/SwarmAgenticCode
- **First framework to fully automate agentic system generation, optimisation, and collaboration from scratch**
- Uses language-driven Particle Swarm Optimisation (PSO) to evolve multi-agent architectures
- Given only a task description and objective function, it constructs complete multi-agent systems
- **Pipeline:**
  1. **Task-Conditioned Initialisation** -- Generates diverse candidate multi-agent configurations from natural-language task description
  2. **Language-Driven PSO Evolution** -- Each particle = candidate system encoded as text; velocity updates combine personal bests, global bests, and failure-driven adjustments
  3. **Execution & Evaluation** -- Candidates instantiated and executed; LLM-based failure analysis identifies agent/coordination weaknesses
  4. **Iterative Refinement & Selection** -- Loop repeats until search stabilises
- **Results:** +261.8% relative improvement over ADAS on TravelPlanner benchmark; strong cross-model transferability

### 1.2 Stigmergy -- Indirect Coordination Through Environment Modification

**Definition:** Stigmergy is a coordination mechanism where agents communicate indirectly through modifications to a shared environment. Originally observed in ant colonies through pheromone trails.

**Key Research: Emergent Collective Memory in Decentralised Multi-Agent Systems (2025)**
- Paper: https://arxiv.org/html/2512.10166
- Demonstrates how collective intelligence emerges when agents maintain individual memories while depositing persistent environmental traces
- **Memory-Trace Asymmetry:** Individual memory alone provides 68.7% better performance than memoryless agents. Environmental traces without memory infrastructure performed no better than random movement -- "traces require cognitive infrastructure for interpretation"
- **Phase Transition Theory:** Predicts critical density threshold (rho_c = 0.230) where trace-based coordination transitions from ineffective to dominant. Below this density, memory-augmented systems excel; above it, stigmergic coordination becomes superior by 36-41%
- **Agent Architecture:** Four memory categories -- food, danger, social, exploration -- with category-specific decay rates (danger: delta=0.998 persists longest; social: delta=0.95 decays fastest)
- **Practical Implications:**
  - **Sparse deployments** (rho < 0.1): Invest in individual agent intelligence; communication fails at low density
  - **Dense systems** (rho >= 0.20): Simple trace-following robots outperform memory-augmented designs through stigmergic coordination

**Key Research: Automatic Design of Stigmergy-Based Behaviours for Robot Swarms (Salman, Nature 2024)**
- Paper: https://www.nature.com/articles/s44172-024-00175-7 (cited 29 times)
- Agents self-organise through indirect local communication mediated by the environment
- Demonstrates automated design of stigmergy-based behaviours without manual tuning

**Application to Software Agents -- Collective Stigmergic Optimisation (CSO):**
- Unlike traditional multi-agent approaches relying on explicit negotiation or shared mental models, CSO systems emphasise indirect coordination
- Agents write to shared data stores (vector databases, knowledge graphs, message queues) creating digital pheromone trails
- Other agents observe state changes and adjust behaviour accordingly
- **Cortex relevance:** Messages on queues already function as a form of digital stigmergy -- agents modify a shared environment (the message bus) and other agents react to those modifications

### 1.3 Ant Colony Optimisation Applied to AI Agents

**Key Research: MACO-Sync -- Multi-Agent Ant Colony Optimisation for Synchronised Arrival (2026)**
- Paper: https://dl.acm.org/doi/10.1007/978-3-032-15621-1_33
- Code: https://github.com/SwapnilSMane/MACO-Sync
- Extends traditional ACO to enable multiple agents to coordinate their arrival times
- **Novel Synchronised Pheromone Formula:**
  ```
  delta_phi = (1/t_i) * Sum( 1/(|t_i - t_j| + epsilon) * N(t_j) / A )
  ```
  Where t_i, t_j are individual agent arrival times; epsilon=0.1 prevents division errors; N(t_j) is quantity of agents arriving at time t_j; A is total agent count
- Rewards pheromone deposits when agents arrive close together temporally
- **Results:** 23.9x better synchronisation scores vs baseline ACO; p < 0.001

**ACO for Task Allocation in Multi-Agent Systems:**
- Modification of ACO for task allocation in agricultural applications (ResearchGate, 2025)
- Pheromone trails encode task priority and agent capability matching
- **Pattern:** Each agent deposits pheromone on tasks it has successfully completed; stronger trails attract more agents to high-priority tasks; evaporation prevents stale allocations

### 1.4 Self-Healing Networks

**Key Research: Self-Healing Multi-Agent Architectures (2025)**
- Paper: https://www.researchgate.net/publication/391425148 (Financial industry focus)
- Introduces self-healing multi-agent architecture for continuous regulatory adaptation
- **Architecture Pattern:**
  1. **Monitor agents** continuously observe system health metrics
  2. **Diagnostic agents** perform root cause analysis when anomalies detected
  3. **Remediation agents** execute recovery actions (restart, reroute, rollback)
  4. **Verification agents** confirm recovery success

**Self-Healing System Design Patterns (Latitude, 2025):**
- Source: https://latitude.so/blog/designing-self-healing-systems-for-llm-platforms
- **Multi-Level Fault Detection:** Infrastructure (CPU, memory, latency), Application (response times, token generation, output quality), Business logic (semantic drift, user satisfaction)
- **Graduated Remediation:** Low-risk first (restart, clear cache) escalating to rollback/reallocate
- **Agent-Driven Self-Healing:** If an agent encounters a database connection failure, it attempts reconnection, falls back to cached data, then notifies human operator
- **Self-healing test automation** achieves 90% accuracy in correcting broken test cases; 85% reduction in test maintenance effort

**Self-Corrective Agent Architecture (EmergentMind, 2025):**
- Source: https://www.emergentmind.com/topics/self-corrective-agent-architecture
- Agents detect, diagnose, and correct failures through hierarchical monitoring
- **Pattern:** Supervisor agent monitors child agents; if child fails/times out, supervisor respawns with preserved context or reroutes to sibling

### 1.5 Autonomous Team Formation

**Key Research: Agent Exchange (2025)**
- Paper: https://arxiv.org/html/2507.03904v1
- Describes foundations for agent-centric markets supporting autonomous team formation and dynamic workflow orchestration
- Agents bid for tasks based on capabilities and workload
- Coalition structures form organically as relationship strengths reach critical thresholds

**Formation Patterns:**
- **Reputation-based routing:** ImpScore and GapScore guide task routing without central oversight
- **Nash-style bargaining:** Agents engage in negotiation within emergent coalitions
- **Role evolution:** Role profiles evolve dynamically through task-role alignment scores and specialisation negotiations
- **Meta-level adaptation:** Meta-agents execute closed-loop composition-evaluation-rectification cycles, recursively instantiating and repairing agent teams

---

## 2. Self-Improving Agent Systems

### 2.1 Self-Reflection and Performance Evaluation

**Position Paper: Truly Self-Improving Agents Require Intrinsic Metacognitive Learning (ICML 2025)**
- Paper: https://openreview.net/forum?id=4KhDd0Ozqe (Liu & van der Schaar)
- **Core argument:** Current self-improvement approaches are rigid, fail to generalise across task domains, and struggle to scale. Effective self-improvement requires intrinsic metacognitive learning.
- **Three-component framework:**
  1. **Metacognitive Knowledge** -- Self-assessment of capabilities, tasks, and learning strategies
  2. **Metacognitive Planning** -- Deciding what and how to learn
  3. **Metacognitive Evaluation** -- Reflecting on learning experiences to improve future learning
- **Key finding:** Existing self-improving agents rely predominantly on extrinsic metacognitive mechanisms (fixed, human-designed loops) that limit scalability and adaptability

**Yohei Nakajima's Self-Improvement Taxonomy (December 2025):**
- Source: https://yoheinakajima.com/better-ways-to-build-self-improving-ai-agents/
- Six core mechanisms identified:

| Mechanism | Description | Performance Impact |
|-----------|-------------|-------------------|
| **Self-Reflection** | Reflexion: write natural-language critique, store reflection, retry | GPT-4 baseline to ~91% on HumanEval |
| **Self-Correction Training** | STaR: generate solutions, filter correct ones, fine-tune on reasoning paths | Closes small/large model gap without human labels |
| **Self-Generated Curricula** | Challenger-executor pattern; one LLM generates tasks, another solves them | Doubles performance on tool-use benchmarks |
| **Self-Adapting Models** | SEAL: generate self-edit instructions as fine-tuning examples | Factual QA from 33.5% to 47% |
| **Self-Improving Code Agents** | STO: apply improver to own code recursively; discovers beam search, simulated annealing | SICA: 17-53% improvement through self-edit loops |
| **Embodied Self-Practice** | Foundation models use steps-to-go as intrinsic reward for practice | Discovers behaviours beyond demonstration data |

**Practical Implementation Roadmap:**
1. Start with reflection and self-generated exemplars (minimal overhead)
2. Add self-training on verified traces with clear correctness signals
3. Introduce persistent skill representations agents can rewrite
4. Wrap everything in rigorous constraint checks

### 2.2 Meta-Learning Agents

**MetaAgent: Self-Evolving via Tool Meta-Learning (August 2025)**
- Paper: https://arxiv.org/html/2508.00271v1
- Proposes learning-by-doing paradigm where expertise develops through iterative tool creation and refinement
- Agent creates, evaluates, and improves its own tools based on task outcomes

**Self-Evolving AI Agent Taxonomy (EmergentMind, 2026):**
- Source: https://www.emergentmind.com/topics/self-evolving-ai-agent

| System | Architecture | Key Innovation |
|--------|-------------|----------------|
| **MUSE** | Hierarchical memory (strategic, procedural, tool-level) | On-the-job policy improvement without parameter updates |
| **RoboPhD** | Evolutionary loop with cross-pollination | ELO-based tournament selection; self-fixing artefacts |
| **Darwin Godel Machine** | Tree-structured agent codebase archive | Empirical performance thresholds for code admission |
| **InfiAgent** | Pyramid-structured DAG with dynamic agents | Dual audits trigger insertion, merging, or pruning |
| **ICE** | Investigate-Consolidate-Exploit workflow | Reusable pipeline retrieval reduces inference costs |

### 2.3 Evolutionary Approaches -- Agents That Mutate and Evolve

**Key Research: EvoAgent (Yuan et al., NAACL 2025)**
- Paper: https://aclanthology.org/2025.naacl-long.315.pdf (cited 72 times)
- **Generic method to automatically extend specialised agents to multi-agent systems via evolutionary mechanisms**
- Mutates agent prompts and roles to create diverse agent populations
- Selection based on task performance fitness

**Key Research: EvoMAS -- Evolutionary Generation of Multi-Agent Systems (Hu et al., 2026)**
- Paper: https://arxiv.org/html/2602.06511v1
- **Critical advance:** Evolves entire multi-agent system configurations rather than individual agents
- **Configuration Representation:** C = (G, {A_i}, V_in, V_out) where G is a directed acyclic graph encoding agent communication; A_i = (b_i, p_i, Gamma_i) are individual agent specs (backbone model, prompt template, tool set)
- **Mutation Strategy** (one component per operation):
  - Prompt refinement (50.6% frequency on reasoning tasks) -- "Chain-of-Thought refinements yield the largest gains"
  - Model reassignment (44.6% frequency) -- for capacity-sensitive tasks
  - Tool updates -- adjusting agent capability access
  - Topology rewiring -- communication edge modifications
- **Crossover:** Inherits topology from one parent; recombines agent-level attributes from both
- **Fitness Function:**
  ```
  R(q,C) = Metrics(q,C) - beta * Cost(C)
  ```
- **Process (Pseudocode):**
  ```
  FOR each task q:
      candidates <- SELECT(q, pool)
      FOR generation i in 1..n:
          EXECUTE(candidates, q)
          IF select_mutation:
              offspring <- MUTATE(parent, feedback, memory)
          ELSE:
              offspring <- CROSSOVER(p1, p2, f1, f2, memory)
          EVALUATE(offspring, q)
          candidates.add(offspring)
      best <- argmax R(q,C)
      pool.add(best)
      summary <- CONSOLIDATE(evolution_trace)
      memory.add((q, best, summary))
  ```
- **Results:**

| Benchmark | EvoMAS | Best Baseline | Improvement |
|-----------|--------|---------------|-------------|
| BBEH | 58.7% | EvoAgent: 48.2% | **+10.5%** |
| WorkBench | 48.9% | Single Agent: 44.5% | **+4.4%** |
| SWE-Bench-Verified | 63.8% | Single Agent: 56.4% | **+7.4%** |
| SWE-Bench (Claude-4.5) | **79.1%** | Leaderboard: 78.8% | Competitive with SOTA |

- **Emergent Behaviours Discovered:**
  - System invents verifiers and decomposers not in seed pools
  - Evolution produces sparse debate graphs and task-decomposed DAGs
  - Collapses agent counts by ~70% and edge density by ~95%
  - Cross-task generalisation: evolved configurations retain up to 90% accuracy when transferred

**Key Research: The Darwin Godel Machine (Sakana AI, May 2025)**
- Paper: https://arxiv.org/abs/2505.22954 (cited 70 times)
- Code: https://github.com/jennyzzt/dgm
- Blog: https://sakana.ai/dgm/
- **A self-improving coding agent that rewrites its own code to improve performance**
- **Mechanism:**
  1. Agent reads and analyses its own Python codebase
  2. Proposes modifications (new tools, modified workflows, patch validation steps)
  3. Tests changes against benchmarks
  4. Maintains an ever-expanding archive of diverse agents (not just hill-climbing from best)
- **Critical finding:** Some lower-performing ancestors proved instrumental in discovering novel approaches that led to breakthrough descendants -- demonstrating the value of preserving stepping stones
- **Results:** SWE-bench from 20.0% to 50.0%; Polyglot from 14.2% to 30.7%
- **Transferability:** Agents optimised on Claude 3.5 Sonnet maintained improvements on o3-mini and Claude 3.7 Sonnet
- **Safety concerns documented:** Reward hacking instances including hallucinated tool execution logs and objective function manipulation

### 2.4 Multi-Agent Reinforcement Learning (MARL)

**MARL for Coordinated Drone Swarms (2025):**
- Paper: https://www.researchgate.net/publication/391391751
- Explores state representation, reward design, and decentralised policy learning for drone swarms
- **Decentralised Partially Observable Markov Decision Process (Dec-POMDP)** formulation

**Key Techniques:**
- **Centralised Training, Decentralised Execution (CTDE):** Agents train with shared information but execute independently
- **Communication learning:** Agents learn what, when, and to whom to communicate
- **Reward shaping:** Individual rewards + team rewards to balance self-interest with collective goals
- **Heterogeneous agent handling:** MAPPO-based methods for agents with different capabilities

**MOSMAC (AAMAS 2025):**
- Paper: https://ifaamas.csc.liv.ac.uk/Proceedings/aamas2025/pdfs/p867.pdf
- Multi-Objective Scalable Multi-Agent Coordination
- Advances in cooperative MARL for heterogeneous agents

### 2.5 Constitutional AI Applied to Agent Teams

**Key Research: Evolving Interpretable Constitutions for Multi-Agent Simulation (Kumar et al., February 2026)**
- Paper: https://arxiv.org/abs/2602.00755
- **Presents Constitutional Evolution:** a framework for automatically discovering behavioural norms in multi-agent LLM systems
- Uses a grid-world environment where agents must cooperate
- **Key innovation:** Constitutions are not fixed -- they evolve automatically based on agent performance
- Extends Constitutional AI from single-model alignment to multi-agent systems
- Addresses novel alignment challenges: "Multi-agent systems create novel alignment challenges" that fixed principles cannot handle

**Collective Constitutional AI (Anthropic, ACM FAccT 2024):**
- Paper: https://dl.acm.org/doi/10.1145/3630106.3658979
- Multi-stage process enabling language models to be shaped by public input
- Creates constitutions from aggregated human preferences rather than expert-designed rules

**Co-evolution of Constitutions and AI Models (EMNLP 2025):**
- Paper: https://aclanthology.org/2025.emnlp-main.869.pdf
- Constitutions and models co-evolve, each improving the other iteratively

**Self-Governing Agents: Runtime Constitutions (Crosley, 2026):**
- Source: https://blakecrosley.com/es/blog/agent-self-governance
- Four-component solution architecture: normative priors, constitutional attention, competence modulation, and value alignment verification

**Self-Regulating AI Agents: A Runtime Constitutional Framework (2026):**
- Paper: https://al-kindipublishers.org/index.php/jcsts/article/download/12089/10799/32726
- Supports dynamic cloud infrastructures and multi-agent systems with runtime constitutional enforcement

---

## 3. Agent-Building Agents

### 3.1 OpenAI Swarm -- Lightweight Agent Spawning

- Code: https://github.com/openai/swarm
- **Architecture:** Two primitive abstractions -- Agents (LLM + instructions + functions) and Handoffs (function returns Agent object)
- **Key Pattern:** Agent handoffs as the mechanism for dynamic agent creation

```python
from swarm import Swarm, Agent

def transfer_to_sales():
    return sales_agent

triage_agent = Agent(
    name="Triage",
    instructions="Route customer to appropriate department.",
    functions=[transfer_to_sales]
)

sales_agent = Agent(
    name="Sales",
    instructions="Handle sales inquiries.",
    functions=[...]
)

client = Swarm()
response = client.run(agent=triage_agent, messages=[...])
# response.agent will be sales_agent after handoff
```

- **Design philosophy:** Agents as composable primitives; no centralised orchestration; entirely client-side and stateless between calls

### 3.2 Sub-Agent Spawning Pattern

- Source: https://github.com/nibzard/awesome-agentic-patterns/blob/main/patterns/sub-agent-spawning.md
- **Problem:** Large multi-file tasks exhaust main agent's context window and reasoning budget
- **Solution:** Main agent spawns focused sub-agents, each with fresh context
- **Two approaches:**
  1. **Declarative YAML configuration** -- Predefined subagent types with specialised system prompts, allowed tools, and context windows
  2. **Dynamic spawning** -- Main agent creates task list, chunks into batches, spawns subagents for parallel execution
- **Best practices:**
  - Clear, specific task subject per subagent for traceability
  - Virtual file isolation -- subagent only sees files explicitly passed
  - Tool scoping -- subagents inherit or use limited tool subsets
  - Limit to 2-4 subagents (more creates coordination overhead)
- **Swarm Migrations:** For massive parallelisation (10+ subagents), main agent creates comprehensive todo list and map-reduces over batch of subagents. Achieves **10x+ speedup vs sequential execution**
- **Quote from Boris Cherny (Anthropic):** "The main agent makes a big to-do list for everything and map reduces over a bunch of subagents"

### 3.3 CAMEL -- Communicative Agents for Mind Exploration

- Paper: https://arxiv.org/abs/2303.17760 (cited 1,653 times)
- Code: https://github.com/camel-ai/camel
- Website: https://www.camel-ai.org/
- **Architecture:** Role-playing methodology where agents take on specific roles (e.g., "Python Programmer" and "Stock Trader") and autonomously cooperate through natural language
- **Key innovation:** Inception prompting -- uses carefully designed system prompts to assign roles and guide autonomous agent cooperation
- **Scale:** Framework designed to support systems with millions of agents
- **Self-building aspect:** CAMEL agents can define new roles, spawn new agent instances with specific expertise, and compose multi-agent workflows dynamically

### 3.4 AgentVerse -- Dynamic Agent Creation for Task Solving

- Paper: https://arxiv.org/abs/2308.10848 (ICLR 2024, cited 754 times)
- Code: https://github.com/OpenBMB/AgentVerse
- **Two frameworks:**
  1. **Task-Solving:** Assembles multiple agents to collaboratively accomplish objectives; dynamically adjusts group composition
  2. **Simulation:** Custom environments to observe emergent multi-agent behaviours
- **Dynamic adjustment:** Agent groups are restructured based on current problem-solving progress
- **Key finding:** Multi-agent groups consistently outperform single agents; emergent collaborative behaviours arise from group interaction

### 3.5 DSPy -- Programmatic Agent Generation

- Website: https://dspy.ai/
- Code: https://github.com/stanfordnlp/dspy
- **Core concept:** Treat LLM tasks as programming rather than manual prompting
- **Agent generation:** Modules compose into pipelines; optimisers automatically tune prompts, few-shot examples, and weights
- **ReAct Agent:** `dspy.ReAct` class creates tool-using agents that can be optimised programmatically
- **Self-optimisation:** DSPy's optimisers (BootstrapFewShot, MIPRO, etc.) automatically improve agent behaviour by finding optimal prompt configurations through systematic search

```python
import dspy

class RAGAgent(dspy.Module):
    def __init__(self):
        self.retrieve = dspy.Retrieve(k=5)
        self.generate = dspy.ChainOfThought("context, question -> answer")

    def forward(self, question):
        context = self.retrieve(question).passages
        return self.generate(context=context, question=question)

# Programmatic optimisation -- agent improves itself
optimizer = dspy.BootstrapFewShot(metric=my_metric)
optimised_agent = optimizer.compile(RAGAgent(), trainset=training_data)
```

### 3.6 Voyager -- Agent That Writes Its Own Skills

- Paper: https://arxiv.org/abs/2305.16291 (cited 1,793 times)
- Code: https://github.com/MineDojo/Voyager
- Website: https://voyager.minedojo.org/
- **First LLM-powered embodied lifelong learning agent** (Minecraft)
- **Three core components:**
  1. **Automatic Curriculum** -- Generates exploration tasks by analysing agent's current state; overarching goal: "discover as many diverse things as possible"
  2. **Skill Library** -- Stores executable code programs as reusable behaviours; indexed by embedding descriptions for semantic retrieval; complex skills synthesised by composing simpler programs
  3. **Iterative Prompting Mechanism** -- Three feedback types: environment feedback, execution errors, self-verification (GPT-4 as critic)
- **Self-building process:** Agent generates executable code through GPT-4, evaluates success, stores successful skills, and composes them into increasingly complex capabilities
- **Results:** 3.3x more unique discoveries, 2.3x longer traversal, 15.3x faster tech tree milestones vs baselines
- **Cortex relevance:** Direct analogue to Cortex's skill system -- agents could generate new skill definitions (markdown wrapping code) and store them in the skill registry

### 3.7 AgentNet -- Decentralised Evolutionary Agent Coordination

- Paper: https://arxiv.org/abs/2504.00587 (NeurIPS 2025, cited 129 times)
- Code: https://github.com/zoe-yyx/AgentNet
- **Architecture:** Decentralised, RAG-based framework; agents operate in a dynamically structured DAG
- **Three innovations:**
  1. **Fully decentralised** -- No central orchestrator; agents coordinate independently
  2. **Dynamic graph topology** -- Agents adjust connectivity and route tasks based on local expertise
  3. **Retrieval-based memory** -- Continuous skill refinement through RAG
- **Agent specialisation:** Develops through RAG mechanisms; agents evolve roles based on task demands rather than predetermined assignments
- **Privacy-preserving:** Minimises centralised control and data exchange; enables fault-tolerant collaboration across organisations
- **Results:** Higher task accuracy than both single-agent and centralised multi-agent baselines

---

## 4. Network Topology Patterns for Agent Swarms

### 4.1 Topology Comparison

| Topology | Description | Strengths | Weaknesses |
|----------|-------------|-----------|------------|
| **Mesh (Peer-to-Peer)** | Every agent connects directly to every other | Maximum resilience; no single point of failure | O(n^2) connections; coordination overhead |
| **Hierarchical (Tree)** | Manager agents delegate to worker agents | Clear authority; efficient task decomposition | Single points of failure; bottleneck at root |
| **Star (Hub-and-Spoke)** | Central hub coordinates all agents | Simple; easy to reason about | Hub is single point of failure; doesn't scale |
| **Ring** | Agents pass messages to neighbours | Predictable latency; simple routing | Slow propagation; vulnerable to node failure |
| **Hybrid (Hierarchical + Mesh)** | Small-world clusters with hierarchy | Best of both worlds; real-world sweet spot | Complex to implement |
| **DAG (Directed Acyclic Graph)** | Agents form directed task pipelines | Natural for workflow processing | Rigid once constructed |

### 4.2 Cooperative Agent Architecture Patterns (2025)

- Source: https://samiranama.com/posts/Designing-Cooperative-Agent-Architectures-in-2025/

**Four primary coordination patterns:**

1. **Manager-Worker (Hierarchical):** Manager breaks tasks into parallelisable units for specialised workers. "Excels in scenarios with clear task decomposition."

2. **Peer Debate (Democratic):** Agents argue from different perspectives. AutoGen's GroupChat improved code evaluation by ~9 points. "Prioritises quality over speed."

3. **Blackboard/Shared Knowledge Graph (Collaborative):** Every agent reads from and writes to shared state. "Creates emergent collective intelligence."

4. **Swarm/Market (Emergent):** Agents bid for tasks based on capabilities and workload. Market-based task allocation reduces idle time.

**Five-Layer Architecture Stack:**
1. Interface & Perception -- Multimodal input parsing
2. Memory & Knowledge -- Temporal reasoning with provenance
3. Reasoning & Planning -- Self-critique and task decomposition
4. Execution & Tooling -- Standardised tool interfaces (MCP)
5. Coordination & Oversight -- Orchestration and resource management

### 4.3 Small-World and Scale-Free Networks in Agent Systems

**Small-World Networks:**
- High clustering coefficient (agents in tight local groups) with short average path length (few hops between any two agents)
- Enables rapid information propagation while maintaining local specialisation
- Natural for agent swarms where teams form clusters but need global coordination

**Scale-Free Networks and Preferential Attachment:**
- Few hub agents handle many connections; most agents have few connections
- Follows power-law degree distribution
- **Advantage:** Extremely efficient routing through hubs
- **Vulnerability:** Targeted attack on hubs can fragment the network
- **Application:** Agent registries and discovery services naturally form scale-free topologies -- popular/capable agents attract more connections

### 4.4 Gossip Protocols for Agent State Propagation

**Key Research: Revisiting Gossip Protocols for Emergent Coordination in Agentic Systems (Habiba, 2025)**
- Paper: https://arxiv.org/html/2508.01531v1 (cited 5 times)

**Architecture:**
- Agents randomly select peers at regular intervals to exchange state information
- Creates redundant paths for data propagation
- **No central coordinator required**

**Gossip Round Structure:**
1. Agent selects **fan-out** peers (typically 3-5 neighbours)
2. Exchanges accumulated state with selected agents
3. Applies merge rules incorporating received information
4. Repeats at configurable intervals

**Convergence Properties:**
- Spread behaves like a branching process with **exponential convergence**
- O(log N) rounds for full propagation
- **A 25,000-agent system converges in 15 rounds** at 1-second intervals

**State Propagation Mechanisms:**
- **Anti-entropy exchanges:** Agents adopt highest version numbers
- **CRDTs (Conflict-Free Replicated Data Types):** For commutative operations across any order
- **Tombstone markers:** For deletion acknowledgment across replicas
- **Content-agnostic payloads:** Agents locally interpret payloads

**Enabling Agent Coordination:**
- **Failure detection:** Agents propagate heartbeats and suspicions about failed peers
- **Emergent task allocation:** Agents gossip task availability and capability metadata; peers claim relevant work
- **Load balancing:** Workload signals propagate creating emergent equilibrium

**Security Considerations:**
- Cryptographic signatures on payloads prevent forgery
- Reputation scores discount messages from unreliable peers
- Rate limiting and TTL fields bound propagation
- k-confirmations before trusting critical information

**Key Insight:** "Gossip protocols serve as a complementary layer enabling resilience, discovery, and emergence rather than replacing structured protocols like MCP and A2A"

### 4.5 Agentic Mesh Architecture

- Source: Various (McKinsey, industry analysis)
- **Concept:** Enterprise-wide networks of autonomous agents coordinating seamlessly across organisations
- **Characteristics:**
  - Each agent operates as independently deployable microservice
  - Service mesh provides observability, policy enforcement, and routing
  - API layers for integrating external data sources, tools, and user feedback
  - Containerised deployment enables rapid updates and fault isolation

### 4.6 Partition Handling and Reconnection

**Challenge:** When agent networks split (network partition), sub-groups must continue operating independently and reconcile state when reconnected.

**Patterns:**
- **CRDTs** for eventually consistent state across partitions
- **Gossip protocols** naturally handle partition recovery -- agents resume gossiping when connectivity returns
- **Split-brain detection:** Quorum-based decisions prevent conflicting actions during partition
- **Reconciliation queues:** Buffered actions replayed during reconnection

---

## 5. Real-World Swarm Intelligence Applied to AI

### 5.1 Particle Swarm Optimisation for Agent Coordination

**PSO Applied to LLM Agent Systems:**
- **SwarmAgentic (EMNLP 2025)** is the landmark application: reformulates PSO into interpretable text-symbol updates over agent roles and coordination structures
- Each "particle" is a complete multi-agent system configuration
- Velocity = combination of personal best experience + global best + failure-driven adjustments
- Position update = textual edits (adding, removing, modifying agents and coordination patterns)

**Heterogeneous Swarms (NeurIPS 2025):**
- Paper: https://neurips.cc/virtual/2025/poster/115041
- Algorithm to design multi-LLM systems by jointly optimising model roles and weights
- Treats LLM selection and weight allocation as a PSO problem

**Traditional PSO for Agent Coordination:**
- Agents treated as particles in solution space
- Each agent maintains position (current solution) and velocity (direction of improvement)
- Global best and personal best guide exploration
- **Application:** Task allocation, resource scheduling, path planning

### 5.2 Bee Colony Algorithms for Task Allocation

**Artificial Bee Colony (ABC) Algorithm Applied to AI Agents:**
- Source: https://towardsdatascience.com/agentic-ai-swarm-optimization-using-artificial-bee-colonization-abc/ (December 2025)

**Three bee roles mapped to agent roles:**

| Bee Role | Agent Equivalent | Behaviour |
|----------|-----------------|-----------|
| **Employed Bees** | Active worker agents | Exploit known good solutions; search local neighbourhood |
| **Onlooker Bees** | Evaluator/selector agents | Observe employed bee results; probabilistically select promising solutions to explore further |
| **Scout Bees** | Explorer agents | Abandon poor solutions; randomly initialise new search positions |

**Process:**
1. Employed agents exploit current task assignments
2. Evaluator agents assess quality via fitness function (nectar amount)
3. Better solutions attract more agents (waggle dance = publishing results)
4. Scout agents restart search when solutions stagnate (abandon threshold)

**Applications:**
- E-government resource allocation (Ishengoma, 2025)
- Multi-farm task assignment (Chen et al., 2025, cited 8)
- Flexible job shop scheduling with transport robots (2026)

### 5.3 Flocking Behaviour (Boids) for Agent Coordination

**Reynolds' Three Rules Applied to Software Agents:**
- Source: https://blakecrosley.com/en/blog/boids-to-agents (February 2026)

| Boids Rule | Agent Equivalent | Purpose |
|-----------|-----------------|---------|
| **Separation** | Spawn budget (limit active agents per parent) | Prevent agent pile-up on identical sub-problems |
| **Alignment** | Shared evaluation criteria | Ensure all agents use consistent quality standards |
| **Cohesion** | Consensus protocol | Converge agents toward group findings |

**Critical Insights:**
- **Separation must be processed first** and consumes available steering force before alignment or cohesion. This prevents agents from pathologically converging on incorrect consensus
- Implemented as deterministic counter capping child agents at 12 per parent -- prevents combinatorial explosion
- **Adding rules beyond the original three creates interference** -- "ten rules produce up to 45 pairwise interactions" making system behaviour unpredictable

**Evolutionary Boids as Sandbox for Agent Societies (OpenReview, 2025):**
- Paper: https://openreview.net/pdf?id=N7Kh0K33Dk
- Maps boids rules to agent societies:
  - **Separation** = Functional niche specialisation with penalties for redundancy
  - **Alignment** = Propagation of successful strategies across network
  - **Cohesion** = Convergence toward shared objectives and resource pools

**LLM-Driven Boids (Jimenez-Romero, 2025):**
- LLM-driven bird flocking using natural language prompts for separation, alignment, and cohesion
- Adding "rationale" JSON field (chain-of-thought) significantly improved heading accuracy
- Compass convention specification essential -- stating "0 degrees = north, 90 degrees = east" resolved directional confusion

### 5.4 Swarm Robotics Patterns Applied to Software Agents

**Towards Applied Swarm Robotics (Kegeleirs et al., Frontiers in Robotics and AI, 2025, cited 9):**
- Paper: https://www.frontiersin.org/journals/robotics-and-ai/articles/10.3389/frobt.2025.1607978/full
- Addresses design, deployment, and analysis of large groups of robots collaborating in a decentralised manner
- **Key patterns transferable to software agents:**

| Swarm Pattern | Description | Software Agent Application |
|--------------|-------------|---------------------------|
| **Aggregation** | Agents converge on a point/resource | Load balancing; resource pooling |
| **Dispersion** | Agents spread across environment | Coverage optimisation; parallel exploration |
| **Foraging** | Search, collect, return resources | Data gathering; web scraping swarms |
| **Formation** | Maintain spatial arrangement | Pipeline ordering; workflow structure |
| **Task allocation** | Distributed assignment of work | Dynamic job scheduling |
| **Collective transport** | Coordinated movement of objects | Multi-agent data processing pipelines |
| **Collective decision-making** | Consensus without central authority | Distributed agreement on plans |

**Swarm Intelligence Classification (Chao, ScienceDirect, 2025, cited 57):**
- Four model categories:
  1. **Self-driven particle models** (Boids primary example)
  2. **Ant colony models** (pheromone-based)
  3. **Bee colony models** (role-based foraging)
  4. **Fish schooling models** (density-dependent behaviour)

---

## 6. Implications for Cortex

### 6.1 Message Bus as Digital Stigmergy

Cortex's RabbitMQ-based message bus already functions as a stigmergic medium. Agents modify the shared environment (publish messages) and other agents react (consume messages). This is directly analogous to pheromone deposition. **Enhancements to consider:**
- Message priority as pheromone strength -- higher priority messages attract more agent attention
- Message TTL as pheromone evaporation -- stale messages naturally decay
- Dead letter exchanges as negative stigmergy -- failed messages signal danger/difficulty

### 6.2 Self-Organising Team Formation

Cortex's ITeam and IAgent interfaces could be extended to support autonomous team formation:
- Agents advertise capabilities via the message bus (gossip-like)
- A lightweight reputation system (ImpScore) tracks agent success rates per task type
- Teams form organically when complementary capabilities are needed
- Authority tiers (JustDoIt/DoItAndShowMe/AskMeFirst) provide natural escalation boundaries

### 6.3 Self-Healing Agent Runtime

The AgentRuntime (IHostedService) should incorporate self-healing patterns:
- **Health monitoring:** AgentHarness reports heartbeats; missed heartbeats trigger investigation
- **Graduated recovery:** Restart agent -> reassign tasks -> spawn replacement -> escalate to human
- **Delegation tracking** (already in Cortex via IDelegationTracker) provides the audit trail for self-healing decisions

### 6.4 Evolutionary Agent Improvement

Following the EvoMAS and Darwin Godel Machine patterns:
- Skills (markdown files wrapping code) are the unit of evolution
- Successful skill executions strengthen the skill's ranking; failures weaken it
- Agent configurations (prompt, tools, authority claims) can be versioned and evolved
- **Safety:** Authority tiers provide natural guardrails -- evolved agents still respect AskMeFirst for high-stakes actions

### 6.5 Voyager-Style Skill Library

Cortex's ISkillRegistry could adopt Voyager's pattern:
- Agents generate new skill definitions during task execution
- Skills indexed by semantic embedding for retrieval
- Complex skills composed from simpler ones
- Self-verification step before skill admission to registry

### 6.6 Gossip for Agent Discovery

For multi-instance Cortex deployments:
- Agents gossip capability metadata via lightweight protocol
- Convergence in O(log N) rounds enables rapid discovery
- CRDTs for eventual consistency of agent registry across partitions
- Complements (not replaces) the structured message bus

### 6.7 Boids-Inspired Coordination Rules

Three simple rules for Cortex agent swarms:
1. **Separation:** Limit concurrent agents working on the same reference code (spawn budget)
2. **Alignment:** Shared evaluation criteria via authority claims and quality metrics
3. **Cohesion:** Consensus protocol for team-level decisions (DoItAndShowMe as natural checkpoint)

### 6.8 Constitutional Governance for Agent Teams

Following Constitutional Evolution (Kumar, 2026):
- Team-level constitutions defining behavioural norms
- Constitutions can evolve based on team performance
- Authority tiers map naturally to constitutional principles
- Runtime constitutional enforcement through message validation

---

## Key Sources

### Papers
| Paper | Venue/Year | Citations | Key Contribution |
|-------|-----------|-----------|-----------------|
| Voyager (Wang et al.) | NeurIPS 2023 | 1,793 | First LLM agent that writes own skills |
| CAMEL (Li et al.) | NeurIPS 2023 | 1,653 | Role-playing multi-agent cooperation |
| AgentVerse (Chen et al.) | ICLR 2024 | 754 | Dynamic agent group adjustment |
| AgentNet (Yang et al.) | NeurIPS 2025 | 129 | Decentralised evolutionary coordination |
| EvoAgent (Yuan et al.) | NAACL 2025 | 72 | Evolutionary multi-agent generation |
| Darwin Godel Machine (Zhang et al.) | arXiv 2025 | 70 | Self-improving coding agent |
| Jimenez-Romero et al. | Frontiers AI 2025 | 33 | LLM-powered swarm emergent behaviour |
| Stigmergy Robot Swarms (Salman) | Nature 2024 | 29 | Automated stigmergy design |
| EvoMAS (Hu et al.) | arXiv 2026 | -- | System-level evolutionary generation |
| Constitutional Evolution (Kumar et al.) | arXiv 2026 | -- | Evolving constitutions for multi-agent |
| SwarmAgentic (Zhang et al.) | EMNLP 2025 | 4 | PSO-based automated agent system generation |
| Gossip Protocols for Agents (Habiba) | arXiv 2025 | 5 | Gossip for emergent agent coordination |
| MACO-Sync (Mane) | Springer 2026 | -- | ACO for synchronised multi-agent arrival |
| Metacognitive Learning (Liu et al.) | ICML 2025 | -- | Framework for truly self-improving agents |

### Frameworks and Code
| Framework | URL | Key Feature |
|-----------|-----|-------------|
| OpenAI Swarm | https://github.com/openai/swarm | Lightweight agent handoffs |
| CAMEL-AI | https://github.com/camel-ai/camel | Million-agent scale role-playing |
| AgentVerse | https://github.com/OpenBMB/AgentVerse | Dynamic group adjustment |
| DSPy | https://dspy.ai/ | Programmatic agent optimisation |
| Voyager | https://github.com/MineDojo/Voyager | Self-building skill library |
| AgentNet | https://github.com/zoe-yyx/AgentNet | Decentralised evolutionary DAG |
| Darwin Godel Machine | https://github.com/jennyzzt/dgm | Self-improving code agent |
| MACO-Sync | https://github.com/SwapnilSMane/MACO-Sync | ACO synchronised arrival |
| SwarmAgentic | https://github.com/yaoz720/SwarmAgenticCode | PSO agent system generation |
| CrewAI | https://github.com/crewAIInc/crewAI | Role-based agent orchestration |
| Microsoft AutoGen | https://github.com/microsoft/autogen | Multi-agent conversation framework |
