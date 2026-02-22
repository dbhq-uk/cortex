# RabbitMQ Message Bus Implementation — Design Document

**Date:** 2026-02-22
**Author:** Daniel Grimes
**Status:** Approved
**Issue:** [#1](https://github.com/dbhq-uk/cortex/issues/1)

## Context

Everything in Cortex communicates through message queues. The abstractions exist in `Cortex.Messaging` (`IMessageBus`, `IMessagePublisher`, `IMessageConsumer`). This design covers the concrete RabbitMQ implementation.

See [ADR-001](../adr/001-message-queue-rabbitmq.md) for the transport decision rationale.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Serialisation | System.Text.Json with type in RabbitMQ header | Clean JSON bodies; type info lives in transport metadata where it belongs. Future Python consumers can ignore and use their own mapping. |
| Exchange model | Single topic exchange + dead letter fanout | Topic exchange maps naturally to org-chart routing. Dead letter fanout catches all failures. |
| Connection management | RabbitMQ.Client v7 built-in auto-recovery | Don't reimplement what the client already does well. |
| Error handling | Nack to dead letter, no retry in the bus | Bus is a dumb pipe. Retry intelligence lives in the agent harness. |
| Dev environment | Docker Compose for local dev, existing RabbitMQ in prod | Contributors can spin up with `docker compose up`. |
| CI | RabbitMQ service container in GitHub Actions | Integration tests validate the real thing. |

## Project Structure

### New project: `Cortex.Messaging.RabbitMQ`

```
src/
  Cortex.Messaging.RabbitMQ/
    RabbitMqMessageBus.cs          # IMessageBus implementation
    RabbitMqConnection.cs          # Connection lifecycle, auto-recovery
    MessageSerializer.cs           # JSON serialisation + type header mapping
    ServiceCollectionExtensions.cs # DI registration
    RabbitMqOptions.cs             # Connection config
tests/
  Cortex.Messaging.RabbitMQ.Tests/ # Integration tests (require running RabbitMQ)
```

### Dependencies

- **RabbitMQ.Client** (v7.x) — official .NET client, async-native
- **System.Text.Json** — built-in, no extra package
- **Microsoft.Extensions.Options** — `IOptions<RabbitMqOptions>` pattern
- **Microsoft.Extensions.DependencyInjection.Abstractions** — DI extension method
- **Microsoft.Extensions.Logging.Abstractions** — structured logging

### What this project does NOT contain

- No retry policies (agent harness's responsibility)
- No message routing logic (that's `QueueTopology` in `Cortex.Messaging`)
- No business logic

## RabbitMQ Topology

### Exchanges

| Exchange | Type | Durable | Purpose |
|----------|------|---------|---------|
| `cortex.messages` | topic | yes | All message routing |
| `cortex.deadletter` | fanout | yes | Failed/expired messages |

### Routing

Messages are published to `cortex.messages` with routing key `queue.<queueName>`. Queues bind to the exchange with matching routing key patterns.

```
cortex.messages (topic exchange)
    ├── routing key: "queue.founder"        → founder inbox queue
    ├── routing key: "queue.cos"            → CoS inbox queue
    ├── routing key: "queue.specialist.*"   → specialist queues
    ├── routing key: "queue.team.*"         → team queues
    └── routing key: "queue.<name>"         → any named queue

cortex.deadletter (fanout exchange)
    └── cortex.deadletter.queue             → single dead letter queue
```

### Queue Properties

All queues are declared:
- **Durable** — survive broker restart
- **Not exclusive** — multiple processes can connect
- **Not auto-delete** — persist when no consumers connected (on-demand compute support)
- **x-dead-letter-exchange** set to `cortex.deadletter`

## Serialisation

### Wire format

- **Body:** JSON-serialised `MessageEnvelope` via System.Text.Json
- **Header `cortex-message-type`:** Assembly-qualified type name of the concrete `IMessage` implementation
- **Content type:** `application/json`
- **Delivery mode:** 2 (persistent)

### Deserialisation flow

1. Read `cortex-message-type` header
2. Resolve CLR type via `Type.GetType()`
3. Deserialise JSON body with the resolved type for the `Message` property
4. If type resolution fails, nack to dead letter

## Configuration

```csharp
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "cortex.messages";
    public string DeadLetterExchangeName { get; set; } = "cortex.deadletter";
    public bool AutoRecoveryEnabled { get; set; } = true;
}
```

Configured via `appsettings.json` section bound to `IOptions<RabbitMqOptions>`.

## Connection Management

- Single `RabbitMqConnection` wrapping one `IConnection`
- Auto-recovery enabled (built into RabbitMQ.Client v7)
- One AMQP channel for publishing, one per consumer
- Implements `IAsyncDisposable` for clean shutdown

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| Handler succeeds | Message acked |
| Handler throws | Message nacked, routes to dead letter queue |
| Broker disconnects | RabbitMQ.Client auto-recovery reconnects |
| Serialisation fails on consume | Message nacked (dead letter), error logged |
| Publish fails | Exception propagates to caller |

No retry logic in the bus. The bus delivers or dead-letters. Retry intelligence lives in the agent harness.

## DI Registration

```csharp
services.AddRabbitMqMessaging(options => {
    options.HostName = "localhost";
    options.UserName = "cortex";
    options.Password = "cortex";
});
```

Registers `RabbitMqMessageBus` as singleton for `IMessageBus`, `IMessagePublisher`, and `IMessageConsumer`.

## Docker Compose (Local Dev)

```yaml
services:
  rabbitmq:
    image: rabbitmq:4-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: cortex
      RABBITMQ_DEFAULT_PASS: cortex
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq

volumes:
  rabbitmq_data:
```

Management UI at `http://localhost:15672` (cortex/cortex).

## CI Pipeline Update

Add RabbitMQ as a service container in GitHub Actions:

```yaml
services:
  rabbitmq:
    image: rabbitmq:4-management-alpine
    ports:
      - 5672:5672
    env:
      RABBITMQ_DEFAULT_USER: cortex
      RABBITMQ_DEFAULT_PASS: cortex
```

## Integration Tests

- Publish and consume round-trip — verify contents match
- Type header serialisation — verify concrete IMessage type survives round-trip
- Dead letter on handler failure — handler throws, verify message in dead letter queue
- Multiple consumers on different queues — verify correct routing
- Connection recovery — manual testing only (too fragile for CI)
