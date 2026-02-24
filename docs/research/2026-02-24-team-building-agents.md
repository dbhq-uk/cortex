# Team-Building Agents -- Deep Research

**Date:** 2026-02-24
**Purpose:** Comprehensive survey of agents that specialise in assembling, improving, and managing teams of other agents. Covers team-architect agents, dynamic team optimisation, capability gap analysis, team templates, real-world examples, and end-to-end team-building flows. Intended to inform Cortex's team composition and agent runtime design.

> **Part of the [Multi-Agent Orchestration Research Corpus](./README.md).** For self-organising team formation see [Self-Building Swarms §1.5](./2026-02-24-self-building-self-organising-agent-swarms.md#15-autonomous-team-formation). For role archetype definitions see [Meta-Orchestration Patterns §2](./2026-02-24-prompt-based-meta-orchestration-patterns.md#2-agent-catalogue). For framework details see [Swarm Frameworks](./2026-02-24-agent-swarm-frameworks.md).

## Sources

- [CrewAI Agents](https://docs.crewai.com/en/concepts/agents) -- Agent definition and configuration
- [CrewAI Crews](https://docs.crewai.com/en/concepts/crews) -- Crew composition and lifecycle
- [CrewAI Processes](https://docs.crewai.com/en/concepts/processes) -- Sequential, hierarchical, consensual
- [CrewAI Hierarchical Process](https://docs.crewai.com/en/learn/hierarchical-process) -- Manager agent delegation
- [AutoGen Teams](https://microsoft.github.io/autogen/stable//user-guide/agentchat-user-guide/tutorial/teams.html) -- Team assembly patterns
- [AutoGen SelectorGroupChat](https://microsoft.github.io/autogen/stable//user-guide/agentchat-user-guide/selector-group-chat.html) -- Model-based speaker selection
- [AutoGen Magentic-One](https://microsoft.github.io/autogen/stable//user-guide/agentchat-user-guide/magentic-one.html) -- Orchestrator-led generalist team
- [Magentic-One Paper](https://arxiv.org/abs/2411.04468) -- Technical report (Nov 2024)
- [ChatDev Paper](https://arxiv.org/html/2307.07924v5) -- ACL 2024, role-based team assembly
- [ChatDev 2.0 GitHub](https://github.com/OpenBMB/ChatDev) -- Multi-agent orchestration platform
- [MetaGPT Paper](https://arxiv.org/html/2308.00352v6) -- ICLR 2024, SOP-driven teams
- [MetaGPT GitHub](https://github.com/FoundationAgents/MetaGPT) -- Multi-agent framework
- [MetaGPT X (VentureBeat)](https://venturebeat.com/business/metagpts-coordinated-ai-teams-promise-to-accelerate-software-development) -- Production platform
- [AgentVerse Paper](https://ar5iv.labs.arxiv.org/html/2308.10848) -- ICLR 2024, dynamic group composition
- [AgentVerse (OpenReview)](https://openreview.net/forum?id=EHg5GDnyq1) -- Cited by 754
- [ADAS: Automated Design of Agentic Systems](https://arxiv.org/abs/2408.08435) -- ICLR 2025
- [ADAS Explainer (TechTalks)](https://bdtechtalks.com/2024/09/09/adas-automated-agent-design/) -- Meta Agent Search
- [ADAS GitHub](https://github.com/ShengranHu/ADAS) -- Reference implementation
- [SwarmAgentic (EMNLP 2025)](https://aclanthology.org/2025.emnlp-main.93.pdf) -- Fully automated agentic system generation
- [MAS-on-the-Fly (MASFly)](https://arxiv.org/html/2602.13671v1) -- Dynamic adaptation at test time (Feb 2026)
- [Swarms Framework AutoSwarmBuilder](https://medium.com/@kyeg/automating-the-creation-of-multi-agent-systems-with-swarms-build-your-agents-autonomously-6c30ec65aafc) -- Autonomous agent team generation
- [Anthropic: Multi-Agent Systems](https://claude.com/blog/building-multi-agent-systems-when-and-how-to-use-them) -- When and how (Jan 2026)
- [Anthropic: Multi-Agent Research System](https://www.anthropic.com/engineering/multi-agent-research-system) -- Production architecture
- [Claude Code Subagents](https://medium.com/@the.gigi/claude-code-deep-dive-subagents-in-action-703cd8745769) -- Deep dive (Feb 2026)
- [GitHub Copilot Workspace](https://github.blog/news-insights/product-news/github-copilot-workspace/) -- Task-to-code pipeline
- [Devin Performance Review](https://cognition.ai/blog/devin-annual-performance-review-2025) -- 18 months in production
- [Designing Cooperative Agent Architectures](https://samiranama.com/posts/Designing-Cooperative-Agent-Architectures-in-2025/) -- Five-layer stack (Samira Ghodratnama, Google)
- [Role Specialization and Crew-Based Architectures](https://notes.muthu.co/2026/02/role-specialization-and-crew-based-multi-agent-architectures/) -- Survey (Feb 2026)
- [Multi-Agent Teams Hold Experts Back](https://arxiv.org/html/2602.01011v3) -- Evaluation study (Feb 2026)

---

## 1. Team-Builder / Team-Architect Agents

### 1.1 CrewAI -- Crew Composition Mechanisms

CrewAI uses a **Crew** as its top-level organising abstraction: a collaborative group of **Agents** working on a set of **Tasks**, governed by a **Process**.

**How agents are selected for a crew:**

| Mechanism | How It Works |
|-----------|-------------|
| **YAML Configuration** | Agents defined in `config/agents.yaml` with `role`, `goal`, `backstory`. The `@CrewBase` class auto-collects `@agent`-decorated methods. |
| **Direct Code** | Instantiate `Agent(role=..., goal=..., tools=[...])` and pass to `Crew(agents=[...])`. |
| **Hierarchical Manager** | Set `process=Process.hierarchical` and provide `manager_llm` or `manager_agent`. The manager autonomously allocates tasks to agents based on capabilities. Tasks are **not pre-assigned**. |
| **Planning Mode** | Set `planning=True`. Before each iteration, all crew data is sent to an `AgentPlanner` that creates a plan injected into each task description. |

**Hierarchical process -- the closest to a "team-architect" agent:**

```python
crew = Crew(
    agents=[researcher, writer, editor],
    tasks=[research_task, writing_task, editing_task],
    process=Process.hierarchical,
    manager_llm="gpt-4o"  # auto-creates a manager agent
)
```

The manager agent:
1. Receives all tasks and agent descriptions
2. Plans task allocation based on agent roles and capabilities
3. Delegates tasks dynamically (not pre-assigned)
4. Validates outcomes before proceeding
5. Can re-delegate if results are unsatisfactory

**Key insight:** CrewAI's composition is predominantly **static at design time** (developer defines agents in YAML or code), but the hierarchical process introduces **dynamic delegation at runtime** through the manager agent. The manager decides *who does what*, not the developer.

### 1.2 AutoGen -- Team Assembly Patterns

AutoGen (Microsoft) provides four team assembly patterns, each embodying a different approach to composition:

| Pattern | Class | How Agents Are Selected |
|---------|-------|------------------------|
| **RoundRobinGroupChat** | `RoundRobinGroupChat` | Fixed rotation order. Developer specifies the list of agents. |
| **SelectorGroupChat** | `SelectorGroupChat` | **LLM-based speaker selection.** A model reads the conversation history and agent descriptions to pick the next speaker. |
| **Swarm** | `Swarm` | Agents transfer control via handoff functions. Routing emerges from agent logic. |
| **MagenticOneGroupChat** | `MagenticOneGroupChat` | **Orchestrator agent** with planning, delegation, progress tracking, and re-planning. |

**SelectorGroupChat -- model-based team direction:**

```python
team = SelectorGroupChat(
    [planner, coder, reviewer],
    model_client=OpenAIChatCompletionClient(model="gpt-4o"),
    selector_prompt="Select the next speaker based on the conversation."
)
```

The selector uses the `selector_prompt` plus each agent's `name` and `description` to pick who speaks next. This enables **adaptive turn-taking** based on conversation state.

**Magentic-One -- the fullest "team-architect" agent in AutoGen:**

Architecture:
- **Orchestrator** (lead agent): Creates a **Task Ledger** (plan + facts + educated guesses) and maintains a **Progress Ledger** (self-reflection on progress)
- **Outer loop**: Updates the Task Ledger and creates new plans when progress stalls
- **Inner loop**: Assigns subtasks to specialist agents, tracks completion
- Specialist agents: **WebSurfer**, **FileSurfer**, **Coder**, **ComputerTerminal**

```python
team = MagenticOneGroupChat(
    [web_surfer, file_surfer, coder, terminal],
    model_client=model_client
)
```

The Orchestrator:
1. Analyses the task and creates a plan
2. Determines which specialist is needed for each subtask
3. Delegates to the appropriate agent
4. Monitors progress via self-reflection
5. Re-plans if the team is stuck
6. Supports heterogeneous models (e.g., o1-preview for Orchestrator, GPT-4o for specialists)

### 1.3 ChatDev -- CEO Agent Assembles the Team

ChatDev simulates a virtual software company with **predefined roles** organised into a **Chat Chain** -- a sequence of phases where pairs of agents collaborate.

**Role-based team structure:**

| Role | Responsibilities |
|------|-----------------|
| **CEO** | Decides task scope, initiates the development process |
| **CTO** | Chooses technology stack, defines architecture |
| **CPO** | Defines product requirements, user experience |
| **Programmer** | Writes code based on specifications |
| **Reviewer** | Reviews code for quality and correctness |
| **Tester** | Tests the software, identifies bugs |
| **Designer** | Creates UI/UX elements |

**How the team is assembled:**

ChatDev uses a **static role assignment** defined in configuration files. The "CEO" does not dynamically recruit agents; instead, the Chat Chain prescribes which role-pairs interact at each phase:

1. **Designing Phase**: CEO + CPO define requirements; CTO + Programmer define architecture
2. **Coding Phase**: Programmer + CTO write code through multi-turn dialogue
3. **Testing Phase**: Programmer + Tester debug through collaborative chat
4. **Documenting Phase**: CEO + CPO produce documentation

**Key mechanism -- "Communicative Dehallucination":** Agents engage in multi-turn chat to iteratively refine outputs. The instructor-assistant dynamic within each phase pair ensures self-correction.

**ChatDev 2.0** evolved into a general multi-agent orchestration platform, moving beyond the fixed software company metaphor.

### 1.4 MetaGPT -- SOP-Driven Pipeline

MetaGPT's core philosophy: **"Code = SOP(Team)"** -- the output is determined by applying Standardised Operating Procedures to a team of role-specialised agents.

**Team pipeline:**

```
User Prompt
    -> ProductManager (requirements document)
        -> Architect (system design, API specs, data model)
            -> ProjectManager (task breakdown, assignments)
                -> Engineer (code implementation)
                    -> QA Engineer (test cases, bug reports)
```

**Key innovations over ChatDev:**

| Feature | MetaGPT | ChatDev |
|---------|---------|---------|
| **Artifacts** | Structured documents (PRDs, design docs, UML) passed between roles | Free-text chat between role pairs |
| **SOPs** | Each role follows defined input/output contracts | Emergent collaboration via multi-turn chat |
| **Structured communication** | Message bus with typed messages; agents subscribe to relevant message types | Direct agent-to-agent conversation |
| **Reduced hallucination** | Constrained outputs via document templates | Chat-based self-correction |

**MetaGPT X (production platform)** implements this as a group-chat interface where humans interact with multiple role-specialised agents simultaneously.

### 1.5 AgentVerse -- Dynamic Expert Recruitment

AgentVerse (ICLR 2024, cited 754 times) introduced the first framework for **dynamic group composition** -- the system itself decides which agents to include.

**Four-stage framework:**

```
1. Expert Recruitment  ->  2. Collaborative Decision-Making
        |                            |
4. Evaluation          <-  3. Action Execution
```

**Stage 1: Expert Recruitment** (the team-building stage):
- A **recruiter module** analyses the current task
- Based on task requirements, it determines optimal group composition
- It selects or generates agents with appropriate expertise
- Composition can change between iterations (dynamic adjustment)

**Key finding:** Dynamically adjusting group composition based on task demands outperforms static teams. The system recruits expert agents as needed rather than pre-defining the full team.

**Practical implications:**
- The "Expert Recruitment" stage determines the upper bounds of the group's capabilities
- Task difficulty influences optimal team size and specialisation
- Simple tasks benefit from smaller teams; complex tasks benefit from diverse specialists
- The system can add or remove agents between rounds

### 1.6 ADAS -- Meta-Agent That Designs Agent Architectures

Automated Design of Agentic Systems (ADAS, ICLR 2025, cited 300 times) represents the most radical approach: a **meta-agent that discovers and designs new agent systems**.

**Core algorithm -- Meta Agent Search:**

1. Provide the meta-agent with: target task, coding frameworks, building blocks, previous discoveries
2. The meta-agent writes code for a new agentic system (prompts, tools, pipelines)
3. Evaluate the new system on validation data
4. If it passes threshold, add to an **archive** of successful designs
5. Feed the archive back as context for the next iteration
6. The meta-agent is encouraged to search for novel ideas at each step

**Key findings:**
- ADAS-designed agents outperform state-of-the-art manually designed agents
- Discovered agents transfer across models (GPT-3.5 -> Claude Sonnet -> GPT-4)
- Discovered agents transfer across domains (math -> reading comprehension)
- The system discovers emergent design patterns (e.g., multi-step chain-of-thought with ensembling)
- Performance is bounded by the foundation model's knowledge

**Implication for Cortex:** A team-builder agent could use ADAS-like techniques to not just compose teams from existing agents but to *discover new agent configurations* optimised for specific task types.

---

## 2. Dynamic Team Optimisation

### 2.1 AgentVerse's Dynamic Adjustment

AgentVerse demonstrates the canonical pattern for runtime team adjustment:

- After each round of collaborative decision-making and action execution, the **Evaluation** stage assesses performance
- Based on evaluation results, the **Expert Recruitment** stage can modify the team
- Agents can be added (recruited), removed (dismissed), or replaced (swapped)
- This creates a feedback loop: Task -> Team -> Execute -> Evaluate -> Adjust Team -> Repeat

### 2.2 Magentic-One's Re-Planning

While Magentic-One does not add/remove agents at runtime, its Orchestrator demonstrates **dynamic strategy adjustment**:

- **Outer loop**: If the Progress Ledger shows the team is stuck for enough steps, the Orchestrator updates the Task Ledger and creates an entirely new plan
- **Inner loop**: The Orchestrator can change which agent handles the next subtask based on progress
- This is effectively **hot-swapping the assignment** (not the agent roster, but the agent-to-task mapping)

### 2.3 MASFly -- Dynamic Adaptation at Test Time (Feb 2026)

MAS-on-the-Fly (MASFly) is a novel framework enabling **dynamic adaptation at test time without extra training**:

- The system adjusts team composition, communication protocols, and agent strategies on-the-fly
- No retraining required -- adaptation happens through prompt engineering and routing changes
- Achieves adaptive system performance through runtime reconfiguration

### 2.4 SwarmAgentic -- Fully Automated Generation and Optimisation (EMNLP 2025)

SwarmAgentic is the first framework that **fully automates agentic system generation, optimisation, and collaboration**:

- Automatic generation of agent roles and team structures
- Optimisation of agent interactions through iterative refinement
- Collaborative improvement where agents collectively enhance the system

### 2.5 Swarms Framework -- AutoSwarmBuilder

The Swarms framework's AutoSwarmBuilder generates entire teams autonomously from a natural-language description:

```python
from swarms.structs.auto_swarm_builder import AutoSwarmBuilder

swarm = AutoSwarmBuilder(
    name="Crypto Accounting Team",
    description="A specialized team for crypto transaction analysis",
    max_loops=1,
    return_agents=True,
    model_name="gpt-4.1",
)

agents = swarm.run(
    task="Create an accounting team to analyze crypto transactions, "
         "there must be 5 agents with comprehensive prompts."
)
```

The AutoSwarmBuilder autonomously determines:
- **Role architecture**: Leadership, technical, compliance, quality, investigation layers
- **Personality optimisation**: Each agent's personality matches its functional role
- **Communication protocols**: Reporting structures, escalation procedures, cross-agent collaboration
- **Quality assurance**: Built-in verification, audit trails, error detection

### 2.6 Patterns for Dynamic Team Optimisation (Not Yet Production)

Based on the research, these patterns are emerging but not yet standard in production frameworks:

| Pattern | Description | Status |
|---------|-------------|--------|
| **Hot-swapping** | Replace underperforming agent with specialist mid-run | AgentVerse (research), not in CrewAI/AutoGen |
| **Dynamic scaling** | Add more agents when workload increases | Kimi K2.5 Agent Swarm (parallel execution) |
| **Team retrospectives** | Post-task evaluation leading to team restructuring | AgentVerse evaluation stage; CrewAI `@after_kickoff` hook |
| **A/B testing compositions** | Run multiple team configs in parallel, pick winner | No framework supports natively; ADAS does this for architecture |
| **Continuous improvement** | Feedback loops that improve team config over time | ADAS archive mechanism; Swarms claims this |

---

## 3. Capability Gap Analysis

### 3.1 The Expert Recruitment Pattern (AgentVerse)

AgentVerse formalises capability gap analysis as:

1. **Task requirement analysis**: Parse the task to identify needed capabilities
2. **Skill matching**: Compare requirements against available agents' declared expertise
3. **Gap identification**: Determine which capabilities are missing
4. **Agent recruitment**: Generate or select agents to fill the gaps

This is the clearest formalisation of "We need X but nobody on the team can do X."

### 3.2 CrewAI's Hierarchical Manager as Gap Detector

In CrewAI's hierarchical process, the manager agent implicitly performs gap analysis:

- It sees all agent roles and capabilities (via their `role`, `goal`, `backstory`)
- It sees all tasks and their requirements
- When delegating, it must match task requirements to agent capabilities
- If no agent matches well, the task may be delegated to the *least bad* fit

**Limitation:** CrewAI's manager cannot create new agents at runtime. The gap is identified but not automatically filled.

### 3.3 ADAS as Automatic Capability Discovery

ADAS takes gap analysis to its logical extreme:

- The meta-agent evaluates current system performance
- When performance is insufficient, it generates *new agentic system designs* in code
- These designs can include novel prompt patterns, tool combinations, and multi-step pipelines
- The archive mechanism ensures that useful capabilities are preserved and built upon

This is effectively **automatic skill decomposition**: the system discovers that a needed capability can be achieved by combining simpler components.

### 3.4 MetaGPT's Structured Gap Detection

MetaGPT's SOP approach implicitly handles gaps through its pipeline structure:

- Each role has defined **inputs** (what it needs) and **outputs** (what it produces)
- If a role cannot produce its required output, the pipeline surfaces the failure
- The ProjectManager role specifically handles task breakdown and can identify when subtasks fall outside the team's capabilities

### 3.5 Anthropic's Guidance on When to Add Agents

Anthropic identifies three signals that a single agent has outgrown its architecture:

1. **Approaching context limits**: Context pollution degrades performance
2. **Managing too many tools**: 15-20+ tools cause selection confusion
3. **Parallelisable subtasks**: Independent work that could run concurrently

These are effectively **capability gap indicators** -- signs that the current agent configuration needs restructuring.

---

## 4. Team Templates and Blueprints

### 4.1 CrewAI's YAML-Based Templates

CrewAI's configuration approach is the closest to reusable team templates:

```yaml
# config/agents.yaml
researcher:
  role: "{topic} Senior Data Researcher"
  goal: "Uncover cutting-edge developments in {topic}"
  backstory: "Seasoned researcher with a knack for finding the latest developments..."

reporting_analyst:
  role: "{topic} Reporting Analyst"
  goal: "Create detailed reports based on {topic} data analysis"
  backstory: "Meticulous analyst with a keen eye for detail..."
```

**Parameterisation:** Variables like `{topic}` are replaced at runtime via `crew.kickoff(inputs={'topic': 'AI Agents'})`.

**Template patterns in practice:**

| Template | Agents | Process |
|----------|--------|---------|
| **Research Team** | Researcher, Writer, Editor | Sequential |
| **Software Dev Team** | Architect, Developer, Tester, Reviewer | Hierarchical |
| **Content Creation** | Researcher, Writer, Editor, Publisher | Sequential |
| **Data Analysis** | Collector, Analyst, Visualiser, Reporter | Sequential |
| **Customer Support** | Triage, Technical, Billing, Escalation | Hierarchical |

### 4.2 MetaGPT's Built-In Team Templates

MetaGPT ships with a predefined software company template:

```
ProductManager -> Architect -> ProjectManager -> Engineer -> QA
```

Each role is parameterised through the SOP framework:
- Input/output contracts define what flows between roles
- Each role has configurable prompts and tool access
- The pipeline order can be modified but follows the default SOP

### 4.3 ChatDev's Phase-Chain Templates

ChatDev defines team templates as **phase chains** in configuration:

```json
{
  "chain": [
    {"phase": "DemandAnalysis", "roles": ["CEO", "CPO"]},
    {"phase": "LanguageChoose", "roles": ["CEO", "CTO"]},
    {"phase": "Coding", "roles": ["CTO", "Programmer"]},
    {"phase": "Testing", "roles": ["Programmer", "Tester"]},
    {"phase": "Documentation", "roles": ["CEO", "CPO"]}
  ]
}
```

These chains are version-controlled alongside the codebase and can be modified for different project types.

### 4.4 AutoGen's Team Types as Templates

AutoGen provides pre-built team types that serve as templates:

| Team Type | Template For |
|-----------|-------------|
| `RoundRobinGroupChat` | Equal-contribution teams |
| `SelectorGroupChat` | Adaptive discussion teams |
| `Swarm` | Routing-based workflows |
| `MagenticOneGroupChat` | Complex, open-ended tasks |
| `GraphFlow` | DAG-structured workflows |

### 4.5 Swarms AutoSwarmBuilder -- Template-Free Generation

The Swarms AutoSwarmBuilder represents a different philosophy: **generate templates on demand** rather than reusing predefined ones.

Given a natural-language description, it generates:
- Agent roles with 1000+ word system prompts
- Communication protocols
- Quality assurance mechanisms
- Team coordination patterns

This is parameterised through:
- `model_name`: Controls agent quality/cost
- `temperature`: Controls specialisation level (0.2 = conservative, 0.8 = creative)
- `max_loops`: Controls processing depth
- `description`: Natural-language team specification

### 4.6 Version Control for Team Configurations

**Current state of the art:**

| Framework | Version Control Support |
|-----------|------------------------|
| CrewAI | YAML files checked into git |
| ChatDev | JSON phase chains in git |
| MetaGPT | Python SOP definitions in git |
| AutoGen | Python team definitions in git |
| Swarms | Generated at runtime (not versioned) |

No framework provides first-class version control for team configurations (e.g., "v2 of the software team added a security reviewer"). This is done through standard code version control.

---

## 5. Real-World Examples of Team-Building AI

### 5.1 GitHub Copilot Workspace

**Architecture:** Copilot Workspace is a **task-centric** environment where Copilot-powered agents collaborate at each stage:

```
Issue/Task
  -> Specification Agent (understands the codebase, generates a spec)
    -> Planning Agent (creates a step-by-step implementation plan)
      -> Code Generation Agent (writes code changes)
        -> Validation Agent (runs tests, builds, checks)
```

**How it assembles its pipeline:**
- The pipeline is **fixed** (spec -> plan -> code -> validate)
- But each stage is powered by different Copilot agents tuned for that phase
- Everything is fully editable by the developer at every stage
- The plan shows which files need to change and why
- Designed to work from any device (including mobile)

**Key design decision:** Full developer control at every step. The system proposes, the developer disposes.

### 5.2 Devin (Cognition)

**Architecture:** Devin operates as a **single autonomous agent** with multiple capability modules, not as a traditional multi-agent system.

**How it decides which capabilities to engage:**

| Capability | When Engaged |
|-----------|-------------|
| **Codebase understanding** | Always active -- reads repo, generates docs (DeepWiki) |
| **Planning** | Activated for complex tasks; generates architecture diagrams |
| **Code writing** | Core capability for implementation tasks |
| **Testing** | Triggered after code changes; writes and runs tests |
| **Browser/API** | Activated when external data is needed |
| **Slack/Jira** | Activated for communication and ticket management |

**Strength pattern: "Junior execution at infinite scale"**
- Excels at clear, verifiable tasks (migrations, security fixes, test writing)
- 67% PR merge rate (up from 34% year prior)
- Infinitely parallelisable: "fleet of Devins" tackles hundreds of repos simultaneously
- 4x faster at problem solving, 2x more efficient in resource consumption vs. year prior

**Strength pattern: "Senior intelligence on demand"**
- Codebase documentation with architecture diagrams
- Planning assistance (draft architecture in 15 minutes)
- Dependency mapping and breaking change detection

**Key limitation:** Does not dynamically compose agent teams. It is a single agent with many capabilities, not a team-builder.

### 5.3 Claude Code -- Subagent Spawning

**Architecture:** Claude Code uses an **orchestrator-subagent** pattern where the main agent can spawn child agents for specific tasks.

**How subagents work:**
- A subagent is an **autonomous child agent** with its own context window, tools, and instructions
- The main agent decides when to delegate based on task complexity and context management needs
- Subagents inherit all tools from the main agent (but cannot spawn sub-subagents)
- Results are returned as summaries to the main agent

**When Claude Code spawns subagents:**

| Signal | Action |
|--------|--------|
| Task decomposes into independent parts | Spawn parallel subagents |
| Context window is getting polluted | Isolate subtask in subagent |
| Specialised tool use needed | Delegate to focused subagent |
| Multiple files need analysis | Fan out to subagents |

**Anthropic's three scenarios where multi-agent outperforms single-agent:**

1. **Context protection**: Subagents isolate irrelevant context (e.g., order lookup agent returns only a summary)
2. **Parallelisation**: Decompose queries into facets, research in parallel, synthesise
3. **Specialisation**: Separate agents with focused toolsets (e.g., CRM agent, Marketing agent, Messaging agent)

**Critical design principle -- Context-centric decomposition:**
- Divide work by **context boundaries**, not by problem type
- "Planning, implementation, and testing of the same feature share too much context" -- keep them in one agent
- "Investigating market trends in Asia vs. Europe can proceed in parallel" -- split them

**The Verification Subagent Pattern:**
```python
class CodingAgent:
    def implement_feature(self, requirements):
        # Main agent implements
        ...

class VerificationAgent:
    def verify_implementation(self, requirements, files_changed):
        # Separate agent verifies without needing full context
        ...

def implement_with_verification(requirements, max_attempts=3):
    for attempt in range(max_attempts):
        result = CodingAgent().implement_feature(requirements)
        verification = VerificationAgent().verify_implementation(
            requirements, result['files_changed']
        )
        if verification['passed']:
            return result
        requirements += f"\nPrevious attempt failed: {verification['issues']}"
```

### 5.4 Anthropic's Multi-Agent Research System

Anthropic's Research feature uses the **decompose-distribute-synthesise** pattern:

1. **Lead agent** analyses a query and identifies independent research facets
2. **Subagents** are spawned in parallel to investigate each facet
3. Each subagent has its own context window and search tools
4. **Lead agent** synthesises findings into a coherent response

```python
async def research_topic(query: str) -> dict:
    facets = await lead_agent.decompose_query(query)
    tasks = [research_subagent(facet) for facet in facets]
    results = await asyncio.gather(*tasks)
    return await lead_agent.synthesize(results)
```

This has shown "substantial accuracy improvements" over single-agent approaches.

---

## 6. End-to-End Team-Building Flows

### 6.1 The Full Lifecycle

Combining insights from all frameworks, the idealised end-to-end flow is:

```
1. Natural Language Request
   "Build me a system that analyses customer feedback and generates weekly reports"

2. Task Analysis (Architect Agent)
   - Parse intent: data ingestion, sentiment analysis, summarisation, report generation
   - Identify required capabilities: web scraping, NLP, data viz, document generation
   - Determine complexity: multi-stage pipeline with 4+ distinct capabilities

3. Team Specification (Team-Builder Agent)
   - Select team template: "Data Analysis Team"
   - Parameterise: topic=customer_feedback, output=weekly_report
   - Determine team size: 4 agents (collector, analyst, visualiser, reporter)
   - Select process: Sequential pipeline
   - Assign tools: web scraper, sentiment API, charting lib, doc generator

4. Agent Instantiation
   - Generate agent configurations (role, goal, backstory, tools)
   - Instantiate agents with appropriate LLMs
   - Configure communication channels between agents
   - Set up monitoring and logging

5. Execution
   - Start the crew/team with kickoff()
   - Manager or process controls execution flow
   - Agents collaborate, passing context between stages
   - Human-in-the-loop checkpoints at critical decisions

6. Verification
   - Verification subagent tests the output
   - Quality gates at each pipeline stage
   - Final output validated against requirements

7. Team Dissolution / Persistence
   - One-shot teams: dissolve after task completion
   - Persistent teams: retain for recurring tasks
   - Learnings captured in memory for future team composition
```

### 6.2 Framework Comparison for End-to-End Flows

| Stage | CrewAI | AutoGen | MetaGPT | AgentVerse | Swarms |
|-------|--------|---------|---------|------------|--------|
| Task Analysis | Planning mode (`planning=True`) | Orchestrator (Magentic-One) | ProductManager role | Recruiter module | AutoSwarmBuilder |
| Team Specification | YAML config + manager | SelectorGroupChat / MagenticOneGroupChat | SOP pipeline | Expert Recruitment stage | Auto-generated from description |
| Agent Instantiation | `@agent` decorators | `AssistantAgent()` | Role classes | Dynamic agent generation | `AutoSwarmBuilder.run()` |
| Execution | `crew.kickoff()` | `team.run_stream()` | Pipeline execution | Collaborative decision-making | `swarm.run()` |
| Verification | `@after_kickoff` hook | Built-in termination conditions | QA Engineer role | Evaluation stage | Claimed but not detailed |
| Dynamic Adjustment | Manager re-delegates | Orchestrator re-plans | None (fixed pipeline) | Expert re-recruitment | None documented |
| Team Dissolution | `kickoff()` returns | `run()` completes | Pipeline ends | Evaluation stage decides | `run()` returns |

### 6.3 Cost/Benefit Analysis of Team Composition

**From Anthropic's production experience:**

| Factor | Single Agent | Multi-Agent Team |
|--------|-------------|-----------------|
| **Token usage** | 1x | 3-10x |
| **Latency** | Baseline | Often longer (coordination overhead) |
| **Thoroughness** | Limited by context window | Much higher (parallel exploration) |
| **Accuracy** | Degrades with context pollution | Higher with clean contexts |
| **Complexity** | Low | High (more failure points) |
| **Maintenance** | One set of prompts | Multiple prompt sets |

**When multi-agent is worth it:**
- Context pollution from previous subtasks degrades quality
- Subtasks are genuinely independent and parallelisable
- Tool domains are clearly separable (15+ tools)
- Verification can be blackbox (output-only, no shared context needed)

**When single-agent is better:**
- Task fits in one context window
- Sequential steps share extensive context
- Coordination cost exceeds execution cost
- Simple tasks with clear instructions

### 6.4 The "Telephone Game" Anti-Pattern

Anthropic's most important lesson: **problem-centric decomposition** (one agent plans, another implements, another tests) causes a "telephone game" where each handoff loses fidelity. In one experiment, agents specialised by role spent more tokens on coordination than on actual work.

**Better approach -- Context-centric decomposition:**
- Group work by what context it requires, not by what type of work it is
- An agent handling a feature should also handle its tests (it already has the context)
- Only split when context can be truly isolated

---

## 7. Coordination Topologies for Teams

From the cooperative architecture research (Ghodratnama, 2025), four primary topologies have emerged:

| Topology | Best For | Example |
|----------|----------|---------|
| **Manager -> Workers** | Clear task decomposition (document processing, scraping) | MetaGPT, CrewAI hierarchical |
| **Peer Debate / Socratic** | Quality over speed (code review, risk analysis) | AutoGen GroupChat, debate patterns |
| **Blackboard / Shared KG** | Enterprise analytics, complex RAG | Knowledge graph-backed teams |
| **Swarm / Market** | Open-world, resource allocation | VillagerBench auction model |

**Benchmarking findings:**
- Graph-orchestrated teams complete **42% more complex milestones** than manager-worker setups
- Peer debate boosts BugFixEval scores by ~**9 F1 points**
- VillagerBench auction reduced agent idle time by **22%**
- But coordination overhead increases with topology complexity

---

## 8. Implications for Cortex

### 8.1 Team-Building Agent Design

Based on this research, a Cortex team-builder agent would need:

1. **Task Analysis Capability**: Parse a natural-language request into capability requirements (like AgentVerse's Expert Recruitment)
2. **Agent Registry Awareness**: Query `IAgentRegistry` for available agents and their declared skills
3. **Gap Detection**: Identify capabilities needed but not available in the registry
4. **Team Template Library**: Reusable configurations for common team shapes (sequential, hierarchical, debate)
5. **Dynamic Delegation**: Runtime task assignment based on agent capabilities (like CrewAI's hierarchical manager)
6. **Progress Monitoring**: Track team execution and re-plan if stuck (like Magentic-One's Task Ledger)

### 8.2 Mapping to Cortex Architecture

| Cortex Concept | Team-Building Pattern |
|---------------|----------------------|
| `ITeam` | Team template / crew specification |
| `IAgent` | Individual team member with declared capabilities |
| `IAgentRegistry` | Agent pool for recruitment |
| `AgentHarness` | Per-agent lifecycle wrapper |
| `AgentRuntime` | Team execution engine |
| `AuthorityTier` | Governs which decisions need human approval during team execution |
| `IMessageBus` | Communication channel between team members |
| `ReferenceCode` | Tracking token for team-building requests |

### 8.3 Authority Model Integration

The authority model maps naturally to team-building decisions:

| Decision | Authority Tier |
|----------|---------------|
| Use existing team template | **JustDoIt** -- internal, no risk |
| Compose new team from existing agents | **DoItAndShowMe** -- present plan for approval |
| Create new agent type to fill a capability gap | **AskMeFirst** -- novel, needs human judgment |
| Scale team (add more agents) | **DoItAndShowMe** -- present scaling plan |
| Dissolve team and release resources | **JustDoIt** -- cleanup |

### 8.4 Recommended Architecture

```
TeamArchitectAgent
  |-- analyses task requirements
  |-- queries IAgentRegistry for available agents
  |-- selects team template or designs custom composition
  |-- proposes team specification (agents, process, tools)
  |-- [authority check: DoItAndShowMe]
  |-- instantiates team via IAgentRuntime
  |-- monitors progress via message bus
  |-- re-plans if team is stuck
  |-- dissolves team on completion
```

### 8.5 Open Questions

1. **Static vs. dynamic team composition**: Should Cortex support runtime agent addition/removal (like AgentVerse) or only design-time composition (like CrewAI)?
2. **Template versioning**: How should team templates be versioned and evolved?
3. **Cost budgets**: Should team composition consider token/compute budgets? (Anthropic reports 3-10x token overhead)
4. **Meta-agent search**: Is it worth implementing ADAS-like capability discovery, or should new agent types be human-designed?
5. **Capability ontology**: How should agent capabilities be declared and matched? (free-text descriptions vs. structured skill taxonomy)

---

## 9. Key Takeaways

1. **No framework has a true autonomous team-builder agent.** The closest are AgentVerse (dynamic expert recruitment), ADAS (meta-agent search), and Swarms AutoSwarmBuilder (generates teams from descriptions). Most frameworks rely on developer-defined team composition.

2. **The hierarchical manager pattern is the most practical team-builder today.** CrewAI's hierarchical process and Magentic-One's Orchestrator both delegate tasks dynamically, even though the agent roster is fixed.

3. **Dynamic team composition is the frontier.** AgentVerse, SwarmAgentic, and MASFly demonstrate that adjusting team composition at runtime improves results, but this is not yet production-standard.

4. **Context-centric decomposition beats role-centric decomposition.** Anthropic's production experience shows that splitting by context boundaries (not by job type) produces better multi-agent systems.

5. **The verification subagent is the one pattern that consistently works.** A dedicated verifier with focused tools, operating in a clean context, reliably improves output quality across all frameworks.

6. **ADAS represents the future of team composition.** A meta-agent that discovers optimal agent architectures through code search is a powerful paradigm that could subsume manual team design.

7. **Team templates are just YAML/JSON/Python configs in git.** No framework provides first-class template management. This is an opportunity for Cortex.

8. **Multi-agent overhead is real.** 3-10x token usage, increased latency, more failure points. Only use multi-agent when context pollution, parallelisation, or specialisation justify it.
