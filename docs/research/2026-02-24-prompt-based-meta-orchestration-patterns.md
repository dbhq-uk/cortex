# Prompt-Based Meta-Orchestration Patterns -- Reference Catalogue

**Date:** 2026-02-24
**Purpose:** Catalogue every useful pattern, concept, checklist, metric, protocol, integration pattern, and design principle identified from community-sourced prompt-based meta-orchestration agent definitions. This reference document maps proven multi-agent orchestration patterns onto the Cortex architecture, drawing from analysis of 11 distinct orchestration role archetypes found across open-source subagent catalogues and industry practice.

> **Part of the [Multi-Agent Orchestration Research Corpus](./README.md).** For framework implementations of these patterns see [Swarm Frameworks](./2026-02-24-agent-swarm-frameworks.md). For team-building lifecycle see [Team-Building Agents](./2026-02-24-team-building-agents.md). For error resilience deep-dive see [Initial Patterns §1.5](./2026-02-23-agent-orchestration-patterns.md#15-error-coordination-patterns).

---

## Table of Contents

1. [Overview](#1-overview)
2. [Agent Catalogue](#2-agent-catalogue)
3. [Orchestration Patterns Extracted](#3-orchestration-patterns-extracted)
4. [Operational Checklists](#4-operational-checklists)
5. [Cross-Agent Integration Map](#5-cross-agent-integration-map)
6. [Model Tier Strategy](#6-model-tier-strategy)
7. [Three-Phase Execution Pattern](#7-three-phase-execution-pattern)
8. [Knowledge Management Patterns](#8-knowledge-management-patterns)
9. [Observability Patterns](#9-observability-patterns)
10. [Relevance to Cortex](#10-relevance-to-cortex)

---

## 1. Overview

### Prompt-Based Subagent Definitions

Community-driven catalogues of prompt-based subagent definitions have emerged as a practical pattern for multi-agent orchestration. Each agent archetype is defined via a structured specification containing:

- **name** -- identifier used to invoke the agent
- **description** -- when-to-use guidance (the dispatch heuristic for agent selection)
- **tools** -- which tools the agent may use (file operations, search, web access, shell commands)
- **model** -- which model tier to use (lightweight, balanced, or heavyweight reasoning)

The body of each definition is a system prompt that encodes the agent's persona, domain expertise, checklists, communication protocols, workflow phases, and integration points with other agents.

### How Prompt-Based Subagents Work

These are not autonomous runtime processes. They are **prompt-based subagents** invoked by a parent orchestrator. When a parent agent (or user) dispatches a task:

1. The orchestrator selects the agent whose description best matches the task
2. A child execution context is initialised with the agent's system prompt
3. The child context is granted access only to declared tools (principle of least privilege)
4. The child executes at the declared model tier (cost/capability optimisation)
5. The result is returned to the parent context

The meta-orchestration category contains agents that specialise in coordinating other agents -- they are the conductors, not the instruments.

### Design Philosophy

Meta-orchestration agents are conductors and coordinators, managing complex multi-agent workflows and optimising AI system performance. These specialists excel at the meta-level -- orchestrating other agents, managing context, distributing tasks, and ensuring smooth collaboration between multiple AI systems.

---

## 2. Agent Catalogue

### 2.1 Summary Table

| # | Agent Name | Model Tier | Tools | Primary Responsibility |
|---|-----------|------------|-------|----------------------|
| 1 | **agent-installer** | haiku | Bash, WebFetch, Read, Write, Glob | Browse and install agents from a shared agent catalogue |
| 2 | **agent-organizer** | sonnet | Read, Write, Edit, Glob, Grep | Assemble and optimise multi-agent teams; task decomposition; agent capability matching |
| 3 | **context-manager** | sonnet | Read, Write, Edit, Glob, Grep | Shared state management; information retrieval; data synchronisation across agents |
| 4 | **error-coordinator** | sonnet | Read, Write, Edit, Glob, Grep | Distributed error handling; cascade prevention; recovery orchestration; learning from failures |
| 5 | **it-ops-orchestrator** | sonnet | Read, Write, Edit, Bash, Glob, Grep | Route IT operations tasks to specialist agents (PowerShell, .NET, Azure, M365) |
| 6 | **knowledge-synthesizer** | sonnet | Read, Write, Edit, Glob, Grep | Extract patterns from agent interactions; build knowledge graphs; enable organisational learning |
| 7 | **multi-agent-coordinator** | **opus** | Read, Write, Edit, Glob, Grep | Coordinate concurrent agents; inter-agent communication; distributed workflow execution |
| 8 | **performance-monitor** | haiku | Read, Write, Edit, Glob, Grep | Metrics collection; anomaly detection; SLO management; observability infrastructure |
| 9 | **task-distributor** | haiku | Read, Write, Edit, Glob, Grep | Queue management; load balancing; priority scheduling; work allocation |
| 10 | **workflow-orchestrator** | **opus** | Read, Write, Edit, Glob, Grep | Business process workflows; state machines; saga patterns; transaction management |
| 11 | **taskade** | *(external MCP)* | *(MCP server)* | AI-powered workspace with autonomous agents (external integration via MCP) |

### 2.2 When-to-Use Guide (from README)

| If you need to... | Use this agent |
|-------------------|---------------|
| Coordinate multiple agents | agent-organizer |
| Manage context efficiently | context-manager |
| Handle system errors | error-coordinator |
| Combine knowledge sources | knowledge-synthesizer |
| Scale agent operations | multi-agent-coordinator |
| Monitor performance | performance-monitor |
| Distribute tasks | task-distributor |
| Manage projects with AI agents | taskade |
| Automate workflows | workflow-orchestrator |

### 2.3 Recommended Composition Patterns (from README)

**Complex Problem Solving:**
- agent-organizer for task breakdown
- task-distributor for work allocation
- knowledge-synthesizer for result combination
- error-coordinator for failure handling

**Large-Scale Operations:**
- multi-agent-coordinator for ecosystem management
- performance-monitor for optimisation
- workflow-orchestrator for process automation
- context-manager for efficiency

**Workflow Automation:**
- workflow-orchestrator for process design
- task-distributor for work distribution
- error-coordinator for resilience
- performance-monitor for optimisation

**Knowledge Management:**
- knowledge-synthesizer for information fusion
- context-manager for memory optimisation
- agent-organizer for research coordination
- workflow-orchestrator for knowledge workflows

---

## 3. Orchestration Patterns Extracted

### 3.1 Task Decomposition Patterns

From **agent-organizer** -- the primary task decomposition agent:

**Decomposition steps:**
1. Requirement analysis
2. Subtask identification
3. Dependency mapping
4. Complexity assessment
5. Resource estimation
6. Timeline planning
7. Risk evaluation
8. Success criteria definition

**Task evaluation procedure:**
1. Parse requirements
2. Identify subtasks
3. Map dependencies
4. Estimate complexity
5. Assess resources
6. Define milestones
7. Plan workflow
8. Set checkpoints

From **it-ops-orchestrator** -- domain-specific decomposition:
- Break ambiguous problems into sub-problems
- Assign each sub-problem to the correct specialist agent
- Merge responses into a coherent unified solution
- Enforce safety, least privilege, and change review workflows

**Routing examples showing decomposition in practice:**

| Ambiguous Task | Sub-problem 1 | Sub-problem 2 | Sub-problem 3 |
|---------------|--------------|--------------|--------------|
| "Audit stale AD users and disable them" | Enumeration (powershell-5.1-expert) | Safety validation (ad-security-reviewer) | Implementation plan (windows-infra-admin) |
| "Create cost-optimized Azure VM deployments" | Architecture (azure-infra-engineer) | Script automation (powershell-7-expert) | -- |
| "Secure scheduled tasks containing credentials" | Security review (powershell-security-hardening) | Implementation (powershell-5.1-expert) | -- |

### 3.2 Agent Selection / Capability Matching Patterns

From **agent-organizer**:

**Agent capability mapping dimensions:**
1. Skill inventory
2. Performance metrics (historical success rates)
3. Specialisation areas
4. Availability status
5. Cost factors
6. Compatibility matrix (can this agent work with others in the team?)
7. Historical success rate
8. Workload capacity

**Selection criteria (enumerated):**
1. Capability matching -- does the agent have the required skills?
2. Performance history -- how well has it performed similar tasks?
3. Cost considerations -- what is the cost of running this agent?
4. Availability checking -- is the agent currently free?
5. Load balancing -- is the agent already overloaded?
6. Specialisation mapping -- is this the agent's primary domain?
7. Compatibility verification -- does it work well with the other team members?
8. Backup selection -- is there a fallback agent if this one fails?

**Team assembly criteria:**
1. Optimal composition (minimum agents for maximum coverage)
2. Skill coverage (no gaps in required capabilities)
3. Role assignment (clear accountability)
4. Communication setup (how agents will exchange information)
5. Coordination rules (who leads, who follows)
6. Backup planning (redundancy for critical roles)
7. Resource allocation (budget per agent)
8. Timeline synchronisation (all agents aligned on deadlines)

From **it-ops-orchestrator** -- routing logic:

**Domain detection ("task smells"):**
- Language experts: PowerShell 5.1/7, .NET
- Infra experts: AD, DNS, DHCP, GPO, on-prem Windows
- Cloud experts: Azure, M365, Graph API
- Security experts: PowerShell hardening, AD security
- DX experts: module architecture, CLI design

**Preference rules:**
- PowerShell-first when the task involves automation
- PowerShell-first when the environment is Windows or hybrid
- PowerShell-first when the user expects scripts, tooling, or a module

### 3.3 Communication Protocols (JSON Message Formats)

Every agent uses a standard JSON message format for context queries. The common structure:

```json
{
  "requesting_agent": "<agent-name>",
  "request_type": "get_<domain>_context",
  "payload": {
    "query": "<natural language description of what context is needed>"
  }
}
```

**Per-agent context query types:**

| Agent | request_type | Payload query |
|-------|-------------|---------------|
| agent-organizer | `get_organization_context` | "Organization context needed: task requirements, available agents, performance constraints, budget limits, and success criteria." |
| context-manager | `get_context_requirements` | "Context requirements needed: data types, access patterns, consistency needs, performance targets, and compliance requirements." |
| error-coordinator | `get_error_context` | "Error context needed: system architecture, failure patterns, recovery procedures, SLAs, incident history, and resilience goals." |
| knowledge-synthesizer | `get_knowledge_context` | "Knowledge context needed: agent ecosystem, interaction history, performance data, existing knowledge base, learning goals, and improvement targets." |
| multi-agent-coordinator | `get_coordination_context` | "Coordination context needed: workflow complexity, agent count, communication patterns, performance requirements, and fault tolerance needs." |
| performance-monitor | `get_monitoring_context` | "Monitoring context needed: system architecture, agent topology, performance SLAs, current metrics, pain points, and optimization goals." |
| task-distributor | `get_distribution_context` | "Distribution context needed: task volumes, agent capacities, priority schemes, performance targets, and constraint requirements." |
| workflow-orchestrator | `get_workflow_context` | "Workflow context needed: process requirements, integration points, error handling needs, performance targets, and compliance requirements." |

**Progress tracking JSON schemas (per-agent):**

```json
// agent-organizer
{
  "agent": "agent-organizer",
  "status": "orchestrating",
  "progress": {
    "agents_assigned": 12,
    "tasks_distributed": 47,
    "completion_rate": "94%",
    "avg_response_time": "3.2s"
  }
}

// context-manager
{
  "agent": "context-manager",
  "status": "managing",
  "progress": {
    "contexts_stored": "2.3M",
    "avg_retrieval_time": "47ms",
    "cache_hit_rate": "89%",
    "consistency_score": "100%"
  }
}

// error-coordinator
{
  "agent": "error-coordinator",
  "status": "coordinating",
  "progress": {
    "errors_handled": 3421,
    "recovery_rate": "93%",
    "cascade_prevented": 47,
    "mttr_minutes": 4.2
  }
}

// knowledge-synthesizer
{
  "agent": "knowledge-synthesizer",
  "status": "synthesizing",
  "progress": {
    "patterns_identified": 342,
    "insights_generated": 156,
    "recommendations_active": 89,
    "improvement_rate": "23%"
  }
}

// multi-agent-coordinator
{
  "agent": "multi-agent-coordinator",
  "status": "coordinating",
  "progress": {
    "active_agents": 87,
    "messages_processed": "234K/min",
    "workflow_completion": "94%",
    "coordination_efficiency": "96%"
  }
}

// performance-monitor
{
  "agent": "performance-monitor",
  "status": "monitoring",
  "progress": {
    "metrics_collected": 2847,
    "dashboards_created": 23,
    "alerts_configured": 156,
    "anomalies_detected": 47
  }
}

// task-distributor
{
  "agent": "task-distributor",
  "status": "distributing",
  "progress": {
    "tasks_distributed": "45K",
    "avg_queue_time": "230ms",
    "load_variance": "7%",
    "deadline_success": "97%"
  }
}

// workflow-orchestrator
{
  "agent": "workflow-orchestrator",
  "status": "orchestrating",
  "progress": {
    "workflows_active": 234,
    "execution_rate": "1.2K/min",
    "success_rate": "99.4%",
    "avg_duration": "4.7min"
  }
}
```

### 3.4 Coordination Patterns

Extracted from **agent-organizer** (enumerated orchestration patterns):

| Pattern | Description |
|---------|-------------|
| **Sequential execution** | Tasks run one after another in defined order |
| **Parallel processing** | Independent tasks run concurrently |
| **Pipeline patterns** | Output of one stage feeds input of next |
| **Map-reduce workflows** | Fan out work, collect and merge results |
| **Event-driven coordination** | Agents react to events rather than being polled |
| **Hierarchical delegation** | Tree-structured authority with parent-child relationships |
| **Consensus mechanisms** | Multiple agents must agree before proceeding |
| **Failover strategies** | Backup agents take over when primary fails |

Extracted from **multi-agent-coordinator** (coordination topologies):

| Pattern | Description |
|---------|-------------|
| **Master-worker** | Single coordinator dispatches to workers |
| **Peer-to-peer** | Agents communicate directly without central coordinator |
| **Hierarchical** | Layered delegation tree |
| **Publish-subscribe** | Agents subscribe to event topics |
| **Request-reply** | Synchronous ask-and-answer |
| **Pipeline** | Chain of processing stages |
| **Scatter-gather** | Broadcast request, collect all responses |
| **Consensus-based** | Quorum-based decision making |

**Parallel execution patterns (multi-agent-coordinator):**
1. Task partitioning
2. Work distribution
3. Load balancing
4. Synchronisation points
5. Barrier coordination
6. Fork-join patterns
7. Map-reduce workflows
8. Result merging

**Communication mechanisms (multi-agent-coordinator):**
1. Message passing
2. Shared memory
3. Event streams
4. RPC calls
5. WebSocket connections
6. REST APIs
7. GraphQL subscriptions
8. Queue systems

### 3.5 Workflow Patterns

From **workflow-orchestrator**:

**State machine patterns:**
- State persistence
- Transition validation
- Consistency checks
- Rollback support
- Version control
- Migration strategies
- Recovery procedures
- Audit logging

**Process patterns (enumerated):**
1. Sequential flow
2. Parallel split/join
3. Exclusive choice
4. Loops and iterations
5. Event-based gateway
6. Compensation
7. Sub-processes
8. Time-based events

**Transaction management patterns:**
1. ACID properties
2. Saga patterns
3. Two-phase commit
4. Compensation logic
5. Idempotency
6. State consistency
7. Rollback procedures
8. Distributed transactions

**Workflow management (multi-agent-coordinator):**
1. DAG execution
2. State machines
3. Saga patterns
4. Compensation logic
5. Checkpoint/restart
6. Dynamic workflows
7. Conditional branching
8. Loop handling

**Event orchestration (workflow-orchestrator):**
1. Event sourcing
2. Event correlation
3. Trigger management
4. Timer events
5. Signal handling
6. Message events
7. Conditional events
8. Escalation events

**Human task integration (workflow-orchestrator):**
1. Task assignment
2. Approval workflows
3. Escalation rules
4. Delegation handling
5. Form integration
6. Notification systems
7. SLA tracking
8. Workload balancing

**Execution engine requirements (workflow-orchestrator):**
1. State persistence
2. Transaction support
3. Rollback capabilities
4. Checkpoint/restart
5. Dynamic modifications
6. Version migration
7. Performance tuning
8. Resource management

### 3.6 Error Handling Patterns

From **error-coordinator** -- the dedicated resilience agent:

**Failure cascade prevention:**
1. Circuit breaker patterns
2. Bulkhead isolation
3. Timeout management
4. Rate limiting
5. Backpressure handling
6. Graceful degradation
7. Failover strategies
8. Load shedding

**Circuit breaker management:**
1. Threshold configuration
2. State transitions (closed -> open -> half-open -> closed)
3. Half-open testing
4. Success criteria
5. Failure counting
6. Reset timers
7. Monitoring integration
8. Alert coordination

**Retry strategy coordination:**
1. Exponential backoff
2. Jitter implementation
3. Retry budgets
4. Dead letter queues
5. Poison pill handling
6. Retry exhaustion
7. Alternative paths
8. Success tracking

**Fallback mechanisms:**
1. Cached responses
2. Default values
3. Degraded service
4. Alternative providers
5. Static content
6. Queue-based processing
7. Asynchronous handling
8. User notification

**Recovery orchestration:**
1. Automated recovery flows
2. Rollback procedures
3. State restoration
4. Data reconciliation
5. Service restoration
6. Health verification
7. Gradual recovery
8. Post-recovery validation

**Recovery strategy hierarchy:**
1. Immediate retry
2. Delayed retry
3. Alternative path
4. Cached fallback
5. Manual intervention
6. Partial recovery
7. Full restoration
8. Preventive action

**Error taxonomy (classification):**
1. Infrastructure errors
2. Application errors
3. Integration failures
4. Data errors
5. Timeout errors
6. Permission errors
7. Resource exhaustion
8. External failures

**Cross-agent error correlation:**
1. Temporal correlation
2. Causal analysis
3. Dependency tracking
4. Service mesh analysis
5. Request tracing
6. Error propagation
7. Root cause identification
8. Impact assessment

**Post-mortem automation:**
1. Incident timeline
2. Data collection
3. Impact analysis
4. Root cause detection
5. Action item generation
6. Documentation creation
7. Learning extraction
8. Process improvement

**Chaos engineering (proactive resilience testing):**
1. Failure injection
2. Load testing
3. Latency injection
4. Resource constraints
5. Network partitions
6. State corruption
7. Recovery testing
8. Resilience validation

**Incident management workflow:**
1. Detection protocols
2. Severity classification
3. Escalation paths
4. Communication plans
5. War room procedures
6. Recovery coordination
7. Status updates
8. Post-incident review

### 3.7 Load Balancing Strategies

From **task-distributor**:

**Distribution strategies (enumerated):**
1. Round-robin
2. Weighted distribution
3. Least connections
4. Random selection
5. Consistent hashing
6. Capacity-based
7. Performance-based
8. Affinity routing

**Load balancing excellence criteria:**
1. Algorithm tuning
2. Weight optimisation
3. Health monitoring
4. Failover speed
5. Geographic awareness
6. Affinity optimisation
7. Cost balancing
8. Energy efficiency

### 3.8 Queue Management Patterns

From **task-distributor**:

**Queue architecture dimensions:**
1. Queue architecture (topology)
2. Priority levels
3. Message ordering
4. TTL handling
5. Dead letter queues
6. Retry mechanisms
7. Batch processing
8. Queue monitoring

**Queue optimisation:**
1. Priority design
2. Batch strategies
3. Overflow handling
4. Retry policies
5. TTL management
6. Dead letter processing
7. Archive procedures
8. Performance tuning

**Priority scheduling:**
1. Priority schemes
2. Deadline management
3. SLA enforcement
4. Preemption rules
5. Starvation prevention
6. Emergency handling
7. Resource reservation
8. Fair scheduling

### 3.9 Dependency Management

From **agent-organizer**:

1. Task dependencies
2. Resource dependencies
3. Data dependencies
4. Timing constraints
5. Priority handling
6. Conflict resolution
7. Deadlock prevention
8. Flow optimisation

From **multi-agent-coordinator**:

1. Dependency graphs
2. Topological sorting
3. Circular detection
4. Resource locking
5. Priority scheduling
6. Constraint solving
7. Deadlock prevention
8. Race condition handling

**Dependency resolution (advanced):**
1. Graph algorithms
2. Priority scheduling
3. Resource allocation
4. Lock optimisation
5. Conflict resolution
6. Parallel planning
7. Critical path analysis
8. Bottleneck removal

---

## 4. Operational Checklists

### 4.1 Target Metrics by Agent

Each agent defines specific operational targets. These are the SLO-equivalent metrics embedded in the agent prompts.

#### agent-organizer
| Metric | Target |
|--------|--------|
| Agent selection accuracy | > 95% |
| Task completion rate | > 99% |
| Resource utilisation | Optimal |
| Response time | < 5s |
| Error recovery | Automated |
| Cost tracking | Enabled |
| Performance monitoring | Continuous |
| Team synergy | Maximised |

#### context-manager
| Metric | Target |
|--------|--------|
| Retrieval time | < 100ms |
| Data consistency | 100% |
| Availability | > 99.9% |
| Version tracking | Enabled |
| Access control | Enforced |
| Privacy compliance | Consistent |
| Audit trail | Complete |
| Performance | Optimal |

#### error-coordinator
| Metric | Target |
|--------|--------|
| Error detection latency | < 30 seconds |
| Recovery success rate | > 90% |
| Cascade prevention | 100% |
| False positives | < 5% |
| MTTR | < 5 minutes |
| Documentation | Automated |
| Learning capture | Systematic |
| Resilience improvement | Continuous |

#### knowledge-synthesizer
| Metric | Target |
|--------|--------|
| Pattern accuracy | > 85% |
| Insight relevance | > 90% |
| Knowledge retrieval | < 500ms |
| Update frequency | Daily |
| Coverage | Comprehensive |
| Validation | Systematic |
| Evolution tracking | Continuous |
| Distribution | Automated |

#### multi-agent-coordinator
| Metric | Target |
|--------|--------|
| Coordination overhead | < 5% |
| Deadlock prevention | 100% |
| Message delivery | Guaranteed |
| Scalability | 100+ agents |
| Fault tolerance | Built-in |
| Monitoring | Comprehensive |
| Recovery | Automated |
| Performance | Optimal |

#### performance-monitor
| Metric | Target |
|--------|--------|
| Metric latency | < 1 second |
| Data retention | 90 days |
| Alert accuracy | > 95% |
| Dashboard load time | < 2 seconds |
| Anomaly detection latency | < 5 minutes |
| Resource overhead | < 2% |
| System availability | 99.99% |
| Insights | Actionable |

#### task-distributor
| Metric | Target |
|--------|--------|
| Distribution latency | < 50ms |
| Load balance variance | < 10% |
| Task completion rate | > 99% |
| Priority respected | 100% |
| Deadlines met | > 95% |
| Resource utilisation | > 80% |
| Queue overflow | Prevented |
| Fairness | Maintained |

#### workflow-orchestrator
| Metric | Target |
|--------|--------|
| Workflow reliability | > 99.9% |
| State consistency | 100% |
| Recovery time | < 30s |
| Version compatibility | Verified |
| Audit trail | Complete |
| Performance tracking | Continuous |
| Monitoring | Enabled |
| Flexibility | Maintained |

### 4.2 Delivery Notification Templates

Each agent has a standard delivery notification template containing aspirational metrics. These serve as success-story templates:

| Agent | Delivery Notification |
|-------|----------------------|
| agent-organizer | "Agent orchestration completed. Coordinated 12 agents across 47 tasks with 94% first-pass success rate. Average response time 3.2s with 67% resource utilization. Achieved 23% performance improvement through optimal team composition and workflow design." |
| context-manager | "Context management system completed. Managing 2.3M contexts with 47ms average retrieval time. Cache hit rate 89% with 100% consistency score. Reduced storage costs by 43% through intelligent tiering and compression." |
| error-coordinator | "Error coordination established. Handling 3421 errors/day with 93% automatic recovery rate. Prevented 47 cascade failures and reduced MTTR to 4.2 minutes. Implemented learning system improving recovery effectiveness by 15% monthly." |
| knowledge-synthesizer | "Knowledge synthesis operational. Identified 342 patterns generating 156 actionable insights. Active recommendations improving system performance by 23%. Knowledge graph contains 50k+ entities enabling cross-agent learning and innovation." |
| multi-agent-coordinator | "Multi-agent coordination completed. Orchestrated 87 agents processing 234K messages/minute with 94% workflow completion rate. Achieved 96% coordination efficiency with zero deadlocks and 99.9% message delivery guarantee." |
| performance-monitor | "Performance monitoring implemented. Collecting 2847 metrics across 50 agents with <1s latency. Created 23 dashboards detecting 47 anomalies, reducing MTTR by 65%. Identified optimizations saving $12k/month in resource costs." |
| task-distributor | "Task distribution system completed. Distributed 45K tasks with 230ms average queue time and 7% load variance. Achieved 97% deadline success rate with 84% resource utilization. Reduced task wait time by 67% through intelligent routing." |
| workflow-orchestrator | "Workflow orchestration completed. Managing 234 active workflows processing 1.2K executions/minute with 99.4% success rate. Average duration 4.7 minutes with automated error recovery reducing manual intervention by 89%." |

---

## 5. Cross-Agent Integration Map

Every agent (except agent-installer and it-ops-orchestrator, which have different integration models) declares an "Integration with other agents" section. Below is the full extraction.

### 5.1 Integration Matrix

| Agent | Integrates With | Relationship |
|-------|----------------|-------------|
| **agent-organizer** | context-manager | Collaborate on information sharing |
| | multi-agent-coordinator | Support on execution |
| | task-distributor | Work with on load balancing |
| | workflow-orchestrator | Guide on process design |
| | performance-monitor | Help on metrics |
| | error-coordinator | Assist on recovery |
| | knowledge-synthesizer | Partner on learning |
| | *all agents* | Coordinate on task execution |
| **context-manager** | agent-organizer | Support with context access |
| | multi-agent-coordinator | Collaborate on state |
| | workflow-orchestrator | Work with on process context |
| | task-distributor | Guide on workload data |
| | performance-monitor | Help on metrics storage |
| | error-coordinator | Assist on error context |
| | knowledge-synthesizer | Partner on insights |
| | *all agents* | Coordinate on information needs |
| **error-coordinator** | performance-monitor | Work with on detection |
| | workflow-orchestrator | Collaborate on recovery |
| | multi-agent-coordinator | Support on resilience |
| | agent-organizer | Guide on error handling |
| | task-distributor | Help on failure routing |
| | context-manager | Assist on state recovery |
| | knowledge-synthesizer | Partner on learning |
| | *teams* | Coordinate on incident response |
| **knowledge-synthesizer** | *all agent interactions* | Extract from all |
| | performance-monitor | Collaborate on metrics |
| | error-coordinator | Support with failure patterns |
| | agent-organizer | Guide with team insights |
| | workflow-orchestrator | Help with process patterns |
| | context-manager | Assist with knowledge storage |
| | multi-agent-coordinator | Partner on optimisation |
| | *all agents* | Enable with collective intelligence |
| **multi-agent-coordinator** | agent-organizer | Collaborate on team assembly |
| | context-manager | Support on state synchronisation |
| | workflow-orchestrator | Work with on process execution |
| | task-distributor | Guide on work allocation |
| | performance-monitor | Help on metrics collection |
| | error-coordinator | Assist on failure handling |
| | knowledge-synthesizer | Partner on patterns |
| | *all agents* | Coordinate on communication |
| **performance-monitor** | agent-organizer | Support with performance data |
| | error-coordinator | Collaborate on incidents |
| | workflow-orchestrator | Work with on bottlenecks |
| | task-distributor | Guide on load patterns |
| | context-manager | Help on storage metrics |
| | knowledge-synthesizer | Assist with insights |
| | multi-agent-coordinator | Partner on efficiency |
| | *teams* | Coordinate on optimisation |
| **task-distributor** | agent-organizer | Collaborate on capacity planning |
| | multi-agent-coordinator | Support on workload distribution |
| | workflow-orchestrator | Work with on task dependencies |
| | performance-monitor | Guide on metrics |
| | error-coordinator | Help on retry distribution |
| | context-manager | Assist on state tracking |
| | knowledge-synthesizer | Partner on patterns |
| | *all agents* | Coordinate on task allocation |
| **workflow-orchestrator** | agent-organizer | Collaborate on process tasks |
| | multi-agent-coordinator | Support on distributed workflows |
| | task-distributor | Work with on work allocation |
| | context-manager | Guide on process state |
| | performance-monitor | Help on metrics |
| | error-coordinator | Assist on recovery flows |
| | knowledge-synthesizer | Partner on patterns |
| | *all agents* | Coordinate on process execution |

### 5.2 Integration for it-ops-orchestrator (domain-specific)

| Specialist Agent | Role |
|-----------------|------|
| powershell-5.1-expert / powershell-7-expert | Primary language specialists |
| powershell-module-architect | Reusable tooling architecture |
| windows-infra-admin | On-prem infrastructure work |
| azure-infra-engineer / m365-admin | Cloud routing targets |
| powershell-security-hardening / ad-security-reviewer | Security posture integration |
| security-auditor / incident-responder | Escalated tasks |

### 5.3 Key Integration Observations

1. **Every agent integrates with every other agent** -- the integration map is fully connected. Each agent explicitly names all 7 peers.
2. **knowledge-synthesizer has the broadest integration** -- it extracts from ALL agent interactions and enables ALL agents with collective intelligence.
3. **context-manager is the shared state backbone** -- every agent either reads from or writes to context.
4. **error-coordinator and performance-monitor form the observability pair** -- error-coordinator works with performance-monitor on detection, and performance-monitor collaborates with error-coordinator on incidents.
5. **multi-agent-coordinator and agent-organizer share team assembly** -- organizer designs teams, coordinator executes them.

---

## 6. Model Tier Strategy

### 6.1 Tier Assignments

| Model Tier | Agents | Count |
|-----------|--------|-------|
| **opus** (highest capability) | multi-agent-coordinator, workflow-orchestrator | 2 |
| **sonnet** (balanced) | agent-organizer, context-manager, error-coordinator, it-ops-orchestrator, knowledge-synthesizer | 5 |
| **haiku** (fastest/cheapest) | agent-installer, performance-monitor, task-distributor | 3 |

### 6.2 Assignment Rationale (Inferred)

**Opus tier -- reasoning-heavy coordination:**
- `multi-agent-coordinator` needs opus because it handles the most complex reasoning: inter-agent communication design, distributed workflow execution, dependency management across 100+ agents, deadlock prevention, and fault tolerance across large agent ecosystems. The prompt's checklist targets "Scalability to 100+ agents" and "Deadlock prevention 100%."
- `workflow-orchestrator` needs opus because it designs state machines, saga patterns, compensation logic, and distributed transactions. These require deep reasoning about state consistency, rollback procedures, and business process correctness.

**Sonnet tier -- analytical and planning work:**
- `agent-organizer` performs team assembly and task decomposition -- structured analysis that benefits from good reasoning but does not require peak capability.
- `context-manager` manages data architecture, synchronisation protocols, and schema design -- complex but more structured than open-ended reasoning.
- `error-coordinator` analyses error patterns, correlation, and recovery strategies -- analytical but pattern-based.
- `it-ops-orchestrator` does routing and decomposition in a well-defined domain -- good judgment needed but within bounded scope.
- `knowledge-synthesizer` extracts patterns and builds knowledge graphs -- analytical work with structured outputs.

**Haiku tier -- high-volume, structured operations:**
- `agent-installer` does simple file operations (browse, download, install) -- no reasoning required.
- `performance-monitor` collects and aggregates metrics -- high-volume, low-reasoning work. The checklist targets "Resource overhead < 2%" which demands a lightweight model.
- `task-distributor` implements scheduling algorithms -- formulaic, high-throughput operations. Target "Distribution latency < 50ms" demands speed over depth.

### 6.3 Design Principle

The model tier strategy follows a clear pattern:

1. **Use the most capable model for open-ended reasoning** about complex system coordination and state management
2. **Use the balanced model for analytical/planning work** that has structure but requires judgment
3. **Use the fastest/cheapest model for high-volume operational tasks** that are algorithmic and latency-sensitive

This maps directly to a cost-optimisation strategy: spend intelligence budget where reasoning depth matters most, and spend speed budget where throughput matters most.

---

## 7. Three-Phase Execution Pattern

### 7.1 The Universal Pattern

Every agent (except agent-installer and it-ops-orchestrator) follows the same three-phase execution pattern:

**Phase 1: Analysis** (naming varies: "Task Analysis", "Architecture Analysis", "Failure Analysis", "Knowledge Discovery", "Workflow Analysis", "System Analysis", "Workload Analysis", "Process Analysis")

**Phase 2: Implementation** (always called "Implementation Phase")

**Phase 3: Excellence** (naming varies: "Orchestration Excellence", "Context Excellence", "Resilience Excellence", "Intelligence Excellence", "Coordination Excellence", "Observability Excellence", "Distribution Excellence", "Orchestration Excellence")

### 7.2 Phase Details

#### Phase 1: Analysis

**Common structure across all agents:**
1. Assessment of the current state ("Analysis priorities" -- 8 items)
2. Domain-specific evaluation ("Evaluation" -- 8 items)

**Phase 1 triggers the communication protocol** -- the JSON context query is sent at this point.

#### Phase 2: Implementation

**Common structure across all agents:**
1. Implementation approach (8 items)
2. Domain-specific patterns (8 items)
3. **Progress tracking JSON** -- emitted during this phase

#### Phase 3: Excellence

**Common structure across all agents:**
1. Excellence checklist (8 items)
2. **Delivery notification** -- emitted at the end of this phase
3. Domain-specific optimisation sections

### 7.3 Invocation Protocol

Every agent follows the same 4-step invocation protocol:

```
When invoked:
1. Query context manager for <domain> requirements and <domain> state
2. Review existing <domain>, <dependencies>, and <history>
3. Analyze <complexity>, <patterns>, and <optimization opportunities>
4. Implement <solution> ensuring <quality attribute>
```

### 7.4 Progress Tracking Schema

The common schema for all progress tracking:

```json
{
  "agent": "<agent-name>",
  "status": "<phase-verb>",  // "orchestrating", "managing", "coordinating", "synthesizing", "monitoring", "distributing"
  "progress": {
    "<metric_1>": "<value>",
    "<metric_2>": "<value>",
    "<metric_3>": "<value>",
    "<metric_4>": "<value>"
  }
}
```

Each agent tracks exactly 4 progress metrics tailored to its domain.

---

## 8. Knowledge Management Patterns

Extracted entirely from **knowledge-synthesizer**.

### 8.1 Knowledge Extraction Pipeline

Eight-stage pipeline:
1. **Interaction mining** -- harvest data from agent-to-agent interactions
2. **Outcome analysis** -- correlate actions with results
3. **Pattern detection** -- identify recurring structures
4. **Success extraction** -- isolate what works
5. **Failure analysis** -- understand what fails and why
6. **Performance insights** -- quantify efficiency patterns
7. **Collaboration patterns** -- map how agents work together effectively
8. **Innovation capture** -- preserve novel approaches

### 8.2 Pattern Recognition Systems

Eight categories of patterns detected:
1. Workflow patterns
2. Success patterns
3. Failure patterns
4. Communication patterns
5. Resource patterns
6. Optimisation patterns
7. Evolution patterns
8. Emergence detection (new patterns appearing from existing ones)

### 8.3 Knowledge Graph Building

Eight-step process:
1. Entity extraction
2. Relationship mapping
3. Property definition
4. Graph construction
5. Query optimisation
6. Visualisation design
7. Update mechanisms
8. Version control

Target scale: 50k+ entities.

### 8.4 Knowledge Architecture (Layered)

1. **Extraction layer** -- pull data from sources
2. **Processing layer** -- clean, normalise, enrich
3. **Storage layer** -- persist in knowledge graph
4. **Analysis layer** -- run pattern detection
5. **Synthesis layer** -- combine patterns into insights
6. **Distribution layer** -- push insights to agents
7. **Feedback layer** -- collect effectiveness data
8. **Evolution layer** -- update patterns based on feedback

### 8.5 Learning Mechanisms

Eight types of learning:
1. Supervised learning
2. Unsupervised discovery
3. Reinforcement learning
4. Transfer learning
5. Meta-learning
6. Federated learning
7. Active learning
8. Continual learning

### 8.6 Learning Distribution

Knowledge is pushed to agents via:
1. Agent updates (direct pushes)
2. Best practice guides (documentation)
3. Performance alerts (triggered notifications)
4. Optimisation tips (contextual suggestions)
5. Warning systems (risk alerts)
6. Training materials (educational content)
7. API improvements (programmatic access)
8. Dashboard insights (visual summaries)

### 8.7 Knowledge Validation

Eight validation dimensions:
1. Accuracy testing
2. Relevance scoring
3. Impact measurement
4. Consistency checking
5. Completeness analysis
6. Timeliness verification
7. Cost-benefit analysis
8. User feedback

### 8.8 Advanced Analytics

Eight analytical capabilities:
1. Deep pattern mining
2. Predictive insights
3. Anomaly detection
4. Trend prediction
5. Impact analysis
6. Correlation discovery
7. Causation inference
8. Emergence detection

### 8.9 Innovation Enablement

Eight mechanisms for driving innovation:
1. Pattern combination (cross-pollination)
2. Cross-domain insights
3. Emergence facilitation
4. Experiment suggestions
5. Hypothesis generation
6. Risk assessment
7. Opportunity identification
8. Innovation tracking

### 8.10 Evolution Tracking

Eight dimensions of knowledge evolution:
1. Knowledge growth rate
2. Pattern changes over time
3. Performance trends
4. System maturity level
5. Innovation rate
6. Adoption metrics
7. Impact measurement
8. ROI calculation

---

## 9. Observability Patterns

Extracted entirely from **performance-monitor**.

### 9.1 Metric Collection Architecture

Eight-layer collection:
1. Agent instrumentation
2. Metric aggregation
3. Time-series storage
4. Data pipelines
5. Sampling strategies
6. Cardinality control
7. Retention policies
8. Export mechanisms

### 9.2 Monitoring Stack Design

Eight layers:
1. **Collection layer** -- instruments and collectors
2. **Aggregation layer** -- roll-ups and summaries
3. **Storage layer** -- time-series databases
4. **Query layer** -- metric query engine
5. **Visualisation layer** -- dashboards and charts
6. **Alert layer** -- rules and notifications
7. **Integration layer** -- connects to other systems
8. **API layer** -- programmatic access

### 9.3 Real-Time Monitoring

Eight capabilities:
1. Live dashboards
2. Streaming metrics
3. Alert triggers
4. Threshold monitoring
5. Rate calculations
6. Percentile tracking
7. Distribution analysis
8. Correlation detection

### 9.4 Anomaly Detection

Eight methods:
1. Statistical methods
2. Machine learning models
3. Pattern recognition
4. Outlier detection
5. Clustering analysis
6. Time-series forecasting
7. Alert suppression
8. Root cause hints

### 9.5 Performance Baselines

Eight baseline dimensions:
1. Historical analysis
2. Seasonal patterns
3. Normal ranges
4. Deviation tracking
5. Trend identification
6. Capacity planning
7. Growth projections
8. Benchmark comparisons

### 9.6 Bottleneck Identification

Eight techniques:
1. Performance profiling
2. Trace analysis
3. Dependency mapping
4. Critical path analysis
5. Resource contention
6. Lock analysis
7. Query optimisation
8. Service mesh insights

### 9.7 Dashboard Design

Eight visualisation types:
1. KPI visualisation
2. Service maps
3. Heat maps
4. Time series graphs
5. Distribution charts
6. Correlation matrices
7. Custom queries
8. Mobile views

### 9.8 Alert Management

Eight dimensions:
1. Alert rules
2. Severity levels
3. Routing logic
4. Escalation paths
5. Suppression rules
6. Notification channels
7. On-call integration
8. Incident creation

### 9.9 Distributed Tracing

Eight capabilities:
1. Request flow tracking
2. Latency breakdown
3. Service dependencies
4. Error propagation
5. Performance bottlenecks
6. Resource attribution
7. Cross-agent correlation
8. Root cause analysis

### 9.10 SLO Management

Eight practices:
1. SLI definition
2. Error budget tracking
3. Burn rate alerts
4. SLO dashboards
5. Reliability reporting
6. Improvement tracking
7. Stakeholder communication
8. Target adjustment

### 9.11 Metrics Inventory

Eight metric categories:
1. Business metrics
2. Technical metrics
3. User experience metrics
4. Cost metrics
5. Security metrics
6. Compliance metrics
7. Custom metrics
8. Derived metrics

### 9.12 Resource Tracking

Eight resource types:
1. CPU utilisation
2. Memory consumption
3. Network bandwidth
4. Disk I/O
5. Queue depths
6. Connection pools
7. Thread counts
8. Cache efficiency

### 9.13 Advanced Analytics

Eight predictive capabilities:
1. Predictive monitoring
2. Capacity forecasting
3. Cost prediction
4. Failure prediction
5. Performance modelling
6. What-if analysis
7. Optimisation simulation
8. Impact analysis

---

## 10. Relevance to Cortex

This section maps every extracted pattern to Cortex's existing architecture. References are to the current codebase in `src/Cortex.Agents/`, `src/Cortex.Messaging/`, `src/Cortex.Core/`, and `src/Cortex.Skills/`.

### 10.1 Agent Catalogue -> IAgent + AgentRegistration + AgentCapability

The prompt-based catalogue defines agents with: name, description, tools, model tier. Cortex already has:

- `IAgent` with `AgentId`, `Name`, `Capabilities` (list of `AgentCapability`)
- `AgentRegistration` with `AgentId`, `Name`, `AgentType` ("human" or "ai"), `Capabilities`, `RegisteredAt`, `IsAvailable`
- `AgentCapability` with `Name`, `Description`, `SkillIds`

**Gap: Model tier.** The catalogue's model-tier assignment (haiku/sonnet/opus) maps to a concept Cortex does not yet have: an agent's **computational weight** or **reasoning tier**. This could be added to `AgentRegistration` as a property (e.g., `ModelTier` or `ReasoningTier`) to enable cost-aware routing.

**Gap: Tool/capability restrictions.** The catalogue restricts each agent to specific tools. Cortex agents have `Capabilities` but these map to skills, not to tool restrictions. The authority model (JustDoIt/DoItAndShowMe/AskMeFirst) partially addresses this, but a per-agent tool allowlist would be a separate concern.

### 10.2 Orchestration Patterns -> IMessageBus + RabbitMQ Topology

The catalogue's coordination patterns map to Cortex's message-driven architecture:

| Orchestration Pattern | Cortex Mapping |
|-------------------|---------------|
| Sequential execution | Chain of messages through agent queues with `ReplyTo` routing |
| Parallel processing | Publish to multiple agent queues simultaneously |
| Pipeline patterns | `ReplyTo` chain: agent.A -> agent.B -> agent.C |
| Map-reduce | Fan-out via multiple publishes, fan-in via a collecting queue |
| Event-driven | RabbitMQ topic exchange with routing keys |
| Hierarchical delegation | `DelegationRecord` with `DelegatedBy`/`DelegatedTo` chain |
| Pub-sub | RabbitMQ fanout/topic exchanges (already supported) |
| Request-reply | `MessageContext.ReplyTo` + `ParentMessageId` (already implemented in `AgentHarness`) |

**Gap: Scatter-gather.** Cortex has no built-in scatter-gather pattern. This would require a correlation mechanism to collect multiple replies into a single result.

**Gap: Consensus mechanisms.** No voting or quorum pattern exists. Would require a new message type or a consensus-tracking service.

**Gap: Barrier coordination / synchronisation points.** No mechanism to wait for N agents to complete before proceeding.

### 10.3 Task Decomposition -> DelegationTracker + AgentOrganizer Role

The agent-organizer role performs task decomposition and team assembly. In Cortex:

- `IDelegationTracker` tracks delegated tasks with `DelegatedBy`, `DelegatedTo`, `Description`, `Status`, `DueAt`
- `DelegationStatus` enum: `Assigned -> InProgress -> AwaitingReview -> Complete -> Overdue`

**Direct mapping:** The decomposition steps (requirement analysis -> subtask identification -> dependency mapping -> resource estimation -> timeline planning -> risk evaluation -> success criteria) could be implemented as a specialised AI agent in Cortex that:
1. Receives a complex message
2. Uses the skill registry to identify required capabilities
3. Queries `IAgentRegistry.FindByCapabilityAsync()` to find capable agents
4. Creates `DelegationRecord` entries for each subtask
5. Publishes messages to agent queues via `IMessageBus`

**Gap: Dependency mapping between subtasks.** `DelegationRecord` tracks individual delegations but not dependencies between them. A dependency graph (DAG) would need to be added.

### 10.4 Agent Selection -> IAgentRegistry.FindByCapabilityAsync

The 8-dimensional capability matching model maps to Cortex:

| Matching Dimension | Cortex Support |
|--------------------|---------------|
| Capability matching | `FindByCapabilityAsync(capabilityName)` -- supported |
| Performance history | Not tracked -- gap |
| Cost considerations | Not tracked -- gap |
| Availability checking | `AgentRegistration.IsAvailable` -- supported |
| Load balancing | Not tracked -- gap (no workload metric) |
| Specialisation mapping | `AgentCapability.Description` -- partial |
| Compatibility verification | Not tracked -- gap |
| Backup selection | Not tracked -- gap |

**Recommendation:** Extend `AgentRegistration` with:
- `CurrentWorkload` (int -- number of in-flight tasks)
- `ModelTier` (enum -- haiku/sonnet/opus equivalent)
- `PerformanceScore` (double -- rolling success rate)
- `CostPerTask` (decimal -- estimated cost)

### 10.5 Communication Protocol -> MessageEnvelope + MessageContext

The catalogue's JSON message format:
```json
{
  "requesting_agent": "<name>",
  "request_type": "<type>",
  "payload": { "query": "<text>" }
}
```

Cortex's existing `MessageEnvelope`:
- `Message` (IMessage with `MessageId`, `CreatedAt`, payload)
- `ReferenceCode` (tracking)
- `Context` (MessageContext with `ReplyTo`, `ParentMessageId`, `FromAgentId`)

**Direct mapping:** The `requesting_agent` field = `Context.FromAgentId`. The `request_type` field could map to a message routing key or a field on the message payload. The pattern is already well-supported.

### 10.6 Error Handling -> Authority Model + Dead Letter Exchange

The error-coordinator patterns map to Cortex:

| Error Pattern | Cortex Mapping |
|-------------------|---------------|
| Circuit breaker | Not implemented -- needs new service |
| Retry with backoff | RabbitMQ TTL + dead letter exchange (infrastructure-level) |
| Dead letter queues | RabbitMQ dead letter exchange (already configured in `RabbitMqMessageBus`) |
| Cascade prevention | Authority model partially prevents (AskMeFirst blocks risky actions) |
| Fallback strategies | Could be implemented via `IAgentRegistry` backup selection |
| Recovery orchestration | Could be a specialised recovery agent |

**Gap: Circuit breaker state per agent.** Cortex has no mechanism to track agent health and automatically stop routing to failing agents.

**Gap: Error taxonomy and classification.** No structured error types in the message model.

### 10.7 Queue Management -> RabbitMQ Topology

The queue management patterns map directly to RabbitMQ:

| Queue Pattern | RabbitMQ Feature |
|-------------------|-----------------|
| Priority levels | RabbitMQ priority queues (x-max-priority) |
| Message ordering | FIFO within single queue |
| TTL handling | Per-message or per-queue TTL |
| Dead letter queues | DLX exchange (configured) |
| Batch processing | Basic.Qos prefetch count |
| Queue monitoring | RabbitMQ management plugin |

**Gap: Priority scheduling in Cortex.** The current `AgentHarness` consumes from `agent.{AgentId}` without priority awareness. Priority queues would need to be configured at the RabbitMQ level and the harness would need to be aware of priority headers.

### 10.8 Load Balancing -> AgentRuntime + Team Support

The distribution strategies map to Cortex's team concept:

- `IAgentRuntime.StartAgentAsync(agent, teamId)` -- agents can be grouped into teams
- `IAgentRuntime.GetTeamAgentIds(teamId)` -- enumerate team members
- `IAgentRuntime.StopTeamAsync(teamId)` -- lifecycle management

**Mapping:** Multiple agents with the same capability in a team can be load-balanced by having them consume from the same RabbitMQ queue (competing consumers pattern). This is already possible with the current architecture -- just start multiple agents consuming from the same queue name.

**Gap: Weighted distribution, affinity routing, capacity-based routing.** These require a routing layer above the raw queue -- a "task router" service that selects which agent queue to publish to based on agent metadata.

### 10.9 Workflow Patterns -> Future Workflow Engine

The workflow-orchestrator role describes patterns not yet in Cortex:

| Pattern | Cortex Status |
|---------|--------------|
| State machines | Not implemented -- no state machine engine |
| Saga patterns | Not implemented -- need compensation tracking |
| Checkpoint/restart | Not implemented -- need persistent workflow state |
| Event sourcing | Not implemented -- but RabbitMQ messages are naturally event-like |
| Human task integration | Partially supported -- IAgent is human-or-AI agnostic |
| Approval workflows | Partially supported via DoItAndShowMe authority tier |
| Escalation | Partially supported via AskMeFirst authority tier |

**Key observation:** Cortex's authority model (JustDoIt / DoItAndShowMe / AskMeFirst) is a **workflow pattern** that maps to the human task integration archetype. The "DoItAndShowMe" tier is essentially an approval workflow, and "AskMeFirst" is an escalation pattern.

### 10.10 Knowledge Management -> Skills Registry + Future Knowledge Service

The knowledge-synthesizer role maps to a potential Cortex capability:

| Knowledge Pattern | Cortex Mapping |
|-------------------|---------------|
| Knowledge graph | Not implemented -- future service |
| Pattern extraction from interactions | Could mine message history |
| Best practice distribution | Could be pushed via message bus |
| Agent performance learning | `IAgentRegistry` extended with performance metrics |

**Recommendation:** A knowledge-synthesizer agent in Cortex could:
1. Subscribe to all message traffic (audit queue)
2. Analyse patterns in delegation success/failure
3. Update agent performance scores in the registry
4. Generate and publish optimisation recommendations

### 10.11 Observability -> Future Monitoring Infrastructure

The performance-monitor role maps to standard observability:

| Observability Pattern | Cortex Mapping |
|-------------------|---------------|
| Distributed tracing | Correlation via `ReferenceCode` + `ParentMessageId` chain |
| Request flow tracking | `MessageContext` already carries trace context |
| SLO management | `DelegationRecord.DueAt` enables deadline tracking |
| Alert management | Not implemented -- needs external integration |
| Dashboard creation | Not implemented -- needs external integration |

**Key insight:** Cortex's `ReferenceCode` (CTX-YYYY-MMDD-NNN format) combined with `ParentMessageId` chaining already provides the foundation for distributed tracing. Every message in a workflow shares a `ReferenceCode` and the `ParentMessageId` chain shows the causal path.

### 10.12 Model Tier Strategy -> Agent Type Selection for Cortex

The model tier strategy provides a template for how Cortex should assign AI model tiers:

| Cortex Agent Role | Recommended Tier | Rationale |
|-------------------|-----------------|-----------|
| Orchestrator / planner agent | Opus equivalent | Needs deep reasoning for decomposition and coordination |
| Workflow / state machine agent | Opus equivalent | Complex state consistency reasoning |
| Domain specialist agents | Sonnet equivalent | Good reasoning within bounded domain |
| Error coordination agent | Sonnet equivalent | Pattern analysis within structured domain |
| Monitoring / metrics agent | Haiku equivalent | High-volume, structured, latency-sensitive |
| Task routing / distribution agent | Haiku equivalent | Algorithmic, high-throughput |
| Simple file/data operations | Haiku equivalent | Minimal reasoning required |

### 10.13 Three-Phase Execution -> AgentHarness Lifecycle

The Analysis -> Implementation -> Excellence pattern maps to a potential enhancement of `AgentHarness`:

1. **Analysis phase:** Agent receives message, queries context (calls `IAgentRegistry`, reads from context store, analyses dependencies)
2. **Implementation phase:** Agent executes work (calls skills, publishes sub-tasks, tracks delegation)
3. **Excellence phase:** Agent validates results, emits completion notification, captures learnings

Currently, `AgentHarness.HandleMessageAsync` is a simple dispatch:
```csharp
var response = await _agent.ProcessAsync(envelope);
```

The three-phase pattern could be implemented as middleware/interceptors around `ProcessAsync`, adding pre-processing (context loading) and post-processing (result validation, metrics emission) phases.

### 10.14 Complete Gap Analysis Summary

| Pattern Area | Cortex Has | Cortex Needs |
|-------------|-----------|-------------|
| Agent registration & discovery | IAgentRegistry, FindByCapabilityAsync | Performance scoring, workload tracking, model tier |
| Message-driven coordination | IMessageBus, RabbitMQ, topic exchange | Scatter-gather, consensus, barrier synchronisation |
| Task delegation | DelegationTracker, DelegationRecord | Dependency DAG between subtasks |
| Error resilience | Dead letter exchange (RabbitMQ) | Circuit breaker, error classification, recovery orchestration |
| Queue management | RabbitMQ queues per agent | Priority queues, intelligent routing |
| Load balancing | Competing consumers (possible) | Weighted routing, affinity, capacity-based selection |
| Workflow state | Authority tiers (partial) | State machine engine, saga coordination, compensation |
| Observability | ReferenceCode tracing, ParentMessageId | Metrics collection, dashboards, SLO tracking, alerts |
| Knowledge management | Skill registry | Knowledge graph, pattern extraction, learning loops |
| Human-AI integration | IAgent (shared interface) | Approval workflows, escalation chains, notification |

---

## Sources

- Community-sourced open-source subagent catalogues (meta-orchestration category)
- Industry analysis of prompt-based agent definition patterns
- Role archetype definitions: agent-installer, agent-organizer, context-manager, error-coordinator, it-ops-orchestrator, knowledge-synthesizer, multi-agent-coordinator, performance-monitor, task-distributor, workflow-orchestrator
