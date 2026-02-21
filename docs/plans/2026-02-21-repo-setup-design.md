# Cortex Repository Setup — Design Document

**Date:** 2026-02-21
**Author:** Daniel Grimes
**Status:** Approved

## Context

Cortex is an open-source, message-driven framework for running a business through a unified system where humans and AI agents collaborate as equals. Built in C# under the `dbhq-uk` organisation.

This design covers the initial repository setup — project structure, foundational contracts, CI/CD, documentation, and open-source infrastructure. The goal is to establish Cortex as a serious, well-structured open-source project that demonstrates both architectural thinking and AI-native development practices.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| License | AGPL-3.0 | Protects managed-service future while keeping it genuinely open source |
| Runtime | .NET 10 LTS | Latest long-term support release, signals currency |
| Scaffolding depth | Skeleton + foundational contracts | Shows architectural thinking, not just boilerplate |
| Credit | Daniel Grimes / dbhq-uk | Personal brand within org context |

## Repository Structure

```
cortex/
├── .github/
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.md
│   │   ├── feature_request.md
│   │   └── config.yml
│   ├── PULL_REQUEST_TEMPLATE.md
│   ├── workflows/
│   │   └── ci.yml
│   ├── FUNDING.yml
│   └── SECURITY.md
├── docs/
│   ├── plans/
│   ├── architecture/
│   │   └── vision.md
│   └── adr/
│       └── 001-message-queue-rabbitmq.md
├── src/
│   ├── Cortex.Core/
│   ├── Cortex.Messaging/
│   ├── Cortex.Agents/
│   ├── Cortex.Skills/
│   └── Cortex.Web/
├── tests/
│   ├── Cortex.Core.Tests/
│   ├── Cortex.Messaging.Tests/
│   └── Cortex.Agents.Tests/
├── skills/
│   └── README.md
├── .editorconfig
├── .gitignore
├── .gitattributes
├── CLAUDE.md
├── CHANGELOG.md
├── CODE_OF_CONDUCT.md
├── CONTRIBUTING.md
├── Directory.Build.props
├── global.json
├── LICENSE
├── README.md
└── Cortex.sln
```

## Foundational Contracts

### Cortex.Core

- `IMessage` — base message contract
- `MessageEnvelope` — wraps any message with routing metadata (reference code, authority claims, context chain, priority)
- `MessagePriority` — enum: Low, Normal, High, Critical
- `MessageContext` — parent message, original goal, team ID, channel
- `AuthorityClaim` — who authorised, what level, what actions permitted
- `AuthorityTier` — enum: JustDoIt, DoItAndShowMe, AskMeFirst
- `IAuthorityProvider` — resolves claims for a given agent/action
- `ReferenceCode` — value object in CTX-YYYY-MMDD-NNN format
- `IReferenceCodeGenerator` — generates unique reference codes
- `IChannel` — channel contract
- `ChannelType` — enum: Default, Named, Direct, Team
- `ITeam` — team lifecycle contract
- `TeamStatus` — enum: Assembling, Active, Dissolving, Complete

### Cortex.Agents

- `IAgent` — core agent contract: Process(MessageEnvelope)
- `AgentCapability` — what an agent can do (maps to skills)
- `AgentRegistration` — agent identity + capabilities for registry
- `IAgentRegistry` — find agents by capability
- `DelegationRecord` — track what's delegated to whom, status
- `DelegationStatus` — enum: Assigned, InProgress, AwaitingReview, Complete, Overdue
- `IDelegationTracker` — query and manage delegations

### Cortex.Messaging

- `IMessageBus` — publish/subscribe abstraction
- `IMessageConsumer` — consume from a queue
- `IMessagePublisher` — publish to a queue
- `QueueBinding` — maps queue name to routing rules
- `QueueTopology` — defines the org-chart queue structure

### Cortex.Skills

- `ISkill` — skill contract: metadata + execution
- `SkillDefinition` — parsed from markdown: name, triggers, executor type
- `SkillCategory` — enum: Integration, Knowledge, Agent, Organisational, Meta
- `ISkillRegistry` — discover and retrieve skills
- `ISkillExecutor` — execute a skill (C#, Python, CLI — polymorphic)

## What Is Excluded

- No RabbitMQ implementation (just the abstraction)
- No web UI code (just the project shell)
- No skill markdown files beyond the README placeholder
- No Docker/containerisation
- No sample applications

## Next Steps

After this repo setup is complete, Phase 1 development begins with the message bus implementation and CoS agent.
