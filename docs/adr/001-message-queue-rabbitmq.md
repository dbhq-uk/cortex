# ADR-001: RabbitMQ as Message Transport

**Status:** Accepted
**Date:** 2026-02-21
**Author:** Daniel Grimes

## Context

Cortex requires a durable message transport layer where all system communication flows through queues. The transport must support:

- Message persistence across restarts
- Queue-per-agent topology mapping to the organisational chart
- Dead letter queues for failure handling and escalation
- Multiple consumers with different processing patterns
- Future support for on-demand compute (messages wait in queue if consumers are offline)

## Options Considered

### RabbitMQ
- Mature, battle-tested AMQP broker
- Built-in message persistence and durability
- Flexible routing with exchanges and bindings
- Dead letter exchange support
- Rich .NET client library
- Self-hosted, no vendor lock-in

### Apache Kafka
- High throughput event streaming
- Excellent for event sourcing patterns
- More complex operational model
- Overkill for message routing / task coordination
- Consumer group model doesn't map well to per-agent queues

### Azure Service Bus / AWS SQS
- Managed service, less operational overhead
- Vendor lock-in
- Cost scales with message volume
- Less flexibility in routing topology

### In-process Channel (System.Threading.Channels)
- Zero infrastructure
- No persistence across restarts
- No distributed capability
- Good for prototyping, not production

## Decision

**Use RabbitMQ** as the primary message transport.

## Consequences

### Positive

- Messages persist and survive restarts -- no lost work
- Queue topology maps naturally to the organisational chart (founder queue, CoS queue, specialist queues, team queues)
- Dead letter queues handle failures and enable escalation patterns
- Messages wait for consumers, enabling future on-demand compute (VM can be off, messages queue up)
- Self-hosted on the VM alongside agent execution
- Well-supported .NET client (RabbitMQ.Client)
- Can be replaced later if needed -- the IMessageBus abstraction isolates the framework from the transport

### Negative

- Requires a running RabbitMQ instance (operational overhead)
- Additional infrastructure component to maintain
- Need to handle connection management and reconnection in the client

### Mitigations

- RabbitMQ runs on the same VM as agent execution, simplifying operations
- The IMessageBus abstraction means the transport can be swapped without changing consuming code
- Connection management will be handled in a dedicated Cortex.Messaging.RabbitMQ project (Phase 1)
