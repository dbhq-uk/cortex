# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial project structure with .NET 10 solution
- Core message contracts: IMessage, MessageEnvelope, MessageContext, MessagePriority
- Authority model: AuthorityClaim, AuthorityTier, IAuthorityProvider
- Reference code system: ReferenceCode value object with CTX-YYYY-MMDD-NNN format
- Channel contracts: IChannel, ChannelType
- Team contracts: ITeam, TeamStatus
- Message bus abstraction: IMessageBus, IMessagePublisher, IMessageConsumer
- Queue topology: QueueBinding, QueueTopology
- Agent contracts: IAgent, AgentCapability, AgentRegistration, IAgentRegistry
- Delegation tracking: DelegationRecord, DelegationStatus, IDelegationTracker
- Skill system contracts: ISkill, SkillDefinition, ISkillRegistry, ISkillExecutor
- CI pipeline with GitHub Actions
- Project documentation: README, CONTRIBUTING, CODE_OF_CONDUCT, SECURITY
- Architecture Decision Record: ADR-001 RabbitMQ as message transport
- Vision document with full system design
- **RabbitMQ message bus implementation** (Cortex.Messaging.RabbitMQ)
  - RabbitMqMessageBus: full IMessageBus implementation with topic exchange routing
  - RabbitMqConnection: connection lifecycle management with auto-recovery
  - RabbitMqOptions: configuration (host, port, credentials, exchange names)
  - MessageSerializer: JSON serialisation with type header for polymorphic IMessage
  - ServiceCollectionExtensions: `AddRabbitMqMessaging()` DI registration
  - Dead letter exchange (fanout) for failed/undeliverable messages
- **InMemoryMessageBus** for unit testing and local development
- Docker Compose configuration for local RabbitMQ development
- RabbitMQ service container in CI pipeline for integration tests
- Integration tests for RabbitMQ message bus (round-trip, type headers, dead letter, routing)
- **Agent runtime harness** (Cortex.Agents)
  - AgentHarness: connects IAgent to message queue with dispatch, reply routing, and FromAgentId stamping
  - AgentRuntime: IHostedService + IAgentRuntime for static, dynamic, and team-scoped agent management
  - IAgentRuntime: injectable interface for dynamic agent creation/destruction with team operations
  - InMemoryAgentRegistry: thread-safe IAgentRegistry implementation
  - InMemoryDelegationTracker: thread-safe IDelegationTracker implementation
  - ServiceCollectionExtensions: `AddCortexAgentRuntime()` DI registration
- ReplyTo and FromAgentId fields on MessageContext for request/reply patterns and sender identity
- Per-consumer lifecycle on IMessageConsumer via IAsyncDisposable handles (breaking change)
