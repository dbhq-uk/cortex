# Agent Swarm Frameworks & Multi-Agent Systems -- Deep Research

**Date:** 2026-02-24
**Purpose:** Comprehensive survey of production multi-agent frameworks, communication protocols, and orchestration patterns to inform Cortex's agent runtime design.

> **Part of the [Multi-Agent Orchestration Research Corpus](./README.md).** For orchestration pattern theory see [Deep Research](./2026-02-24-agent-orchestration-deep-research.md). For team composition strategies see [Team-Building Agents](./2026-02-24-team-building-agents.md). For self-organising patterns see [Self-Building Swarms](./2026-02-24-self-building-self-organising-agent-swarms.md).

## Sources

- [OpenAI Swarm](https://github.com/openai/swarm) -- Educational multi-agent framework
- [OpenAI Agents SDK](https://openai.github.io/openai-agents-python/) -- Production successor to Swarm
- [Orchestrating Agents: Routines and Handoffs](https://developers.openai.com/cookbook/examples/orchestrating_agents) -- OpenAI Cookbook
- [CrewAI](https://docs.crewai.com/en/introduction) -- Role-based agent orchestration framework
- [CrewAI Multi-Agent Tutorial](https://www.firecrawl.dev/blog/crewai-multi-agent-systems-tutorial) -- Firecrawl
- [Microsoft AutoGen](https://github.com/microsoft/autogen) -- Agentic AI programming framework
- [AutoGen AgentChat Teams](https://microsoft.github.io/autogen/stable//user-guide/agentchat-user-guide/tutorial/teams.html) -- Team patterns
- [AutoGen Swarm](https://microsoft.github.io/autogen/stable//user-guide/agentchat-user-guide/swarm.html) -- Swarm pattern
- [AutoGen SelectorGroupChat](https://microsoft.github.io/autogen/stable//user-guide/agentchat-user-guide/selector-group-chat.html)
- [LangGraph](https://www.langchain.com/langgraph) -- Agent orchestration framework
- [LangGraph Supervisor](https://github.com/langchain-ai/langgraph-supervisor-py) -- Supervisor pattern library
- [Google A2A Protocol](https://a2a-protocol.org/latest/) -- Agent-to-Agent communication standard
- [A2A Specification](https://a2a-protocol.org/latest/specification/) -- Full protocol spec
- [A2A Announcement](https://developers.googleblog.com/en/a2a-a-new-era-of-agent-interoperability/) -- Google Developers Blog
- [Anthropic: Building Effective Agents](https://www.anthropic.com/research/building-effective-agents) -- Pattern catalogue
- [Anthropic: Multi-Agent Research System](https://www.anthropic.com/engineering/multi-agent-research-system) -- Production architecture
- [Anthropic Agent Cookbook](https://github.com/anthropics/anthropic-cookbook/tree/main/patterns/agents) -- Reference implementations
- [MetaGPT](https://github.com/FoundationAgents/MetaGPT) -- Software company simulation
- [MetaGPT Multi-Agent 101](https://docs.deepwisdom.ai/main/en/guide/tutorials/multi_agent_101.html) -- Tutorial
- [ChatDev](https://github.com/OpenBMB/ChatDev) -- Multi-agent software development
- [ChatDev Paper](https://arxiv.org/abs/2307.07924) -- ACL 2024

---

## 1. OpenAI Swarm (and Agents SDK)

### 1.1 Architecture

Swarm is an educational framework exploring lightweight multi-agent orchestration. It is built on two primitive abstractions:

| Abstraction | Purpose |
|-------------|---------|
| **Agent** | An LLM with `instructions` (system prompt) and `functions` (tools) |
| **Handoff** | A function that returns another Agent, transferring control |

The `Swarm` client runs an agent loop: call the LLM, execute any tool calls, detect if a tool returned an Agent (handoff), switch context, repeat.

```python
from swarm import Swarm, Agent

client = Swarm()

# Define agents
triage_agent = Agent(
    name="Triage Agent",
    instructions="Direct customers to the appropriate department.",
    tools=[transfer_to_sales, transfer_to_support]
)

sales_agent = Agent(
    name="Sales Agent",
    instructions="Help with product purchases.",
    tools=[execute_order, transfer_back_to_triage]
)

# Handoff function -- returning an Agent triggers transfer
def transfer_to_sales():
    """Transfer to sales department."""
    return sales_agent

# Run
response = client.run(
    agent=triage_agent,
    messages=[{"role": "user", "content": "I want to buy something"}],
    context_variables={"user_id": "123"},
)
# response.agent == sales_agent (after handoff)
# response.messages == full conversation history
```

**Agent class fields:**

| Field | Type | Description |
|-------|------|-------------|
| `name` | `str` | Agent identifier |
| `model` | `str` | LLM model (default `gpt-4o`) |
| `instructions` | `str` or `Callable` | System prompt; callable receives `context_variables` |
| `functions` | `List` | Python functions as tools |
| `tool_choice` | `str` | Optional tool selection strategy |

**Context variables** are a shared `dict` passed across agents. Functions that accept a `context_variables` parameter get it injected automatically. Functions can return:
- `str` -- tool result
- `Agent` -- triggers handoff
- `Result(value, agent, context_variables)` -- handoff with updated context

### 1.2 The Routines Concept

A **routine** is a natural language instruction set (system prompt) plus the tools needed to execute it. It represents a self-contained agent behaviour. Routines are not a class -- they are a design pattern: each Agent _is_ a routine.

### 1.3 Handoff Mechanism

The core execution loop detects handoffs:

```python
def run_full_turn(agent, messages):
    current_agent = agent
    while True:
        response = client.chat.completions.create(
            model=agent.model,
            messages=[{"role": "system", "content": current_agent.instructions}] + messages,
            tools=[function_to_schema(t) for t in current_agent.tools] or None
        )
        message = response.choices[0].message
        messages.append(message)

        if not message.tool_calls:
            break

        for tool_call in message.tool_calls:
            result = execute_tool_call(tool_call, tools)
            if type(result) is Agent:
                current_agent = result
                result = f"Transferred to {current_agent.name}. Adopt persona immediately."
            messages.append({"role": "tool", "tool_call_id": tool_call.id, "content": result})

    return Response(agent=current_agent, messages=messages)
```

Key properties:
- **Stateless** -- no hidden state; full conversation history is in `messages`
- **Conversation continuity** -- full message history persists across handoffs
- **Modular** -- each agent owns specific tools and instructions
- **Scalable** -- add agents without modifying core logic

### 1.4 OpenAI Agents SDK (Production Successor)

Swarm is now superseded by the **OpenAI Agents SDK** (March 2025), which adds:

| Feature | Description |
|---------|-------------|
| **Guardrails** | Input/output validation running in parallel with agent execution |
| **Tracing** | Built-in observability for LLM calls, tool calls, handoffs |
| **Sessions** | Persistent memory layer for maintaining context across runs |
| **Provider-agnostic** | Documented paths for non-OpenAI models |

```python
from agents import Agent, Runner, handoff
from pydantic import BaseModel

class EscalationData(BaseModel):
    reason: str

async def on_handoff(ctx, input_data: EscalationData):
    print(f"Escalation: {input_data.reason}")

billing_agent = Agent(name="Billing agent", instructions="Handle billing queries.")
refund_agent = Agent(name="Refund agent", instructions="Process refunds.")

triage_agent = Agent(
    name="Triage agent",
    handoffs=[
        billing_agent,  # Simple handoff
        handoff(         # Customised handoff
            agent=refund_agent,
            on_handoff=on_handoff,
            input_type=EscalationData,
            tool_name_override="escalate_to_refunds",
            tool_description_override="Transfer to refund specialist"
        )
    ]
)

result = Runner.run_sync(triage_agent, "I need a refund for order #456")
```

### 1.5 Strengths and Weaknesses

| Strengths | Weaknesses |
|-----------|------------|
| Extremely simple mental model | Swarm is educational only, not production |
| Full conversation history preserved | No built-in persistence or state management (in Swarm) |
| Easy to test and debug | No native parallelism -- agents run sequentially |
| Functions-as-tools feel natural | No hierarchical delegation -- flat handoffs only |
| Agents SDK adds guardrails/tracing | Tied to OpenAI models by default |

---

## 2. CrewAI

### 2.1 Architecture

CrewAI is a Python framework for orchestrating role-playing autonomous AI agents. It has a **dual-model architecture**:

| Layer | Purpose |
|-------|---------|
| **Flows** | Structured, event-driven workflows managing state and control flow |
| **Crews** | Teams of autonomous agents that handle intelligent work within Flows |

Core components:

```
Flow --> triggers --> Crew --> contains --> Agents --> perform --> Tasks
                                                          |
                                                    uses --> Tools
```

### 2.2 Agent Definition

Agents are defined with role, goal, backstory, and tools:

```python
from crewai import Agent

researcher = Agent(
    role="Senior Research Analyst",
    goal="Find and synthesise the latest AI developments",
    backstory="You are an expert analyst at a leading tech think tank.",
    tools=[search_tool, scrape_tool],
    allow_delegation=True,   # Can ask other agents for help
    verbose=True,
    max_iter=25,             # Maximum task iterations
)

writer = Agent(
    role="Tech Content Writer",
    goal="Write compelling articles on AI developments",
    backstory="You are a renowned content strategist.",
    allow_delegation=False,
)
```

**Key Agent parameters:**

| Parameter | Purpose |
|-----------|---------|
| `role` | Agent expertise area |
| `goal` | Directs decision-making |
| `backstory` | Provides behavioural context |
| `allow_delegation` | Can request help from other agents |
| `tools` | Available tools |
| `max_iter` | Maximum iterations per task (default 25) |
| `verbose` | Enables detailed logging |

### 2.3 Task Definition

Tasks describe work units with expected outputs:

```python
from crewai import Task

research_task = Task(
    description="Research the latest {topic} developments in 2026.",
    expected_output="A list of 10 bullet points of the most relevant findings.",
    agent=researcher,
)

writing_task = Task(
    description="Write a blog post based on the research findings.",
    expected_output="A 500-word blog post in markdown format.",
    agent=writer,
)
```

### 2.4 Process Types

**Sequential** -- tasks execute in order, output of each feeds to the next:

```python
from crewai import Crew, Process

crew = Crew(
    agents=[researcher, writer],
    tasks=[research_task, writing_task],
    process=Process.sequential,
    verbose=True,
)

result = crew.kickoff(inputs={"topic": "AI agents"})
```

**Hierarchical** -- a manager agent is automatically created (or custom-provided) that plans, delegates, and validates:

```python
crew = Crew(
    agents=[researcher, writer, editor],
    tasks=[research_task, writing_task, editing_task],
    process=Process.hierarchical,
    # manager_agent=custom_manager,  # Optional custom manager
)
```

In hierarchical mode, tasks are _not_ pre-assigned to agents. The manager allocates them dynamically based on agent capabilities.

**Consensual** (planned) -- agents collaborate through consensus-building.

### 2.5 Delegation Mechanism

When `allow_delegation=True`, an agent can ask other agents for help using a built-in delegation tool. The delegating agent:
1. Identifies that another agent's expertise is needed
2. Formulates a sub-request
3. Receives the delegated agent's response
4. Incorporates it into their own output

### 2.6 YAML Configuration Pattern

CrewAI supports YAML-based agent and task configuration:

```yaml
# config/agents.yaml
researcher:
  role: Senior Research Analyst
  goal: Find and synthesise the latest {topic} developments
  backstory: You are an expert analyst at a leading tech think tank.
  allow_delegation: true

# config/tasks.yaml
research_task:
  description: Research the latest {topic} developments in 2026.
  expected_output: A list of 10 bullet points of the most relevant findings.
```

```python
@CrewBase
class ResearchCrew:
    agents_config = "config/agents.yaml"
    tasks_config = "config/tasks.yaml"

    @agent
    def researcher(self) -> Agent:
        return Agent(config=self.agents_config["researcher"], tools=[search_tool])

    @crew
    def crew(self) -> Crew:
        return Crew(agents=self.agents, tasks=self.tasks, process=Process.sequential)
```

### 2.7 Strengths and Weaknesses

| Strengths | Weaknesses |
|-----------|------------|
| Intuitive role-based agent design | Python only |
| Built-in delegation between agents | Hierarchical process can be unpredictable |
| Sequential and hierarchical processes | Limited control over inter-agent communication |
| YAML config for reproducibility | Manager agent in hierarchical mode adds latency |
| Active development and community | "80% of effort should focus on task design" -- easy to misconfigure |
| Production-ready with Flows layer | No native message queue support |

---

## 3. Microsoft AutoGen

### 3.1 Architecture

AutoGen v0.4 (January 2025) is a complete redesign with a layered architecture:

| Layer | Purpose |
|-------|---------|
| **Core** | Event-driven agent runtime, topic-based pub/sub messaging |
| **AgentChat** | High-level API with pre-built agents and team patterns |
| **Extensions** | Model clients, tool integrations, storage |

The Core layer uses an event-driven agent runtime where agents communicate through messages published to topics. Agents subscribe to topics and react to messages.

### 3.2 Agent Types

```python
from autogen_agentchat.agents import AssistantAgent

# Basic assistant
agent = AssistantAgent(
    "research_agent",
    model_client=model_client,
    system_message="You are a research assistant.",
    tools=[search_tool],
    handoffs=["writer_agent"],         # For Swarm pattern
    description="Researches topics.",   # For SelectorGroupChat
)
```

Other agent types: `UserProxyAgent` (human-in-the-loop), `CodeExecutorAgent`, `SocietyOfMindAgent` (nested teams).

### 3.3 Team Patterns

AutoGen provides four pre-built team orchestration patterns:

#### RoundRobinGroupChat

Agents take turns in a fixed round-robin order:

```python
from autogen_agentchat.agents import AssistantAgent
from autogen_agentchat.conditions import TextMentionTermination
from autogen_agentchat.teams import RoundRobinGroupChat

primary = AssistantAgent("primary", model_client=model_client,
    system_message="You are a helpful assistant.")
critic = AssistantAgent("critic", model_client=model_client,
    system_message="Provide feedback. Say 'APPROVE' when satisfied.")

termination = TextMentionTermination("APPROVE")
team = RoundRobinGroupChat([primary, critic], termination_condition=termination)

result = await team.run(task="Write a short poem about AI.")
```

#### SelectorGroupChat

An LLM selects the next speaker based on conversation context:

```python
from autogen_agentchat.teams import SelectorGroupChat

planning_agent = AssistantAgent("PlanningAgent",
    description="Plans tasks. Should engage first for new tasks.",
    model_client=model_client,
    system_message="Break down complex tasks into subtasks.")

web_search_agent = AssistantAgent("WebSearchAgent",
    description="Searches the web for information.",
    tools=[search_tool], model_client=model_client)

data_analyst = AssistantAgent("DataAnalystAgent",
    description="Performs calculations and data analysis.",
    tools=[calc_tool], model_client=model_client)

# Custom selector function: always return to planner after specialist
def selector_func(messages):
    if messages[-1].source != planning_agent.name:
        return planning_agent.name
    return None  # Fall back to LLM selection

team = SelectorGroupChat(
    [planning_agent, web_search_agent, data_analyst],
    model_client=model_client,
    termination_condition=termination,
    selector_prompt="Select the best agent for the next step. {roles} {history}",
    selector_func=selector_func,
    allow_repeated_speaker=True,
)

await Console(team.run_stream(task="Research TSLA stock performance"))
```

#### Swarm

Agents hand off to each other using `HandoffMessage`:

```python
from autogen_agentchat.teams import Swarm
from autogen_agentchat.conditions import HandoffTermination, TextMentionTermination
from autogen_agentchat.messages import HandoffMessage

travel_agent = AssistantAgent(
    "travel_agent", model_client=model_client,
    handoffs=["flights_refunder", "user"],
    system_message="You are a travel agent. Hand off to flights_refunder for refunds."
)

flights_refunder = AssistantAgent(
    "flights_refunder", model_client=model_client,
    handoffs=["travel_agent", "user"],
    tools=[refund_flight],
    system_message="You handle flight refunds."
)

termination = HandoffTermination(target="user") | TextMentionTermination("TERMINATE")
team = Swarm([travel_agent, flights_refunder], termination_condition=termination)

# Run with user-in-the-loop
task_result = await Console(team.run_stream(task="I need to refund my flight."))
last = task_result.messages[-1]

while isinstance(last, HandoffMessage) and last.target == "user":
    user_input = input("User: ")
    task_result = await Console(
        team.run_stream(task=HandoffMessage(source="user", target=last.source, content=user_input))
    )
    last = task_result.messages[-1]
```

#### MagenticOneGroupChat

A generalist multi-agent system for open-ended web and file-based tasks (Magentic-One).

### 3.4 Core Layer: Topic-Based Messaging

At the Core layer, agents communicate via pub/sub topics:

```
GroupChatMessage --> [Shared Topic] --> All Agents
                                    --> GroupChatManager
                                            |
                                    RequestToSpeak --> [Agent Topic] --> Selected Agent
```

The GroupChatManager:
1. Maintains participant info and chat history
2. Selects the next speaker using an LLM selector
3. Sends `RequestToSpeak` to the chosen agent
4. The agent publishes a `GroupChatMessage` back to the shared topic
5. Continues until termination condition is met

### 3.5 Termination Conditions

```python
from autogen_agentchat.conditions import (
    TextMentionTermination,    # Keyword in message
    MaxMessageTermination,     # Message count limit
    HandoffTermination,        # Handoff to specific target
    ExternalTermination,       # External signal
    TimeoutTermination,        # Time limit
)

# Compose with | (OR) and & (AND)
condition = TextMentionTermination("DONE") | MaxMessageTermination(20)
```

### 3.6 Strengths and Weaknesses

| Strengths | Weaknesses |
|-----------|------------|
| Rich set of pre-built team patterns | Complex layered architecture -- steep learning curve |
| Event-driven Core with topic-based pub/sub | v0.4 is a breaking redesign from v0.2 |
| SelectorGroupChat provides intelligent routing | Recently migrated to Microsoft Agent Framework branding |
| Swarm pattern with typed HandoffMessage | SelectorGroupChat requires an extra LLM call per turn |
| Built-in streaming, state serialisation, memory | Heavy abstraction layers can obscure debugging |
| Human-in-the-loop via UserProxyAgent | Documentation spread across v0.2/v0.4/stable |

---

## 4. LangGraph

### 4.1 Architecture

LangGraph models agent workflows as **directed graphs** with **state machines**. Agents are nodes, communication paths are edges, and shared state flows through the graph.

| Concept | Description |
|---------|-------------|
| **StateGraph** | Directed graph where nodes operate on a shared state object |
| **Node** | A function or agent that reads/writes state |
| **Edge** | Connection between nodes (static or conditional) |
| **State** | A shared data structure (typically `TypedDict` or `Pydantic` model) |
| **Conditional Edge** | Routes to different nodes based on state evaluation |

### 4.2 Basic Graph Construction

```python
from langgraph.graph import StateGraph, START, END
from langgraph.prebuilt import ToolNode
from typing import TypedDict, Annotated
from operator import add

class AgentState(TypedDict):
    messages: Annotated[list, add]

def call_model(state: AgentState):
    response = model.invoke(state["messages"])
    return {"messages": [response]}

def should_continue(state: AgentState):
    last_msg = state["messages"][-1]
    if last_msg.tool_calls:
        return "tools"
    return END

# Build the graph
builder = StateGraph(AgentState)
builder.add_node("agent", call_model)
builder.add_node("tools", ToolNode(tools))
builder.add_edge(START, "agent")
builder.add_conditional_edges("agent", should_continue, {"tools": "tools", END: END})
builder.add_edge("tools", "agent")

graph = builder.compile()
```

This creates the classic ReAct loop: agent reasons -> optionally calls tools -> reasons about results -> repeats until done.

### 4.3 Supervisor Pattern

The `langgraph-supervisor` library provides a high-level API:

```python
from langgraph_supervisor import create_supervisor
from langgraph.prebuilt import create_react_agent

# Create specialist agents
math_agent = create_react_agent(
    model=model,
    tools=[add, multiply],
    name="math_expert",
    prompt="You are a math expert. Use one tool at a time."
)

research_agent = create_react_agent(
    model=model,
    tools=[web_search, wiki_search],
    name="research_expert",
    prompt="You are a research expert."
)

# Create supervisor that orchestrates the specialists
supervisor = create_supervisor(
    agents=[math_agent, research_agent],
    model=model,
    prompt="Route tasks to the appropriate specialist."
).compile()

result = supervisor.invoke({"messages": [{"role": "user", "content": "What is 15% of GDP of France?"}]})
```

The supervisor receives all messages, decides which agent to invoke via tool-based handoff, and each agent returns results to the supervisor.

### 4.4 Hierarchical Multi-Agent Teams

LangGraph supports nested supervisors for complex hierarchies:

```
Top Supervisor
├── Research Supervisor
│   ├── Web Searcher
│   └── Paper Analyser
└── Writing Supervisor
    ├── Drafter
    └── Editor
```

Each supervisor is itself a compiled `StateGraph` that can be used as a node in a parent graph.

### 4.5 Parallel Execution Patterns

| Pattern | Description |
|---------|-------------|
| **Scatter-Gather** | Distribute tasks to multiple agents, consolidate results downstream |
| **Pipeline Parallelism** | Different agents handle sequential stages concurrently |
| **Map-Reduce** | Fan out identical tasks, aggregate results |

### 4.6 Key Features

- **Checkpointing** -- save and resume graph state
- **Human-in-the-loop** -- interrupt graph, get human input, resume
- **Streaming** -- stream intermediate results from nodes
- **Memory** -- short-term (checkpointer) and long-term (store) persistence
- **Time travel** -- replay from any checkpoint

### 4.7 Strengths and Weaknesses

| Strengths | Weaknesses |
|-----------|------------|
| Explicit, visual graph structure | Verbose graph construction for simple flows |
| Conditional routing via state | Requires learning graph abstractions |
| Built-in checkpointing and persistence | Python and JS only |
| Supervisor pattern with tool-based delegation | State management can become complex |
| Hierarchical nesting of agent teams | Tight coupling to LangChain ecosystem |
| Production-grade with LangSmith integration | Overkill for simple agent chains |

---

## 5. Google A2A Protocol

### 5.1 Overview

The Agent2Agent (A2A) protocol is an **open standard** for agent-to-agent communication, announced by Google in April 2025 with 50+ technology partners. It is **not a framework** but a **wire protocol** -- it defines how agents discover, communicate with, and delegate to each other regardless of their internal implementation.

**Key distinction:** A2A handles agent-to-agent communication, while Anthropic's MCP handles agent-to-tool communication. They are complementary.

### 5.2 Architecture

A2A operates across three layers:

| Layer | Purpose |
|-------|---------|
| **Data Model** | Canonical structures: Task, Message, AgentCard, Part, Artifact |
| **Operations** | Abstract capabilities (SendMessage, GetTask, CancelTask, etc.) |
| **Protocol Bindings** | JSON-RPC 2.0, gRPC, HTTP/REST mappings |

Communication is between **Client Agents** (formulate tasks) and **Remote Agents** (execute tasks).

### 5.3 Agent Cards

Agents advertise capabilities via an **Agent Card** -- a JSON document at `/.well-known/agent-card.json`:

```python
from a2a.types import AgentCard, AgentSkill, AgentCapabilities

skill = AgentSkill(
    id="currency_conversion",
    name="Currency Converter",
    description="Converts between currencies using live rates.",
    tags=["finance", "currency"],
    examples=["Convert 100 USD to EUR"],
)

agent_card = AgentCard(
    name="Currency Agent",
    description="Handles currency conversion queries.",
    url="https://agent.example.com/",
    version="1.0.0",
    default_input_modes=["text"],
    default_output_modes=["text"],
    capabilities=AgentCapabilities(streaming=True),
    skills=[skill],
    supports_authenticated_extended_card=True,
)
```

Agent Card attributes:
- `name`, `description`, `version` -- identity
- `url` -- service endpoint
- `capabilities` -- streaming, push notifications
- `skills` -- list of `AgentSkill` objects
- `defaultInputModes` / `defaultOutputModes` -- MIME types
- `authentication` -- auth requirements (OAuth2, OpenID Connect, mTLS)

### 5.4 Task Lifecycle

The fundamental unit of work is a **Task**, identified by a server-generated unique ID:

```
submitted --> working --> completed
                |-----> failed
                |-----> canceled
                |-----> rejected
                |-----> input-required --> (user provides input) --> working
                |-----> auth-required --> (client authenticates) --> working
```

**Messages** contain **Parts** (smallest content units: text, files, structured data) and have a `role` of either `"user"` or `"agent"`.

**Artifacts** are the final outputs of completed tasks.

### 5.5 Communication Patterns

| Pattern | Mechanism |
|---------|-----------|
| **Request-Response** | SendMessage, wait for completion |
| **Polling** | Client-initiated periodic GetTask calls |
| **Streaming** | Persistent connection (SSE/gRPC streams) for real-time updates |
| **Push Notifications** | Server-initiated HTTP POST to registered webhooks |

### 5.6 Core Operations

11 operations defined in the spec:

| Operation | Purpose |
|-----------|---------|
| `SendMessage` | Primary interaction -- initiates or continues a task |
| `SendStreamingMessage` | Real-time streaming updates |
| `GetTask` | Retrieve task state |
| `ListTasks` | Query tasks with filtering |
| `CancelTask` | Request task termination |
| `SubscribeToTask` | Stream updates for existing tasks |
| `CreatePushNotification` | Register webhook for async updates |
| `GetPushNotification` | Retrieve push config |
| `ListPushNotifications` | List push configs |
| `DeletePushNotification` | Remove push config |
| `GetExtendedAgentCard` | Authenticated capability discovery |

### 5.7 Python SDK Example

```python
from a2a.server.agent_execution import AgentExecutor

class CurrencyAgentExecutor(AgentExecutor):
    async def execute(self, context, event):
        # Process incoming message
        query = event.message.parts[0].text
        result = await convert_currency(query)

        # Return response
        await context.send_message(
            parts=[TextPart(text=result)]
        )
        await context.complete()

    async def cancel(self, context, event):
        await context.fail("Task cancelled by user.")
```

### 5.8 Strengths and Weaknesses

| Strengths | Weaknesses |
|-----------|------------|
| Vendor and framework agnostic | Protocol overhead for co-located agents |
| Built on existing standards (HTTP, JSON-RPC, gRPC) | Still in Release Candidate (v1.0 RC) |
| Enterprise security model | Complexity of full spec for simple use cases |
| Long-running task support with lifecycle states | Limited adoption beyond Google ecosystem so far |
| Agent discovery via Agent Cards | Requires network infrastructure |
| Complements MCP for tool access | No reference implementation for all bindings |
| Multi-modal (text, audio, video) | |

---

## 6. Anthropic Agent Patterns

### 6.1 Building Blocks Philosophy

Anthropic's approach emphasises **simplicity first**: start with simple prompts, add complexity only when it demonstrably improves outcomes. They identify five composable **workflow patterns** and one **agent pattern**:

### 6.2 Workflow Patterns

#### Prompt Chaining
Sequential LLM calls where each processes the previous output:

```
Input --> LLM Call 1 --> Gate/Check --> LLM Call 2 --> Output
```

Use when tasks decompose cleanly into fixed subtasks. Trades latency for accuracy.

#### Routing
Classify input and direct to specialised handlers:

```
Input --> Classifier LLM --> Route A (General Questions)
                         --> Route B (Refunds)
                         --> Route C (Technical Support)
```

#### Parallelisation
Run LLM calls simultaneously, aggregate results. Two variations:
- **Sectioning** -- independent subtasks in parallel
- **Voting** -- same task multiple times for diverse outputs

#### Orchestrator-Workers
Central LLM dynamically breaks down tasks, delegates to workers, synthesises:

```
Input --> Orchestrator LLM --> Worker 1 --> |
                           --> Worker 2 --> |--> Orchestrator --> Output
                           --> Worker N --> |
```

Differs from parallelisation: subtasks are determined dynamically, not pre-defined.

#### Evaluator-Optimiser
One LLM generates, another evaluates in a loop:

```
Input --> Generator LLM --> Evaluator LLM --> (feedback loop) --> Output
```

### 6.3 Autonomous Agents

True agents operate independently with:
- Complex input understanding and planning
- Tool use with error recovery
- Environmental feedback (tool results, code execution)
- Human checkpoints at critical junctures
- Maximum iteration stopping conditions

### 6.4 Multi-Agent Research System (Production)

Anthropic's production Research feature uses an **orchestrator-worker** architecture:

| Component | Role |
|-----------|------|
| **Lead Agent** (Claude Opus 4) | Analyses query, develops strategy, spawns subagents, synthesises results |
| **Subagents** (Claude Sonnet 4) | Execute independent web searches, evaluate results, return findings |
| **Citation Agent** | Post-processes final document for source attribution |

**Communication flow:**
1. User submits query
2. Lead agent analyses and decomposes into subtasks
3. Lead agent spawns 3-5 subagents in parallel
4. Each subagent gets: objective, output format, tool guidance, task boundaries
5. Subagents iteratively search and filter results
6. Lead agent synthesises findings, optionally spawns more subagents
7. Citation agent adds source attribution

**Scaling rules embedded in prompts:**
- Simple fact-finding: 1 subagent, 3-10 tool calls
- Direct comparisons: 2-4 subagents
- Complex research: 5+ subagents with parallel tool calls

**Performance:**
- Multi-agent outperformed single-agent Claude Opus 4 by **90.2%** on internal eval
- Token usage explains ~80% of performance variance
- Parallel tool calling reduced time by up to **90%** for complex queries
- Multi-agent uses ~**15x** more tokens than single-agent

**Key engineering lessons:**
1. Tool design is as critical as prompt design
2. Agents work best: broad queries first, then progressively narrow
3. Extended thinking mode for planning and assessment
4. Full production tracing for non-deterministic debugging
5. Rainbow deployments for safe rollout
6. Context management: summarise completed phases, spawn fresh subagents near limits

### 6.5 Tool Design Principles

Anthropic invested more time optimising tools than prompts:
- Each tool needs a distinct purpose and clear description
- Use absolute paths, not relative (prevents errors)
- Docstrings become tool descriptions -- invest in them
- Test tools independently before agent integration

### 6.6 Strengths and Weaknesses

| Strengths | Weaknesses |
|-----------|------------|
| Principled, composable pattern catalogue | Patterns are conceptual -- no unified framework/SDK |
| Production-proven orchestrator-worker system | Relies on Claude models (Opus/Sonnet cost) |
| Emphasis on simplicity and transparency | 15x token overhead for multi-agent |
| Extended thinking enhances planning | Currently synchronous subagent execution |
| Excellent tool design guidance | No open-source reference implementation of Research system |
| MCP for tool integration | |

---

## 7. MetaGPT

### 7.1 Architecture

MetaGPT simulates an entire software company using LLM-powered agents. Its core philosophy is **"Code = SOP(Team)"** -- Standard Operating Procedures materialised into executable multi-agent workflows.

The framework implements an **assembly-line paradigm** where requirements flow through sequential stages, each handled by a specialised agent role.

### 7.2 Core Abstractions

| Abstraction | Description |
|-------------|-------------|
| **Role** | A specialised agent persona (e.g., ProductManager, Architect) |
| **Action** | A capability a Role can perform, with prompts and execution logic |
| **Message** | Communication unit between Roles, published to a shared message pool |
| **Environment** | Shared context where all agents operate and messages are broadcast |
| **Team** | A group of Roles working in a shared Environment |

### 7.3 Agent Roles

| Role | Responsibility |
|------|---------------|
| **Product Manager** | Analyses requirements, produces PRD (Product Requirements Document) |
| **Architect** | Designs system architecture, API definitions, data structures |
| **Project Manager** | Breaks project into tasks, assigns to engineers |
| **Engineer** | Implements code based on task assignments |
| **QA Engineer** | Writes unit tests, reviews code for bugs |

### 7.4 Message-Based Communication

Agents communicate through a shared message pool. Each Role _watches_ specific Action types from upstream Roles:

```python
from metagpt.actions import Action
from metagpt.roles import Role

class SimpleWriteCode(Action):
    PROMPT_TEMPLATE = """Write a Python function that {instruction}."""

    async def run(self, instruction: str):
        prompt = self.PROMPT_TEMPLATE.format(instruction=instruction)
        response = await self._aask(prompt)
        return parse_code(response)

class SimpleCoder(Role):
    name = "SimpleCoder"
    profile = "Coder"

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        self._watch([UserRequirement])       # Triggers on user input
        self.set_actions([SimpleWriteCode])   # Can perform this action

class SimpleTester(Role):
    name = "SimpleTester"
    profile = "Tester"

    def __init__(self, **kwargs):
        super().__init__(**kwargs)
        self._watch([SimpleWriteCode])        # Triggers when code is written
        self.set_actions([SimpleWriteTest])    # Generates tests
```

**Communication mechanism:**
1. A Role completes an Action
2. The result is published as a Message to the shared pool
3. Any Role that _watches_ that Action type receives the message
4. That Role's `_observe` -> `_think` -> `_act` cycle triggers

### 7.5 Team Assembly

```python
from metagpt.team import Team

team = Team()
team.hire([
    SimpleCoder(),
    SimpleTester(),
    SimpleReviewer(),
])
team.run_project("Create a 2048 game")
```

Or via CLI:
```bash
metagpt "Create a 2048 game"
```

### 7.6 SOP Workflow

The SOP chain for software development:

```
User Requirement
    --> Product Manager (PRD)
        --> Architect (System Design, API specs)
            --> Project Manager (Task breakdown)
                --> Engineer (Code implementation)
                    --> QA Engineer (Unit tests, review)
```

Each stage validates outputs from the previous stage, reducing error propagation.

### 7.7 MGX (MetaGPT X)

As of February 2025, MetaGPT launched **MGX** -- a natural language programming product described as "the world's first AI agent development team."

### 7.8 Strengths and Weaknesses

| Strengths | Weaknesses |
|-----------|------------|
| Realistic software company simulation | Primarily focused on software development |
| SOP-based workflows reduce hallucination errors | Complex role definitions for custom workflows |
| Assembly-line ensures sequential quality gates | Limited to Python 3.9-3.11 |
| Message-based agent communication | Heavy token usage for full pipeline |
| Watch mechanism creates natural dependencies | Less flexible than general-purpose frameworks |
| Academic rigour (ICLR 2025 top 1.8%) | Not easily adaptable to non-software domains |

---

## 8. ChatDev

### 8.1 Architecture

ChatDev (by OpenBMB) is a multi-agent framework that simulates a software company using chat-based agent collaboration. It follows a **waterfall-style lifecycle** with four phases: Design, Coding, Testing, Documentation.

**ChatDev 2.0** ("DevAll", January 2026) evolved into a zero-code multi-agent orchestration platform with:
- FastAPI backend
- Vue 3 web console
- Configurable workflow engine
- DAG-based agent topologies

### 8.2 Chat Chain Mechanism

The framework structures development as a hierarchical workflow:

```
Chain (C)
├── Phase: Design
│   └── Subtask: Requirements discussion (CEO <-> CPO)
│   └── Subtask: Architecture design (CTO <-> CPO)
├── Phase: Coding
│   └── Subtask: Code writing (CTO <-> Programmer)
│   └── Subtask: Code completion (CTO <-> Programmer)
├── Phase: Testing
│   └── Subtask: Code review / static testing (Reviewer <-> Programmer)
│   └── Subtask: System testing / dynamic testing (Tester <-> Programmer)
└── Phase: Documentation
    └── Subtask: Document generation
```

Each subtask involves a **dual-agent pair**: an Instructor and an Assistant who communicate iteratively until consensus.

### 8.3 Agent Roles

| Role | Function |
|------|----------|
| **CEO** | Requirements gathering, high-level decisions |
| **CPO** | Product requirements, feature prioritisation |
| **CTO** | Architecture decisions, technical specifications |
| **Programmer** | Code implementation, bug fixing |
| **Designer** | UI/UX design, GUI implementation |
| **Tester** | Test execution, dynamic testing |
| **Reviewer** | Code review, static analysis |

### 8.4 Communication Pattern

**Instructor-Assistant dialogue:**
1. Instructor (e.g., CTO) initiates with instructions
2. Assistant (e.g., Programmer) responds with solution
3. Dialogue continues until consensus or iteration limit
4. Solution is extracted and passed to next phase

**Communicative Dehallucination:**
To reduce coding hallucinations, ChatDev uses **role reversal**: the Assistant proactively asks for clarification before responding:
```
Assistant requests specifics --> Instructor provides details --> Assistant optimises
```

### 8.5 Memory Architecture

| Type | Scope | Purpose |
|------|-------|---------|
| **Short-term** | Within a single phase | Conversation history for context-aware decisions |
| **Long-term** | Across phases | Solutions from completed phases initiate subsequent phases |

Long-term memory prevents information overload by only passing relevant outputs forward.

### 8.6 Inception Prompting

ChatDev uses **inception prompting** to initialise and sustain agent communication through system prompts, preventing:
- Role-flipping (agent adopting wrong persona)
- Instruction-repeating (agent echoing instructions instead of acting)

### 8.7 ChatDev 2.0 (DevAll)

The evolution to ChatDev 2.0 shifts from rigid hierarchy to flexible agent networks:
- DAG-based task collaboration supporting 1000+ agents
- Visual workflow canvas (drag-and-drop)
- Human-in-the-loop feedback mechanisms
- Templates for data visualisation, 3D generation, game development, deep research
- Python SDK for programmatic execution

### 8.8 Strengths and Weaknesses

| Strengths | Weaknesses |
|-----------|------------|
| Realistic software company simulation | Waterfall model is inflexible |
| Chat-based communication is natural and debuggable | Dual-agent pairs can be slow (many turns) |
| Communicative dehallucination reduces errors | ChatDev 1.0 had rigid topology |
| Short/long-term memory architecture | Primarily software development focused |
| ChatDev 2.0 adds flexible DAG topologies | 2.0 is a significant architecture change |
| MacNet scales to 1000+ agents | Limited to Python ecosystem |

---

## 9. Cross-Framework Comparison

### 9.1 Taxonomy of Communication Patterns

| Pattern | Frameworks |
|---------|------------|
| **Handoff (transfer control)** | Swarm, Agents SDK, AutoGen Swarm |
| **Shared message pool** | MetaGPT, AutoGen Core |
| **Sequential pipeline** | CrewAI (sequential), MetaGPT (SOP), ChatDev (waterfall) |
| **Supervisor/orchestrator** | LangGraph, CrewAI (hierarchical), Anthropic orchestrator-worker |
| **Round-robin turns** | AutoGen RoundRobinGroupChat |
| **LLM-selected speaker** | AutoGen SelectorGroupChat |
| **Protocol-based RPC** | Google A2A |

### 9.2 Agent Discovery Mechanisms

| Framework | Discovery |
|-----------|-----------|
| **Swarm / Agents SDK** | Hardcoded in agent's tool list |
| **CrewAI** | Crew definition lists all agents; delegation finds by role |
| **AutoGen** | Team constructor enumerates agents; descriptions used for selection |
| **LangGraph** | Graph edges define agent connectivity |
| **Google A2A** | Agent Cards at `.well-known/agent-card.json` |
| **Anthropic** | Lead agent decides based on task decomposition |
| **MetaGPT** | `_watch()` on action types creates implicit discovery |
| **ChatDev** | Phase configuration defines agent pairings |

### 9.3 Delegation and Result Collection

| Framework | Delegation | Result Collection |
|-----------|-----------|------------------|
| **Swarm** | Return Agent from function | Response object with messages and final agent |
| **Agents SDK** | `handoffs` parameter, `Handoff` class | `Runner.run()` returns final output |
| **CrewAI** | `allow_delegation=True`, manager in hierarchical | `crew.kickoff()` returns aggregated result |
| **AutoGen** | HandoffMessage, SelectorGroupChat routing | `team.run()` returns TaskResult with messages |
| **LangGraph** | Supervisor routes via tool calls | State object accumulates all node outputs |
| **A2A** | SendMessage to remote agent | Task with Artifacts on completion |
| **Anthropic** | Lead spawns subagents with objectives | Subagents return findings; lead synthesises |
| **MetaGPT** | Messages trigger downstream Roles | Final Role's output is the result |
| **ChatDev** | Phase configuration pairs instructor/assistant | Chain output flows to next phase |

### 9.4 State Management

| Framework | State Model |
|-----------|-------------|
| **Swarm** | Stateless; `context_variables` dict passed explicitly |
| **Agents SDK** | Sessions for persistent memory |
| **CrewAI** | Flow state management across steps; Crew internal state |
| **AutoGen** | Agent memory, team state serialisation, checkpointing |
| **LangGraph** | `State` TypedDict flowing through graph; checkpointers |
| **A2A** | Task state machine (submitted/working/completed/failed) |
| **Anthropic** | Context windows; external memory for long-horizon tasks |
| **MetaGPT** | Shared Environment; Role Memory (message history) |
| **ChatDev** | Short-term (per-phase) + long-term (cross-phase) memory |

---

## 10. Relevance to Cortex

### 10.1 Patterns Directly Applicable

Based on Cortex's message-driven architecture with RabbitMQ, authority tiers, and skill-based agents:

1. **Message-based communication** (MetaGPT's shared pool, AutoGen's topic pub/sub) maps directly to Cortex's `IMessageBus` with topic exchange routing.

2. **Agent Cards** (A2A) align with Cortex's skill registry -- agents advertise capabilities through `ISkillRegistry` rather than JSON endpoints, but the concept is identical.

3. **Task lifecycle states** (A2A: submitted/working/completed/failed) map to Cortex's delegation tracking (`DelegationRecord`, `IDelegationTracker`).

4. **Authority-gated handoffs** -- Swarm/Agents SDK handoffs are flat. Cortex can enhance this with authority tier checks (JustDoIt/DoItAndShowMe/AskMeFirst) before allowing delegation.

5. **Orchestrator-worker** (Anthropic) is the most production-proven pattern and maps to Cortex's `AgentHarness` (per-agent lifecycle) coordinated by `AgentRuntime`.

6. **Watch/subscribe mechanism** (MetaGPT's `_watch()`) maps to Cortex's `IMessageConsumer.StartConsumingAsync` with queue bindings.

### 10.2 Design Considerations

| Decision | Recommendation | Rationale |
|----------|---------------|-----------|
| Agent communication | Message bus (already in place) | Matches MetaGPT shared pool and AutoGen topic pub/sub |
| Agent discovery | Skill registry + agent card metadata | Combines CrewAI role descriptions with A2A's structured cards |
| Delegation | Authority-gated handoff messages | Extends Swarm's handoff pattern with Cortex's 3-tier authority model |
| Orchestration | Configurable per-team (flat, hierarchical, sequential) | Supports CrewAI's process types within Cortex's team construct |
| State management | Delegation tracker + message context | Combines A2A task lifecycle with Anthropic's context management |
| Result collection | Completion messages back through bus | Aligns with A2A's artifact model and MetaGPT's message pool |

### 10.3 What to Avoid

- **LLM-based speaker selection** (AutoGen SelectorGroupChat) adds latency and cost per turn -- use deterministic routing where possible
- **Stateless handoffs** (Swarm) lose context in durable systems -- prefer explicit state in messages
- **Rigid waterfall phases** (ChatDev 1.0) are too inflexible for a general business framework
- **15x token overhead** (Anthropic multi-agent) -- carefully evaluate when multi-agent justifies the cost
