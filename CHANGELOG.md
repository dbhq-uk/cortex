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
