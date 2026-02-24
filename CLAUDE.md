# CLAUDE.md

This file provides context for AI-assisted development on the Cortex project.

## Project Overview

Cortex is a message-driven framework for running a business through a unified system where humans and AI agents collaborate. Built in C# targeting .NET 10.

## Architecture

- **Message-driven**: All communication flows through durable message queues (RabbitMQ)
- **Authority model**: Three tiers (JustDoIt, DoItAndShowMe, AskMeFirst) as claims on messages
- **Skill-based**: Every capability is a skill (markdown file wrapping C#, Python, CLI, or API)
- **Agent-agnostic**: Human and AI agents implement the same IAgent interface

## Project Structure

```
src/
  Cortex.Core/              - Message contracts, authority model, reference codes, channels, teams
  Cortex.Messaging/         - Message bus abstraction (IMessageBus, queue topology, InMemoryMessageBus)
  Cortex.Messaging.RabbitMQ/ - RabbitMQ message bus implementation
  Cortex.Agents/            - Agent contracts (IAgent), harness, runtime, delegation tracking
  Cortex.Skills/            - Skill registry, execution contracts
  Cortex.Web/               - Web UI (ASP.NET)
tests/
  Cortex.Core.Tests/
  Cortex.Messaging.Tests/
  Cortex.Messaging.RabbitMQ.Tests/  - Integration tests (require RabbitMQ)
  Cortex.Agents.Tests/
skills/                     - Skill definition files (markdown)
docs/
  architecture/             - Vision document
  adr/                      - Architecture Decision Records
  plans/                    - Design and implementation plans
```

## Build Commands

```bash
# Restore dependencies
dotnet restore

# Build (Release)
dotnet build --configuration Release

# Run unit tests only (no external dependencies)
dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"

# Run all tests including integration tests (requires RabbitMQ)
docker compose up -d
dotnet test --configuration Release --verbosity normal

# Build specific project
dotnet build src/Cortex.Core/Cortex.Core.csproj
```

## Key Namespaces

| Namespace                    | Purpose                                     |
|------------------------------|---------------------------------------------|
| `Cortex.Core.Messages`       | IMessage, MessageEnvelope, MessageContext    |
| `Cortex.Core.Authority`      | AuthorityClaim, AuthorityTier, IAuthorityProvider |
| `Cortex.Core.References`     | ReferenceCode, IReferenceCodeGenerator, SequentialReferenceCodeGenerator, ISequenceStore, FileSequenceStore |
| `Cortex.Core.Channels`       | IChannel, ChannelType                       |
| `Cortex.Core.Teams`          | ITeam, TeamStatus                           |
| `Cortex.Messaging`           | IMessageBus, IMessagePublisher, IMessageConsumer, InMemoryMessageBus |
| `Cortex.Messaging.RabbitMQ`  | RabbitMqMessageBus, RabbitMqConnection, RabbitMqOptions, MessageSerializer |
| `Cortex.Agents`              | IAgent, IAgentRegistry, AgentHarness, AgentRuntime, IAgentRuntime |
| `Cortex.Agents.Delegation`   | DelegationRecord, IDelegationTracker, InMemoryDelegationTracker |
| `Cortex.Skills`              | ISkill, ISkillRegistry, ISkillExecutor      |

## Design Decisions

- **Interfaces over implementations**: Consumers depend on contracts, not concrete types
- **Records for data**: Immutable record types for messages, claims, and registrations
- **Value objects**: ReferenceCode is a readonly record struct with validation
- **Directory.Build.props**: Shared build settings (nullable, warnings as errors, .NET 10)
- **RabbitMQ message bus**: Topic exchange routing, dead letter fanout, JSON serialisation with type headers
- **InMemoryMessageBus**: For unit testing and local development without infrastructure
- **Docker Compose**: Local RabbitMQ for integration testing (`docker compose up -d`)
- **Agent runtime**: AgentHarness per agent, AgentRuntime as IHostedService, dynamic agent creation via IAgentRuntime with team support
- **Per-consumer lifecycle**: IMessageConsumer.StartConsumingAsync returns IAsyncDisposable for independent consumer stop

## Coding Conventions

- File-scoped namespaces
- `var` when type is apparent
- XML documentation on all public members
- Private fields: `_camelCase`
- Public members: PascalCase
- Async methods: suffix with `Async`
- CancellationToken on all async interfaces
- `required` keyword for mandatory init properties
- Collection expressions `[]` for empty collections

## Reference Code Format

`CTX-YYYY-MMDD-NNN(N)` (e.g. `CTX-2026-0221-001`, overflow: `CTX-2026-0221-1000`)

## Authority Tiers

1. **JustDoIt** -- internal, no external footprint (log, update, file)
2. **DoItAndShowMe** -- prepare and present for approval (draft email, create plan)
3. **AskMeFirst** -- novel, high-stakes, or uncertain (send email, publish, spend money)
