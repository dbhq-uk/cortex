# RabbitMQ Message Bus Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement `InMemoryMessageBus` (for unit testing and local dev) and `RabbitMqMessageBus` (production transport) — both implementing the existing `IMessageBus` interface from `Cortex.Messaging`.

**Architecture:** Two-layer approach. First, build an `InMemoryMessageBus` inside `Cortex.Messaging` that validates the abstraction and enables fast unit testing. Then build `Cortex.Messaging.RabbitMQ` as a separate project with the real RabbitMQ transport, tested via CI service container. Both implement the same `IMessageBus`, `IMessagePublisher`, `IMessageConsumer` contracts.

**Tech Stack:** .NET 10, System.Text.Json, System.Threading.Channels, RabbitMQ.Client 7.2.0, Microsoft.Extensions.Options, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, xUnit, Docker Compose, GitHub Actions service containers.

**Design Doc:** `docs/plans/2026-02-22-rabbitmq-message-bus-design.md`
**Issue:** [#1](https://github.com/dbhq-uk/cortex/issues/1)

---

## Task 1: Test Message Type

A concrete `IMessage` implementation needed for all tests. Lives in the messaging test project.

**Files:**
- Create: `tests/Cortex.Messaging.Tests/TestMessage.cs`

**Step 1: Create TestMessage**

```csharp
using Cortex.Core.Messages;

namespace Cortex.Messaging.Tests;

/// <summary>
/// Simple concrete IMessage for unit testing.
/// </summary>
public sealed record TestMessage : IMessage
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Test payload content.
    /// </summary>
    public required string Content { get; init; }
}
```

**Step 2: Build to verify it compiles**

Run: `dotnet build tests/Cortex.Messaging.Tests/Cortex.Messaging.Tests.csproj`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add tests/Cortex.Messaging.Tests/TestMessage.cs
git commit -m "test: add TestMessage for messaging unit tests"
```

---

## Task 2: InMemoryMessageBus — Publish and Consume Tests

Write the failing tests that define the core publish/consume contract.

**Files:**
- Create: `tests/Cortex.Messaging.Tests/InMemoryMessageBusTests.cs`

**Step 1: Write failing tests**

```csharp
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Messaging.Tests;

public sealed class InMemoryMessageBusTests
{
    private static MessageEnvelope CreateEnvelope(string content) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    [Fact]
    public async Task PublishAsync_AndConsume_DeliversMessage()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var received = new TaskCompletionSource<MessageEnvelope>();

        await bus.StartConsumingAsync("test-queue", envelope =>
        {
            received.SetResult(envelope);
            return Task.CompletedTask;
        });

        var sent = CreateEnvelope("hello");

        // Act
        await bus.PublishAsync(sent, "test-queue");

        // Assert
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("hello", ((TestMessage)result.Message).Content);
        Assert.Equal(sent.ReferenceCode, result.ReferenceCode);
    }

    [Fact]
    public async Task PublishAsync_ToQueueWithNoConsumer_MessageWaitsForConsumer()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var sent = CreateEnvelope("waiting");

        // Act — publish before any consumer is registered
        await bus.PublishAsync(sent, "lazy-queue");

        // Now start consuming
        var received = new TaskCompletionSource<MessageEnvelope>();
        await bus.StartConsumingAsync("lazy-queue", envelope =>
        {
            received.SetResult(envelope);
            return Task.CompletedTask;
        });

        // Assert
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("waiting", ((TestMessage)result.Message).Content);
    }

    [Fact]
    public async Task PublishAsync_MultipleQueues_RoutesCorrectly()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var receivedA = new TaskCompletionSource<MessageEnvelope>();
        var receivedB = new TaskCompletionSource<MessageEnvelope>();

        await bus.StartConsumingAsync("queue-a", envelope =>
        {
            receivedA.SetResult(envelope);
            return Task.CompletedTask;
        });
        await bus.StartConsumingAsync("queue-b", envelope =>
        {
            receivedB.SetResult(envelope);
            return Task.CompletedTask;
        });

        // Act
        await bus.PublishAsync(CreateEnvelope("for-a"), "queue-a");
        await bus.PublishAsync(CreateEnvelope("for-b"), "queue-b");

        // Assert
        var resultA = await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var resultB = await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("for-a", ((TestMessage)resultA.Message).Content);
        Assert.Equal("for-b", ((TestMessage)resultB.Message).Content);
    }

    [Fact]
    public async Task StopConsumingAsync_StopsDelivery()
    {
        // Arrange
        var bus = new InMemoryMessageBus();
        var callCount = 0;

        await bus.StartConsumingAsync("stop-queue", _ =>
        {
            Interlocked.Increment(ref callCount);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(CreateEnvelope("first"), "stop-queue");

        // Wait for delivery
        await Task.Delay(100);

        // Act
        await bus.StopConsumingAsync();

        await bus.PublishAsync(CreateEnvelope("second"), "stop-queue");
        await Task.Delay(100);

        // Assert — only the first message was delivered
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetTopologyAsync_ReturnsEmptyByDefault()
    {
        // Arrange
        var bus = new InMemoryMessageBus();

        // Act
        var topology = await bus.GetTopologyAsync();

        // Assert
        Assert.NotNull(topology);
        Assert.Empty(topology.Bindings);
    }

    [Fact]
    public async Task PublishAsync_NullEnvelope_ThrowsArgumentNullException()
    {
        var bus = new InMemoryMessageBus();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => bus.PublishAsync(null!, "queue"));
    }

    [Fact]
    public async Task PublishAsync_NullQueueName_ThrowsArgumentException()
    {
        var bus = new InMemoryMessageBus();
        await Assert.ThrowsAsync<ArgumentException>(
            () => bus.PublishAsync(CreateEnvelope("test"), null!));
    }

    [Fact]
    public async Task StartConsumingAsync_NullQueueName_ThrowsArgumentException()
    {
        var bus = new InMemoryMessageBus();
        await Assert.ThrowsAsync<ArgumentException>(
            () => bus.StartConsumingAsync(null!, _ => Task.CompletedTask));
    }

    [Fact]
    public async Task StartConsumingAsync_NullHandler_ThrowsArgumentNullException()
    {
        var bus = new InMemoryMessageBus();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => bus.StartConsumingAsync("queue", null!));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Messaging.Tests/ --verbosity normal`
Expected: FAIL — `InMemoryMessageBus` does not exist yet.

**Step 3: Commit failing tests**

```bash
git add tests/Cortex.Messaging.Tests/InMemoryMessageBusTests.cs
git commit -m "test: add InMemoryMessageBus unit tests (red)"
```

---

## Task 3: InMemoryMessageBus — Implementation

Implement the in-memory message bus using `System.Threading.Channels`.

**Files:**
- Create: `src/Cortex.Messaging/InMemoryMessageBus.cs`

**Step 1: Implement InMemoryMessageBus**

```csharp
using System.Threading.Channels;
using Cortex.Core.Messages;

namespace Cortex.Messaging;

/// <summary>
/// In-memory message bus for unit testing, local development, and prototyping.
/// Uses System.Threading.Channels for async queue semantics.
/// </summary>
public sealed class InMemoryMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Channel<MessageEnvelope>> _queues = new();
    private readonly List<CancellationTokenSource> _consumers = [];
    private readonly object _consumersLock = new();

    /// <inheritdoc />
    public Task PublishAsync(
        MessageEnvelope envelope,
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var channel = _queues.GetOrAdd(queueName, _ =>
            Channel.CreateUnbounded<MessageEnvelope>());

        return channel.Writer.WriteAsync(envelope, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public Task StartConsumingAsync(
        string queueName,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(handler);

        var channel = _queues.GetOrAdd(queueName, _ =>
            Channel.CreateUnbounded<MessageEnvelope>());

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_consumersLock)
        {
            _consumers.Add(cts);
        }

        _ = ConsumeLoopAsync(channel.Reader, handler, cts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopConsumingAsync(CancellationToken cancellationToken = default)
    {
        lock (_consumersLock)
        {
            foreach (var cts in _consumers)
            {
                cts.Cancel();
                cts.Dispose();
            }

            _consumers.Clear();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<QueueTopology> GetTopologyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new QueueTopology());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopConsumingAsync();

        foreach (var channel in _queues.Values)
        {
            channel.Writer.TryComplete();
        }

        _queues.Clear();
    }

    private static async Task ConsumeLoopAsync(
        ChannelReader<MessageEnvelope> reader,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var envelope in reader.ReadAllAsync(cancellationToken))
            {
                await handler(envelope);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — consumer was stopped.
        }
    }
}
```

**Step 2: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Messaging.Tests/ --verbosity normal`
Expected: All 9 tests PASS.

**Step 3: Run full solution build and tests**

Run: `dotnet build --configuration Release && dotnet test --configuration Release --verbosity normal`
Expected: Build succeeded. All tests pass (including Cortex.Core.Tests).

**Step 4: Commit**

```bash
git add src/Cortex.Messaging/InMemoryMessageBus.cs
git commit -m "feat: add InMemoryMessageBus for testing and local dev"
```

---

## Task 4: MessageSerializer

JSON serialisation with type header mapping, used by the RabbitMQ transport.

**Files:**
- Create: `src/Cortex.Messaging.RabbitMQ/Cortex.Messaging.RabbitMQ.csproj`
- Create: `src/Cortex.Messaging.RabbitMQ/MessageSerializer.cs`
- Create: `tests/Cortex.Messaging.RabbitMQ.Tests/Cortex.Messaging.RabbitMQ.Tests.csproj`
- Create: `tests/Cortex.Messaging.RabbitMQ.Tests/TestMessage.cs`
- Create: `tests/Cortex.Messaging.RabbitMQ.Tests/MessageSerializerTests.cs`

**Step 1: Create the project file**

`src/Cortex.Messaging.RabbitMQ/Cortex.Messaging.RabbitMQ.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <PackageReference Include="RabbitMQ.Client" Version="7.2.0" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="10.0.0-preview.1.25080.5" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0-preview.1.25080.5" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0-preview.1.25080.5" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Cortex.Messaging\Cortex.Messaging.csproj" />
  </ItemGroup>

</Project>
```

> **Note to implementer:** The `Microsoft.Extensions.*` package versions must match what's available for .NET 10. Run `dotnet restore` and check for the latest compatible preview versions. If `10.0.0-preview.1.25080.5` isn't found, use the latest `10.0.0-preview.*` or fall back to `9.0.0` which is forward-compatible. Similarly, verify `RabbitMQ.Client` `7.2.0` is available; use the latest `7.x` if not.

**Step 2: Create the test project file**

`tests/Cortex.Messaging.RabbitMQ.Tests/Cortex.Messaging.RabbitMQ.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Cortex.Messaging.RabbitMQ\Cortex.Messaging.RabbitMQ.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

**Step 3: Add both projects to the solution**

```bash
dotnet sln Cortex.slnx add src/Cortex.Messaging.RabbitMQ/Cortex.Messaging.RabbitMQ.csproj --solution-folder src
dotnet sln Cortex.slnx add tests/Cortex.Messaging.RabbitMQ.Tests/Cortex.Messaging.RabbitMQ.Tests.csproj --solution-folder tests
```

**Step 4: Create TestMessage for RabbitMQ tests**

`tests/Cortex.Messaging.RabbitMQ.Tests/TestMessage.cs`:

```csharp
using Cortex.Core.Messages;

namespace Cortex.Messaging.RabbitMQ.Tests;

/// <summary>
/// Simple concrete IMessage for RabbitMQ integration testing.
/// </summary>
public sealed record TestMessage : IMessage
{
    public string MessageId { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Test payload content.
    /// </summary>
    public required string Content { get; init; }
}
```

**Step 5: Write failing serialiser tests**

`tests/Cortex.Messaging.RabbitMQ.Tests/MessageSerializerTests.cs`:

```csharp
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Messaging.RabbitMQ.Tests;

public sealed class MessageSerializerTests
{
    private static MessageEnvelope CreateEnvelope(string content) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    [Fact]
    public void Serialize_ProducesValidJsonBody()
    {
        var envelope = CreateEnvelope("hello");

        var (body, messageType) = MessageSerializer.Serialize(envelope);

        Assert.NotNull(body);
        Assert.True(body.Length > 0);
    }

    [Fact]
    public void Serialize_ReturnsAssemblyQualifiedTypeName()
    {
        var envelope = CreateEnvelope("hello");

        var (_, messageType) = MessageSerializer.Serialize(envelope);

        Assert.Contains("TestMessage", messageType);
        Assert.Contains(",", messageType); // Assembly-qualified
    }

    [Fact]
    public void RoundTrip_PreservesMessageContent()
    {
        var original = CreateEnvelope("round-trip");

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.NotNull(deserialized);
        var message = Assert.IsType<TestMessage>(deserialized.Message);
        Assert.Equal("round-trip", message.Content);
    }

    [Fact]
    public void RoundTrip_PreservesReferenceCode()
    {
        var original = CreateEnvelope("ref-test");

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.Equal(original.ReferenceCode, deserialized!.ReferenceCode);
    }

    [Fact]
    public void RoundTrip_PreservesPriority()
    {
        var original = CreateEnvelope("priority") with { Priority = MessagePriority.Critical };

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.Equal(MessagePriority.Critical, deserialized!.Priority);
    }

    [Fact]
    public void RoundTrip_PreservesContext()
    {
        var original = CreateEnvelope("context") with
        {
            Context = new MessageContext
            {
                OriginalGoal = "test goal",
                TeamId = "team-1",
                ChannelId = "channel-1"
            }
        };

        var (body, messageType) = MessageSerializer.Serialize(original);
        var deserialized = MessageSerializer.Deserialize(body, messageType);

        Assert.Equal("test goal", deserialized!.Context.OriginalGoal);
        Assert.Equal("team-1", deserialized.Context.TeamId);
        Assert.Equal("channel-1", deserialized.Context.ChannelId);
    }

    [Fact]
    public void Deserialize_InvalidType_ReturnsNull()
    {
        var (body, _) = MessageSerializer.Serialize(CreateEnvelope("bad-type"));

        var result = MessageSerializer.Deserialize(body, "NonExistent.Type, NoAssembly");

        Assert.Null(result);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsNull()
    {
        var badBody = System.Text.Encoding.UTF8.GetBytes("{ not valid json }}}");

        var result = MessageSerializer.Deserialize(
            badBody,
            typeof(TestMessage).AssemblyQualifiedName!);

        Assert.Null(result);
    }

    [Fact]
    public void Serialize_NullEnvelope_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MessageSerializer.Serialize(null!));
    }
}
```

**Step 6: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Messaging.RabbitMQ.Tests/ --verbosity normal`
Expected: FAIL — `MessageSerializer` does not exist.

**Step 7: Commit failing tests**

```bash
git add src/Cortex.Messaging.RabbitMQ/ tests/Cortex.Messaging.RabbitMQ.Tests/ Cortex.slnx
git commit -m "test: add MessageSerializer tests and RabbitMQ project scaffolding (red)"
```

---

## Task 5: MessageSerializer — Implementation

Implement JSON serialisation with type header support as defined in the design doc.

**Files:**
- Create: `src/Cortex.Messaging.RabbitMQ/MessageSerializer.cs`

**Step 1: Implement MessageSerializer**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.Core.Messages;

namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// Serialises and deserialises <see cref="MessageEnvelope"/> to/from JSON bytes.
/// The concrete IMessage type name is returned separately for placement in a transport header.
/// </summary>
public static class MessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serialises an envelope to a JSON byte array and extracts the message type name.
    /// </summary>
    /// <returns>Tuple of (json bytes, assembly-qualified type name of the IMessage).</returns>
    public static (byte[] Body, string MessageType) Serialize(MessageEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var messageType = envelope.Message.GetType().AssemblyQualifiedName
            ?? throw new InvalidOperationException(
                "Could not resolve assembly-qualified name for message type.");

        // Serialise the envelope with the concrete message type
        var json = JsonSerializer.SerializeToUtf8Bytes(envelope, CreateOptionsForType(envelope.Message.GetType()));

        return (json, messageType);
    }

    /// <summary>
    /// Deserialises a JSON byte array back into a MessageEnvelope, using the type header
    /// to resolve the concrete IMessage type.
    /// </summary>
    /// <returns>The deserialised envelope, or null if deserialisation fails.</returns>
    public static MessageEnvelope? Deserialize(ReadOnlySpan<byte> body, string messageTypeName)
    {
        var messageType = Type.GetType(messageTypeName);
        if (messageType is null)
        {
            return null;
        }

        try
        {
            var envelopeType = typeof(TypedEnvelope<>).MakeGenericType(messageType);
            var typed = JsonSerializer.Deserialize(body, envelopeType, CreateOptionsForType(messageType));

            if (typed is IEnvelopeAccessor accessor)
            {
                return accessor.ToEnvelope();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonSerializerOptions CreateOptionsForType(Type messageType)
    {
        // Clone base options and add polymorphic handling
        var options = new JsonSerializerOptions(Options);
        options.Converters.Add(
            (JsonConverter)Activator.CreateInstance(
                typeof(MessageConverter<>).MakeGenericType(messageType))!);
        return options;
    }

    /// <summary>
    /// Internal interface for generic envelope accessor.
    /// </summary>
    private interface IEnvelopeAccessor
    {
        MessageEnvelope ToEnvelope();
    }

    /// <summary>
    /// Strongly-typed envelope for deserialisation with a known message type.
    /// </summary>
    private sealed record TypedEnvelope<TMessage> : IEnvelopeAccessor
        where TMessage : IMessage
    {
        public required TMessage Message { get; init; }
        public required string ReferenceCode { get; init; }
        public IReadOnlyList<AuthorityClaimDto>? AuthorityClaims { get; init; }
        public MessageContext Context { get; init; } = new();
        public MessagePriority Priority { get; init; } = MessagePriority.Normal;
        public TimeSpan? Sla { get; init; }

        public MessageEnvelope ToEnvelope() => new()
        {
            Message = Message,
            ReferenceCode = new Core.References.ReferenceCode(ReferenceCode),
            Context = Context,
            Priority = Priority,
            Sla = Sla
        };
    }

    /// <summary>
    /// DTO for authority claim serialisation.
    /// </summary>
    private sealed record AuthorityClaimDto
    {
        public string GrantedBy { get; init; } = "";
        public string GrantedTo { get; init; } = "";
        public string Tier { get; init; } = "";
        public IReadOnlyList<string> PermittedActions { get; init; } = [];
        public DateTimeOffset GrantedAt { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
    }

    /// <summary>
    /// Custom converter that serialises/deserialises IMessage as the concrete type.
    /// </summary>
    private sealed class MessageConverter<TMessage> : JsonConverter<IMessage>
        where TMessage : IMessage
    {
        public override IMessage? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // Remove this converter to avoid recursion, deserialise as concrete type
            var cleanOptions = new JsonSerializerOptions(options);
            cleanOptions.Converters.Clear();
            foreach (var converter in options.Converters)
            {
                if (converter is not MessageConverter<TMessage>)
                {
                    cleanOptions.Converters.Add(converter);
                }
            }

            return JsonSerializer.Deserialize<TMessage>(ref reader, cleanOptions);
        }

        public override void Write(
            Utf8JsonWriter writer,
            IMessage value,
            JsonSerializerOptions options)
        {
            var cleanOptions = new JsonSerializerOptions(options);
            cleanOptions.Converters.Clear();
            foreach (var converter in options.Converters)
            {
                if (converter is not MessageConverter<TMessage>)
                {
                    cleanOptions.Converters.Add(converter);
                }
            }

            JsonSerializer.Serialize(writer, (TMessage)value, cleanOptions);
        }
    }
}
```

> **Note to implementer:** This serialiser uses a generic `TypedEnvelope<TMessage>` to work around System.Text.Json's lack of polymorphic deserialisation for the `IMessage` property. The `MessageConverter<T>` allows the `IMessage` interface to serialise/deserialise using the concrete type resolved from the `cortex-message-type` header. If the approach above causes issues with complex nested types, simplify by serialising the `MessageEnvelope` body and `IMessage` payload as two separate JSON documents (header + body pattern). But try this first — it's cleaner.

**Step 2: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Messaging.RabbitMQ.Tests/ --verbosity normal`
Expected: All 9 serialiser tests PASS.

**Step 3: Run full test suite**

Run: `dotnet test --configuration Release --verbosity normal`
Expected: All tests pass across all projects.

**Step 4: Commit**

```bash
git add src/Cortex.Messaging.RabbitMQ/MessageSerializer.cs
git commit -m "feat: add MessageSerializer with JSON + type header round-trip"
```

---

## Task 6: RabbitMqOptions

Configuration class for RabbitMQ connection settings.

**Files:**
- Create: `src/Cortex.Messaging.RabbitMQ/RabbitMqOptions.cs`

**Step 1: Create RabbitMqOptions**

```csharp
namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// Configuration options for the RabbitMQ message bus connection.
/// Bound to IOptions&lt;RabbitMqOptions&gt; via appsettings.json.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "RabbitMQ";

    /// <summary>
    /// RabbitMQ server hostname.
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ AMQP port.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Authentication username.
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Authentication password.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// RabbitMQ virtual host.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Name of the topic exchange for message routing.
    /// </summary>
    public string ExchangeName { get; set; } = "cortex.messages";

    /// <summary>
    /// Name of the dead letter exchange.
    /// </summary>
    public string DeadLetterExchangeName { get; set; } = "cortex.deadletter";

    /// <summary>
    /// Whether automatic connection recovery is enabled.
    /// </summary>
    public bool AutoRecoveryEnabled { get; set; } = true;
}
```

**Step 2: Build**

Run: `dotnet build src/Cortex.Messaging.RabbitMQ/Cortex.Messaging.RabbitMQ.csproj`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/Cortex.Messaging.RabbitMQ/RabbitMqOptions.cs
git commit -m "feat: add RabbitMqOptions configuration class"
```

---

## Task 7: RabbitMqConnection

Connection lifecycle management wrapping `RabbitMQ.Client`.

**Files:**
- Create: `src/Cortex.Messaging.RabbitMQ/RabbitMqConnection.cs`

**Step 1: Implement RabbitMqConnection**

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// Manages the RabbitMQ connection lifecycle.
/// Wraps a single IConnection with auto-recovery support.
/// </summary>
public sealed class RabbitMqConnection : IAsyncDisposable
{
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly ILogger<RabbitMqConnection> _logger;
    private IConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    public RabbitMqConnection(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates the AMQP connection.
    /// </summary>
    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var config = _options.Value;

            var factory = new ConnectionFactory
            {
                HostName = config.HostName,
                Port = config.Port,
                UserName = config.UserName,
                Password = config.Password,
                VirtualHost = config.VirtualHost,
                AutomaticRecoveryEnabled = config.AutoRecoveryEnabled,
                ClientProvidedName = "cortex-message-bus"
            };

            _logger.LogInformation(
                "Connecting to RabbitMQ at {HostName}:{Port}/{VirtualHost}",
                config.HostName, config.Port, config.VirtualHost);

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            _logger.LogInformation("Connected to RabbitMQ");

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Creates a new AMQP channel from the connection.
    /// </summary>
    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_connection is not null)
        {
            _logger.LogInformation("Closing RabbitMQ connection");
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _connectionLock.Dispose();
    }
}
```

**Step 2: Build**

Run: `dotnet build src/Cortex.Messaging.RabbitMQ/Cortex.Messaging.RabbitMQ.csproj`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/Cortex.Messaging.RabbitMQ/RabbitMqConnection.cs
git commit -m "feat: add RabbitMqConnection with auto-recovery support"
```

---

## Task 8: RabbitMqMessageBus — Tests

Write integration tests for the full RabbitMQ message bus. These require a running RabbitMQ instance.

**Files:**
- Create: `tests/Cortex.Messaging.RabbitMQ.Tests/RabbitMqMessageBusTests.cs`
- Create: `tests/Cortex.Messaging.RabbitMQ.Tests/RabbitMqFixture.cs`

**Step 1: Create the shared test fixture**

`tests/Cortex.Messaging.RabbitMQ.Tests/RabbitMqFixture.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cortex.Messaging.RabbitMQ.Tests;

/// <summary>
/// Shared fixture that provides a RabbitMqConnection for integration tests.
/// Requires a running RabbitMQ instance at localhost:5672 (cortex/cortex).
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    public RabbitMqConnection Connection { get; private set; } = null!;

    public RabbitMqOptions Options { get; } = new()
    {
        HostName = "localhost",
        Port = 5672,
        UserName = "cortex",
        Password = "cortex",
        VirtualHost = "/"
    };

    public async Task InitializeAsync()
    {
        Connection = new RabbitMqConnection(
            Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<RabbitMqConnection>.Instance);

        // Verify connectivity
        var conn = await Connection.GetConnectionAsync();
        Assert.True(conn.IsOpen, "RabbitMQ connection should be open");
    }

    public async Task DisposeAsync()
    {
        await Connection.DisposeAsync();
    }
}
```

**Step 2: Write integration tests**

`tests/Cortex.Messaging.RabbitMQ.Tests/RabbitMqMessageBusTests.cs`:

```csharp
using Cortex.Core.Messages;
using Cortex.Core.References;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Messaging.RabbitMQ.Tests;

[Collection("RabbitMQ")]
public sealed class RabbitMqMessageBusTests : IClassFixture<RabbitMqFixture>, IAsyncDisposable
{
    private readonly RabbitMqFixture _fixture;
    private readonly RabbitMqMessageBus _bus;

    public RabbitMqMessageBusTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
        _bus = new RabbitMqMessageBus(
            fixture.Connection,
            Microsoft.Extensions.Options.Options.Create(fixture.Options),
            NullLogger<RabbitMqMessageBus>.Instance);
    }

    private static MessageEnvelope CreateEnvelope(string content) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    [Fact]
    public async Task PublishAndConsume_RoundTrip_DeliversMessage()
    {
        // Arrange
        var queueName = $"test-roundtrip-{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<MessageEnvelope>();

        await _bus.StartConsumingAsync(queueName, envelope =>
        {
            received.SetResult(envelope);
            return Task.CompletedTask;
        });

        var sent = CreateEnvelope("hello-rabbit");

        // Act
        await _bus.PublishAsync(sent, queueName);

        // Assert
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var message = Assert.IsType<TestMessage>(result.Message);
        Assert.Equal("hello-rabbit", message.Content);
        Assert.Equal(sent.ReferenceCode, result.ReferenceCode);
    }

    [Fact]
    public async Task PublishAndConsume_PreservesTypeHeader()
    {
        // Arrange
        var queueName = $"test-type-{Guid.NewGuid():N}";
        var received = new TaskCompletionSource<MessageEnvelope>();

        await _bus.StartConsumingAsync(queueName, envelope =>
        {
            received.SetResult(envelope);
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(CreateEnvelope("typed"), queueName);

        // Assert
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsType<TestMessage>(result.Message);
    }

    [Fact]
    public async Task HandlerThrows_MessageNackedToDeadLetter()
    {
        // Arrange
        var queueName = $"test-deadletter-{Guid.NewGuid():N}";
        var deadLetterReceived = new TaskCompletionSource<bool>();

        // Start a consumer that always throws
        await _bus.StartConsumingAsync(queueName, _ =>
            throw new InvalidOperationException("Simulated failure"));

        // Start a dead letter consumer
        await _bus.StartConsumingAsync("cortex.deadletter.queue", _ =>
        {
            deadLetterReceived.TrySetResult(true);
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(CreateEnvelope("will-fail"), queueName);

        // Assert
        var wasDeadLettered = await deadLetterReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(wasDeadLettered);
    }

    [Fact]
    public async Task MultipleQueues_RoutesCorrectly()
    {
        // Arrange
        var queueA = $"test-route-a-{Guid.NewGuid():N}";
        var queueB = $"test-route-b-{Guid.NewGuid():N}";
        var receivedA = new TaskCompletionSource<MessageEnvelope>();
        var receivedB = new TaskCompletionSource<MessageEnvelope>();

        await _bus.StartConsumingAsync(queueA, envelope =>
        {
            receivedA.SetResult(envelope);
            return Task.CompletedTask;
        });
        await _bus.StartConsumingAsync(queueB, envelope =>
        {
            receivedB.SetResult(envelope);
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(CreateEnvelope("for-a"), queueA);
        await _bus.PublishAsync(CreateEnvelope("for-b"), queueB);

        // Assert
        var resultA = await receivedA.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var resultB = await receivedB.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("for-a", ((TestMessage)resultA.Message).Content);
        Assert.Equal("for-b", ((TestMessage)resultB.Message).Content);
    }

    public async ValueTask DisposeAsync()
    {
        await _bus.DisposeAsync();
    }
}
```

**Step 3: Build (tests won't run without RabbitMQ, but should compile)**

Run: `dotnet build tests/Cortex.Messaging.RabbitMQ.Tests/`
Expected: FAIL — `RabbitMqMessageBus` does not exist yet. This is expected.

**Step 4: Commit**

```bash
git add tests/Cortex.Messaging.RabbitMQ.Tests/RabbitMqFixture.cs tests/Cortex.Messaging.RabbitMQ.Tests/RabbitMqMessageBusTests.cs
git commit -m "test: add RabbitMqMessageBus integration tests (red)"
```

---

## Task 9: RabbitMqMessageBus — Implementation

The core message bus implementation using RabbitMQ.Client v7.

**Files:**
- Create: `src/Cortex.Messaging.RabbitMQ/RabbitMqMessageBus.cs`

**Step 1: Implement RabbitMqMessageBus**

```csharp
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Cortex.Core.Messages;

namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// RabbitMQ implementation of <see cref="IMessageBus"/>.
/// Uses a topic exchange for routing and a dead letter fanout for failures.
/// </summary>
public sealed class RabbitMqMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqMessageBus> _logger;

    private IChannel? _publishChannel;
    private readonly SemaphoreSlim _publishChannelLock = new(1, 1);
    private readonly List<(IChannel Channel, string ConsumerTag)> _consumers = [];
    private readonly object _consumersLock = new();
    private bool _topologyDeclared;
    private bool _disposed;

    /// <summary>
    /// Header name used to carry the assembly-qualified message type.
    /// </summary>
    public const string MessageTypeHeader = "cortex-message-type";

    public RabbitMqMessageBus(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqMessageBus> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        MessageEnvelope envelope,
        string queueName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = await GetPublishChannelAsync(cancellationToken);
        await EnsureTopologyAsync(channel, cancellationToken);

        var (body, messageType) = MessageSerializer.Serialize(envelope);
        var routingKey = $"queue.{queueName}";

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                [MessageTypeHeader] = Encoding.UTF8.GetBytes(messageType)
            }
        };

        _logger.LogDebug(
            "Publishing to {Exchange} with routing key {RoutingKey}",
            _options.ExchangeName, routingKey);

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task StartConsumingAsync(
        string queueName,
        Func<MessageEnvelope, Task> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = await _connection.CreateChannelAsync(cancellationToken);
        await EnsureTopologyAsync(channel, cancellationToken);

        // Declare the queue and bind it
        var routingKey = $"queue.{queueName}";

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);

        // Prefetch 1 message at a time
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var messageTypeName = ea.BasicProperties.Headers is not null
                    && ea.BasicProperties.Headers.TryGetValue(MessageTypeHeader, out var typeObj)
                    && typeObj is byte[] typeBytes
                        ? Encoding.UTF8.GetString(typeBytes)
                        : null;

                if (messageTypeName is null)
                {
                    _logger.LogWarning(
                        "Message missing {Header} header, nacking to dead letter",
                        MessageTypeHeader);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                var body = ea.Body.ToArray(); // Copy — body is only valid in this scope
                var envelope = MessageSerializer.Deserialize(body, messageTypeName);

                if (envelope is null)
                {
                    _logger.LogWarning(
                        "Failed to deserialise message of type {MessageType}, nacking to dead letter",
                        messageTypeName);
                    await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                await handler(envelope);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler failed for message {DeliveryTag}, nacking to dead letter", ea.DeliveryTag);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        lock (_consumersLock)
        {
            _consumers.Add((channel, consumerTag));
        }

        _logger.LogInformation(
            "Started consuming from queue {QueueName} with consumer {ConsumerTag}",
            queueName, consumerTag);
    }

    /// <inheritdoc />
    public async Task StopConsumingAsync(CancellationToken cancellationToken = default)
    {
        List<(IChannel Channel, string ConsumerTag)> consumersToStop;

        lock (_consumersLock)
        {
            consumersToStop = [.. _consumers];
            _consumers.Clear();
        }

        foreach (var (channel, consumerTag) in consumersToStop)
        {
            try
            {
                await channel.BasicCancelAsync(consumerTag, cancellationToken: cancellationToken);
                await channel.CloseAsync(cancellationToken: cancellationToken);
                channel.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping consumer {ConsumerTag}", consumerTag);
            }
        }

        _logger.LogInformation("All consumers stopped");
    }

    /// <inheritdoc />
    public Task<QueueTopology> GetTopologyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new QueueTopology());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await StopConsumingAsync();

        if (_publishChannel is not null)
        {
            await _publishChannel.CloseAsync();
            _publishChannel.Dispose();
        }

        _publishChannelLock.Dispose();
    }

    private async Task<IChannel> GetPublishChannelAsync(CancellationToken cancellationToken)
    {
        if (_publishChannel is { IsOpen: true })
        {
            return _publishChannel;
        }

        await _publishChannelLock.WaitAsync(cancellationToken);
        try
        {
            if (_publishChannel is { IsOpen: true })
            {
                return _publishChannel;
            }

            _publishChannel = await _connection.CreateChannelAsync(cancellationToken);
            return _publishChannel;
        }
        finally
        {
            _publishChannelLock.Release();
        }
    }

    private async Task EnsureTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        if (_topologyDeclared)
        {
            return;
        }

        // Declare the topic exchange
        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Declare the dead letter exchange
        await channel.ExchangeDeclareAsync(
            exchange: _options.DeadLetterExchangeName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        // Declare the dead letter queue
        await channel.QueueDeclareAsync(
            queue: "cortex.deadletter.queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: "cortex.deadletter.queue",
            exchange: _options.DeadLetterExchangeName,
            routingKey: "",
            cancellationToken: cancellationToken);

        _topologyDeclared = true;

        _logger.LogInformation(
            "Declared RabbitMQ topology: exchanges {Exchange}, {DeadLetterExchange}",
            _options.ExchangeName, _options.DeadLetterExchangeName);
    }
}
```

**Step 2: Build**

Run: `dotnet build src/Cortex.Messaging.RabbitMQ/Cortex.Messaging.RabbitMQ.csproj`
Expected: Build succeeded.

**Step 3: Build tests (they won't pass without RabbitMQ, but should compile)**

Run: `dotnet build tests/Cortex.Messaging.RabbitMQ.Tests/`
Expected: Build succeeded.

**Step 4: Commit**

```bash
git add src/Cortex.Messaging.RabbitMQ/RabbitMqMessageBus.cs
git commit -m "feat: add RabbitMqMessageBus implementing IMessageBus"
```

---

## Task 10: DI Registration

Extension method for adding RabbitMQ messaging to `IServiceCollection`.

**Files:**
- Create: `src/Cortex.Messaging.RabbitMQ/ServiceCollectionExtensions.cs`

**Step 1: Implement ServiceCollectionExtensions**

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Messaging.RabbitMQ;

/// <summary>
/// Extension methods for registering RabbitMQ messaging services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitMQ-backed messaging to the service collection.
    /// Registers <see cref="RabbitMqMessageBus"/> as singleton for
    /// <see cref="IMessageBus"/>, <see cref="IMessagePublisher"/>, and <see cref="IMessageConsumer"/>.
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        Action<RabbitMqOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<RabbitMqMessageBus>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<RabbitMqMessageBus>());
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RabbitMqMessageBus>());
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<RabbitMqMessageBus>());

        return services;
    }
}
```

**Step 2: Build**

Run: `dotnet build src/Cortex.Messaging.RabbitMQ/Cortex.Messaging.RabbitMQ.csproj`
Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/Cortex.Messaging.RabbitMQ/ServiceCollectionExtensions.cs
git commit -m "feat: add DI registration for RabbitMQ messaging"
```

---

## Task 11: Docker Compose

Local dev environment for running RabbitMQ.

**Files:**
- Create: `docker-compose.yml` (repository root)

**Step 1: Create docker-compose.yml**

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
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "-q", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  rabbitmq_data:
```

**Step 2: Verify it starts (if Docker is available)**

Run: `docker compose up -d`
Expected: RabbitMQ container running. Management UI at http://localhost:15672 (cortex/cortex).

Run: `docker compose down`

**Step 3: Commit**

```bash
git add docker-compose.yml
git commit -m "infra: add Docker Compose for local RabbitMQ dev environment"
```

---

## Task 12: CI Pipeline Update

Add RabbitMQ service container to GitHub Actions for integration tests.

**Files:**
- Modify: `.github/workflows/ci.yml`

**Step 1: Update CI workflow**

Replace the entire `ci.yml` with:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

permissions:
  contents: read

jobs:
  build:
    name: Build & Test
    runs-on: ubuntu-latest

    services:
      rabbitmq:
        image: rabbitmq:4-management-alpine
        ports:
          - 5672:5672
        env:
          RABBITMQ_DEFAULT_USER: cortex
          RABBITMQ_DEFAULT_PASS: cortex
        options: >-
          --health-cmd "rabbitmq-diagnostics -q ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal
```

**Step 2: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add RabbitMQ service container for integration tests"
```

---

## Task 13: Run Integration Tests Locally

Verify the full stack works end-to-end with a real RabbitMQ.

**Step 1: Start RabbitMQ**

Run: `docker compose up -d`
Wait for: `docker compose exec rabbitmq rabbitmq-diagnostics -q ping` to succeed.

**Step 2: Run all tests**

Run: `dotnet test --configuration Release --verbosity normal`
Expected: ALL tests pass — both `Cortex.Messaging.Tests` (InMemory) and `Cortex.Messaging.RabbitMQ.Tests` (integration).

**Step 3: Stop RabbitMQ**

Run: `docker compose down`

**Step 4: Verify unit tests still pass without RabbitMQ**

Run: `dotnet test tests/Cortex.Messaging.Tests/ --configuration Release --verbosity normal`
Expected: All InMemoryMessageBus tests PASS (no Docker needed).

> **Note to implementer:** If the RabbitMQ integration tests fail because RabbitMQ isn't running, that's expected when running locally without Docker. The CI pipeline will always have RabbitMQ available. Consider adding a `[Trait("Category", "Integration")]` attribute to the RabbitMQ tests and filtering them in local dev: `dotnet test --filter "Category!=Integration"`. This is optional but recommended.

**Step 5: Final commit with any fixes**

```bash
git add -A
git commit -m "chore: integration test fixes and cleanup"
```

---

## Task 14: Documentation Update

Update CHANGELOG and README to reflect the new messaging implementation.

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `README.md` (add Docker Compose quick start note)

**Step 1: Update CHANGELOG.md**

Add under `## [Unreleased]` → `### Added`:

```markdown
- `InMemoryMessageBus` in `Cortex.Messaging` for unit testing and local dev
- `Cortex.Messaging.RabbitMQ` project with `RabbitMqMessageBus`, `RabbitMqConnection`, `MessageSerializer`
- DI registration via `services.AddRabbitMqMessaging()`
- Docker Compose for local RabbitMQ development
- RabbitMQ service container in CI pipeline
- Integration tests for publish/consume round-trip, type headers, dead lettering, routing
```

**Step 2: Update README.md quick start**

Add a "Local Development" section after the existing quick start:

```markdown
### Local Development with RabbitMQ

```bash
# Start RabbitMQ (Docker required)
docker compose up -d

# Run all tests including integration
dotnet test --configuration Release

# Management UI: http://localhost:15672 (cortex/cortex)
```
```

**Step 3: Commit**

```bash
git add CHANGELOG.md README.md
git commit -m "docs: update CHANGELOG and README for messaging implementation"
```

---

## Summary

| Task | What | Type |
|------|------|------|
| 1 | TestMessage type | Scaffolding |
| 2 | InMemoryMessageBus tests (red) | TDD — test |
| 3 | InMemoryMessageBus implementation (green) | TDD — implement |
| 4 | MessageSerializer project + tests (red) | TDD — test |
| 5 | MessageSerializer implementation (green) | TDD — implement |
| 6 | RabbitMqOptions | Config |
| 7 | RabbitMqConnection | Infrastructure |
| 8 | RabbitMqMessageBus tests (red) | TDD — test |
| 9 | RabbitMqMessageBus implementation (green) | TDD — implement |
| 10 | DI registration | Infrastructure |
| 11 | Docker Compose | Dev environment |
| 12 | CI pipeline update | Infrastructure |
| 13 | Integration test run | Verification |
| 14 | Documentation | Docs |

**Total: 14 tasks, ~37 commits**
