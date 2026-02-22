# Agent Harness Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the agent execution runtime that connects IAgent implementations to message queues, with dynamic agent creation, reply routing, and in-memory registry/tracker implementations.

**Architecture:** AgentHarness (per-agent queue connection) managed by AgentRuntime (IHostedService + IAgentRuntime singleton). InMemoryAgentRegistry and InMemoryDelegationTracker provide concrete implementations. ReplyTo field on MessageContext enables request/reply patterns.

**Tech Stack:** .NET 10, xUnit, Microsoft.Extensions.Hosting, InMemoryMessageBus (from Cortex.Messaging)

---

### Task 1: Add ReplyTo to MessageContext

**Files:**
- Modify: `src/Cortex.Core/Messages/MessageContext.cs`

**Step 1: Add ReplyTo property**

```csharp
/// <summary>
/// Queue name where responses to this message should be sent.
/// </summary>
public string? ReplyTo { get; init; }
```

Add this as the last property in the `MessageContext` record, after `ChannelId`.

**Step 2: Verify build**

Run: `dotnet build --configuration Release`
Expected: Build succeeds. This is an additive change to an immutable record — no existing code breaks.

**Step 3: Commit**

```bash
git add src/Cortex.Core/Messages/MessageContext.cs
git commit -m "feat: add ReplyTo property to MessageContext for request/reply routing"
```

---

### Task 2: InMemoryAgentRegistry — Tests (red)

**Files:**
- Create: `src/Cortex.Agents/InMemoryAgentRegistry.cs` (empty stub)
- Create: `tests/Cortex.Agents.Tests/InMemoryAgentRegistryTests.cs`

**Step 1: Add project reference**

The test project needs a reference to `Cortex.Messaging` (for `InMemoryMessageBus` later). Add to `tests/Cortex.Agents.Tests/Cortex.Agents.Tests.csproj`:

```xml
<ProjectReference Include="..\..\src\Cortex.Messaging\Cortex.Messaging.csproj" />
```

**Step 2: Create empty stub**

```csharp
// src/Cortex.Agents/InMemoryAgentRegistry.cs
namespace Cortex.Agents;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IAgentRegistry"/>.
/// </summary>
public sealed class InMemoryAgentRegistry : IAgentRegistry
{
    /// <inheritdoc />
    public Task RegisterAsync(AgentRegistration registration, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<AgentRegistration?> FindByIdAsync(string agentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
```

**Step 3: Write failing tests**

```csharp
// tests/Cortex.Agents.Tests/InMemoryAgentRegistryTests.cs
namespace Cortex.Agents.Tests;

public sealed class InMemoryAgentRegistryTests
{
    private readonly InMemoryAgentRegistry _registry = new();

    private static AgentRegistration CreateRegistration(
        string agentId = "test-agent",
        string name = "Test Agent",
        string agentType = "ai",
        params AgentCapability[] capabilities) =>
        new()
        {
            AgentId = agentId,
            Name = name,
            AgentType = agentType,
            Capabilities = capabilities,
            RegisteredAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task RegisterAsync_NullRegistration_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _registry.RegisterAsync(null!));
    }

    [Fact]
    public async Task RegisterAsync_ThenFindById_ReturnsRegistration()
    {
        var reg = CreateRegistration("agent-1");
        await _registry.RegisterAsync(reg);

        var result = await _registry.FindByIdAsync("agent-1");

        Assert.NotNull(result);
        Assert.Equal("agent-1", result.AgentId);
    }

    [Fact]
    public async Task FindByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _registry.FindByIdAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateId_OverwritesRegistration()
    {
        await _registry.RegisterAsync(CreateRegistration("agent-1", name: "First"));
        await _registry.RegisterAsync(CreateRegistration("agent-1", name: "Second"));

        var result = await _registry.FindByIdAsync("agent-1");

        Assert.NotNull(result);
        Assert.Equal("Second", result.Name);
    }

    [Fact]
    public async Task FindByCapabilityAsync_MatchesCapabilityName()
    {
        var cap = new AgentCapability { Name = "drafting", Description = "Draft documents" };
        await _registry.RegisterAsync(CreateRegistration("agent-1", capabilities: cap));
        await _registry.RegisterAsync(CreateRegistration("agent-2"));

        var results = await _registry.FindByCapabilityAsync("drafting");

        Assert.Single(results);
        Assert.Equal("agent-1", results[0].AgentId);
    }

    [Fact]
    public async Task FindByCapabilityAsync_NoMatch_ReturnsEmpty()
    {
        await _registry.RegisterAsync(CreateRegistration("agent-1"));

        var results = await _registry.FindByCapabilityAsync("nonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindByCapabilityAsync_OnlyReturnsAvailableAgents()
    {
        var cap = new AgentCapability { Name = "drafting", Description = "Draft documents" };
        await _registry.RegisterAsync(CreateRegistration("agent-1", capabilities: cap));
        await _registry.RegisterAsync(new AgentRegistration
        {
            AgentId = "agent-2",
            Name = "Unavailable",
            AgentType = "ai",
            Capabilities = [cap],
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = false
        });

        var results = await _registry.FindByCapabilityAsync("drafting");

        Assert.Single(results);
        Assert.Equal("agent-1", results[0].AgentId);
    }
}
```

**Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal`
Expected: FAIL — `NotImplementedException` from stub methods.

**Step 5: Commit**

```bash
git add src/Cortex.Agents/InMemoryAgentRegistry.cs tests/Cortex.Agents.Tests/
git commit -m "test: add InMemoryAgentRegistry tests (red)"
```

---

### Task 3: InMemoryAgentRegistry — Implementation (green)

**Files:**
- Modify: `src/Cortex.Agents/InMemoryAgentRegistry.cs`

**Step 1: Implement**

```csharp
using System.Collections.Concurrent;

namespace Cortex.Agents;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IAgentRegistry"/>.
/// </summary>
public sealed class InMemoryAgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, AgentRegistration> _agents = new();

    /// <inheritdoc />
    public Task RegisterAsync(AgentRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _agents[registration.AgentId] = registration;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AgentRegistration?> FindByIdAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        _agents.TryGetValue(agentId, out var registration);
        return Task.FromResult(registration);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);

        var matches = _agents.Values
            .Where(a => a.IsAvailable && a.Capabilities.Any(c =>
                string.Equals(c.Name, capabilityName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return Task.FromResult<IReadOnlyList<AgentRegistration>>(matches);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal`
Expected: All 7 tests PASS.

**Step 3: Commit**

```bash
git add src/Cortex.Agents/InMemoryAgentRegistry.cs
git commit -m "feat: implement InMemoryAgentRegistry with ConcurrentDictionary"
```

---

### Task 4: InMemoryDelegationTracker — Tests (red)

**Files:**
- Create: `src/Cortex.Agents/Delegation/InMemoryDelegationTracker.cs` (empty stub)
- Create: `tests/Cortex.Agents.Tests/InMemoryDelegationTrackerTests.cs`

**Step 1: Create empty stub**

```csharp
// src/Cortex.Agents/Delegation/InMemoryDelegationTracker.cs
using Cortex.Core.References;

namespace Cortex.Agents.Delegation;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IDelegationTracker"/>.
/// </summary>
public sealed class InMemoryDelegationTracker : IDelegationTracker
{
    /// <inheritdoc />
    public Task DelegateAsync(DelegationRecord record, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task UpdateStatusAsync(ReferenceCode referenceCode, DelegationStatus status, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<DelegationRecord>> GetByAssigneeAsync(string agentId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<DelegationRecord>> GetOverdueAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
```

**Step 2: Write failing tests**

```csharp
// tests/Cortex.Agents.Tests/InMemoryDelegationTrackerTests.cs
using Cortex.Agents.Delegation;
using Cortex.Core.References;

namespace Cortex.Agents.Tests;

public sealed class InMemoryDelegationTrackerTests
{
    private readonly InMemoryDelegationTracker _tracker = new();

    private static DelegationRecord CreateRecord(
        string delegatedTo = "agent-1",
        DelegationStatus status = DelegationStatus.Assigned,
        DateTimeOffset? dueAt = null) =>
        new()
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            DelegatedBy = "cos-agent",
            DelegatedTo = delegatedTo,
            Description = "Test task",
            Status = status,
            AssignedAt = DateTimeOffset.UtcNow,
            DueAt = dueAt
        };

    [Fact]
    public async Task DelegateAsync_NullRecord_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _tracker.DelegateAsync(null!));
    }

    [Fact]
    public async Task DelegateAsync_ThenGetByAssignee_ReturnsRecord()
    {
        var record = CreateRecord("agent-1");
        await _tracker.DelegateAsync(record);

        var results = await _tracker.GetByAssigneeAsync("agent-1");

        Assert.Single(results);
        Assert.Equal(record.ReferenceCode, results[0].ReferenceCode);
    }

    [Fact]
    public async Task GetByAssigneeAsync_NoRecords_ReturnsEmpty()
    {
        var results = await _tracker.GetByAssigneeAsync("nonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatus()
    {
        var record = CreateRecord("agent-1");
        await _tracker.DelegateAsync(record);

        await _tracker.UpdateStatusAsync(record.ReferenceCode, DelegationStatus.InProgress);

        var results = await _tracker.GetByAssigneeAsync("agent-1");
        Assert.Equal(DelegationStatus.InProgress, results[0].Status);
    }

    [Fact]
    public async Task GetOverdueAsync_ReturnsPastDueRecords()
    {
        var overdue = CreateRecord("agent-1", dueAt: DateTimeOffset.UtcNow.AddHours(-1));
        var notDue = CreateRecord("agent-2", dueAt: DateTimeOffset.UtcNow.AddHours(1));
        var noDueDate = CreateRecord("agent-3");

        await _tracker.DelegateAsync(overdue);
        await _tracker.DelegateAsync(notDue);
        await _tracker.DelegateAsync(noDueDate);

        var results = await _tracker.GetOverdueAsync();

        Assert.Single(results);
        Assert.Equal("agent-1", results[0].DelegatedTo);
    }

    [Fact]
    public async Task GetOverdueAsync_ExcludesCompletedRecords()
    {
        var record = CreateRecord("agent-1", dueAt: DateTimeOffset.UtcNow.AddHours(-1));
        await _tracker.DelegateAsync(record);
        await _tracker.UpdateStatusAsync(record.ReferenceCode, DelegationStatus.Complete);

        var results = await _tracker.GetOverdueAsync();

        Assert.Empty(results);
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal`
Expected: FAIL — `NotImplementedException` from stub methods.

**Step 4: Commit**

```bash
git add src/Cortex.Agents/Delegation/InMemoryDelegationTracker.cs tests/Cortex.Agents.Tests/
git commit -m "test: add InMemoryDelegationTracker tests (red)"
```

---

### Task 5: InMemoryDelegationTracker — Implementation (green)

**Files:**
- Modify: `src/Cortex.Agents/Delegation/InMemoryDelegationTracker.cs`

**Step 1: Implement**

```csharp
using System.Collections.Concurrent;
using Cortex.Core.References;

namespace Cortex.Agents.Delegation;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IDelegationTracker"/>.
/// </summary>
public sealed class InMemoryDelegationTracker : IDelegationTracker
{
    private readonly ConcurrentDictionary<string, DelegationRecord> _records = new();

    /// <inheritdoc />
    public Task DelegateAsync(DelegationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.ReferenceCode.Value] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(ReferenceCode referenceCode, DelegationStatus status, CancellationToken cancellationToken = default)
    {
        if (_records.TryGetValue(referenceCode.Value, out var existing))
        {
            _records[referenceCode.Value] = existing with { Status = status };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DelegationRecord>> GetByAssigneeAsync(string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        var matches = _records.Values
            .Where(r => string.Equals(r.DelegatedTo, agentId, StringComparison.Ordinal))
            .ToList();

        return Task.FromResult<IReadOnlyList<DelegationRecord>>(matches);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DelegationRecord>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var overdue = _records.Values
            .Where(r => r.DueAt.HasValue
                && r.DueAt.Value < now
                && r.Status != DelegationStatus.Complete)
            .ToList();

        return Task.FromResult<IReadOnlyList<DelegationRecord>>(overdue);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal`
Expected: All 13 tests PASS (7 registry + 6 tracker).

**Step 3: Commit**

```bash
git add src/Cortex.Agents/Delegation/InMemoryDelegationTracker.cs
git commit -m "feat: implement InMemoryDelegationTracker with ConcurrentDictionary"
```

---

### Task 6: EchoAgent and Test Helpers

**Files:**
- Create: `tests/Cortex.Agents.Tests/TestMessage.cs`
- Create: `tests/Cortex.Agents.Tests/EchoAgent.cs`

**Step 1: Create TestMessage**

```csharp
// tests/Cortex.Agents.Tests/TestMessage.cs
using Cortex.Core.Messages;

namespace Cortex.Agents.Tests;

/// <summary>
/// Simple message type for testing agent harness behaviour.
/// </summary>
public sealed record TestMessage : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Test content payload.
    /// </summary>
    public required string Content { get; init; }
}
```

**Step 2: Create EchoAgent**

```csharp
// tests/Cortex.Agents.Tests/EchoAgent.cs
using Cortex.Core.Messages;

namespace Cortex.Agents.Tests;

/// <summary>
/// Test agent that echoes back the received message content in a response envelope.
/// </summary>
public sealed class EchoAgent : IAgent
{
    /// <inheritdoc />
    public string AgentId { get; } = "echo-agent";

    /// <inheritdoc />
    public string Name { get; } = "Echo Agent";

    /// <inheritdoc />
    public IReadOnlyList<AgentCapability> Capabilities { get; } =
    [
        new AgentCapability { Name = "echo", Description = "Echoes messages back" }
    ];

    /// <inheritdoc />
    public Task<MessageEnvelope?> ProcessAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var incoming = (TestMessage)envelope.Message;

        var response = envelope with
        {
            Message = new TestMessage { Content = $"echo: {incoming.Content}" }
        };

        return Task.FromResult<MessageEnvelope?>(response);
    }
}
```

**Step 3: Verify build**

Run: `dotnet build tests/Cortex.Agents.Tests --configuration Release`
Expected: Build succeeds.

**Step 4: Commit**

```bash
git add tests/Cortex.Agents.Tests/TestMessage.cs tests/Cortex.Agents.Tests/EchoAgent.cs
git commit -m "test: add EchoAgent and TestMessage for agent harness testing"
```

---

### Task 7: AgentHarness — Tests (red)

**Files:**
- Create: `src/Cortex.Agents/AgentHarness.cs` (empty stub)
- Create: `tests/Cortex.Agents.Tests/AgentHarnessTests.cs`

The `Cortex.Agents` project needs a reference to `Cortex.Messaging` for `IMessageBus`. Add to `src/Cortex.Agents/Cortex.Agents.csproj`:

```xml
<ProjectReference Include="..\Cortex.Messaging\Cortex.Messaging.csproj" />
```

**Step 1: Create empty stub**

```csharp
// src/Cortex.Agents/AgentHarness.cs
using Cortex.Messaging;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents;

/// <summary>
/// Connects a single <see cref="IAgent"/> to its message queue.
/// Handles message dispatch, reply routing, and lifecycle management.
/// </summary>
public sealed class AgentHarness
{
    public AgentHarness(
        IAgent agent,
        IMessageBus messageBus,
        IAgentRegistry agentRegistry,
        ILogger<AgentHarness> logger)
    {
    }

    /// <summary>
    /// The queue name this harness consumes from.
    /// </summary>
    public string QueueName => throw new NotImplementedException();

    /// <summary>
    /// Whether this harness is currently running.
    /// </summary>
    public bool IsRunning => throw new NotImplementedException();

    /// <summary>
    /// Starts the harness: registers the agent and begins consuming messages.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <summary>
    /// Stops the harness: stops consuming and marks the agent as unavailable.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
```

Also add `Microsoft.Extensions.Logging.Abstractions` to the Agents project. Add to `src/Cortex.Agents/Cortex.Agents.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.2" />
```

**Step 2: Write failing tests**

```csharp
// tests/Cortex.Agents.Tests/AgentHarnessTests.cs
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class AgentHarnessTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _registry = new();

    private AgentHarness CreateHarness(IAgent? agent = null) =>
        new(
            agent ?? new EchoAgent(),
            _bus,
            _registry,
            NullLogger<AgentHarness>.Instance);

    private static MessageEnvelope CreateEnvelope(
        string content,
        string? replyTo = null) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo }
        };

    [Fact]
    public void QueueName_DerivedFromAgentId()
    {
        var harness = CreateHarness();

        Assert.Equal("agent.echo-agent", harness.QueueName);
    }

    [Fact]
    public async Task StartAsync_RegistersAgentInRegistry()
    {
        var harness = CreateHarness();

        await harness.StartAsync();

        var reg = await _registry.FindByIdAsync("echo-agent");
        Assert.NotNull(reg);
        Assert.True(reg.IsAvailable);
    }

    [Fact]
    public async Task StartAsync_SetsIsRunningTrue()
    {
        var harness = CreateHarness();

        await harness.StartAsync();

        Assert.True(harness.IsRunning);
    }

    [Fact]
    public async Task StopAsync_SetsIsRunningFalse()
    {
        var harness = CreateHarness();
        await harness.StartAsync();

        await harness.StopAsync();

        Assert.False(harness.IsRunning);
    }

    [Fact]
    public async Task MessageDispatched_ToAgentProcessAsync()
    {
        var received = new TaskCompletionSource<MessageEnvelope>();
        var agent = new CallbackAgent("cb-agent", envelope =>
        {
            received.SetResult(envelope);
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var harness = CreateHarness(agent);
        await harness.StartAsync();

        await _bus.PublishAsync(CreateEnvelope("hello"), "agent.cb-agent");

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var msg = Assert.IsType<TestMessage>(result.Message);
        Assert.Equal("hello", msg.Content);
    }

    [Fact]
    public async Task ResponsePublished_ToReplyToQueue()
    {
        var harness = CreateHarness(); // EchoAgent
        await harness.StartAsync();

        var replyReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("reply-queue", envelope =>
        {
            replyReceived.SetResult(envelope);
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(
            CreateEnvelope("hello", replyTo: "reply-queue"),
            "agent.echo-agent");

        var reply = await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var msg = Assert.IsType<TestMessage>(reply.Message);
        Assert.Equal("echo: hello", msg.Content);
    }

    [Fact]
    public async Task ResponseWithNoReplyTo_IsDropped()
    {
        var harness = CreateHarness(); // EchoAgent always returns a response
        await harness.StartAsync();

        // Publish with no ReplyTo — should not throw
        await _bus.PublishAsync(
            CreateEnvelope("hello"),
            "agent.echo-agent");

        // Give it time to process
        await Task.Delay(100);

        // No assertion — just verifying it doesn't throw or deadlock
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _bus.DisposeAsync();
    }
}

/// <summary>
/// Test agent that invokes a callback on ProcessAsync.
/// </summary>
file sealed class CallbackAgent(
    string agentId,
    Func<MessageEnvelope, Task<MessageEnvelope?>> callback) : IAgent
{
    public string AgentId { get; } = agentId;
    public string Name { get; } = $"Callback Agent ({agentId})";
    public IReadOnlyList<AgentCapability> Capabilities { get; } = [];

    public Task<MessageEnvelope?> ProcessAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => callback(envelope);
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal`
Expected: FAIL — `NotImplementedException` from stub methods.

**Step 4: Commit**

```bash
git add src/Cortex.Agents/ tests/Cortex.Agents.Tests/
git commit -m "test: add AgentHarness tests (red)"
```

---

### Task 8: AgentHarness — Implementation (green)

**Files:**
- Modify: `src/Cortex.Agents/AgentHarness.cs`

**Step 1: Implement**

```csharp
using Cortex.Core.Messages;
using Cortex.Messaging;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents;

/// <summary>
/// Connects a single <see cref="IAgent"/> to its message queue.
/// Handles message dispatch, reply routing, and lifecycle management.
/// </summary>
public sealed class AgentHarness
{
    private readonly IAgent _agent;
    private readonly IMessageBus _messageBus;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<AgentHarness> _logger;

    /// <summary>
    /// Creates a new <see cref="AgentHarness"/> for the specified agent.
    /// </summary>
    public AgentHarness(
        IAgent agent,
        IMessageBus messageBus,
        IAgentRegistry agentRegistry,
        ILogger<AgentHarness> logger)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(agentRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _agent = agent;
        _messageBus = messageBus;
        _agentRegistry = agentRegistry;
        _logger = logger;
    }

    /// <summary>
    /// The queue name this harness consumes from.
    /// </summary>
    public string QueueName => $"agent.{_agent.AgentId}";

    /// <summary>
    /// Whether this harness is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Starts the harness: registers the agent and begins consuming messages.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var registration = new AgentRegistration
        {
            AgentId = _agent.AgentId,
            Name = _agent.Name,
            AgentType = _agent is IAgentTypeProvider typed ? typed.AgentType : "unknown",
            Capabilities = _agent.Capabilities.ToList(),
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = true
        };

        await _agentRegistry.RegisterAsync(registration, cancellationToken);

        await _messageBus.StartConsumingAsync(QueueName, HandleMessageAsync, cancellationToken);

        IsRunning = true;

        _logger.LogInformation(
            "Agent {AgentId} started, consuming from {QueueName}",
            _agent.AgentId, QueueName);
    }

    /// <summary>
    /// Stops the harness: stops consuming and marks the agent as unavailable.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _messageBus.StopConsumingAsync(cancellationToken);

        // Mark agent as unavailable
        var registration = await _agentRegistry.FindByIdAsync(_agent.AgentId, cancellationToken);
        if (registration is not null)
        {
            await _agentRegistry.RegisterAsync(
                registration with { IsAvailable = false },
                cancellationToken);
        }

        IsRunning = false;

        _logger.LogInformation("Agent {AgentId} stopped", _agent.AgentId);
    }

    private async Task HandleMessageAsync(MessageEnvelope envelope)
    {
        _logger.LogDebug(
            "Agent {AgentId} processing message {MessageId}",
            _agent.AgentId, envelope.Message.MessageId);

        var response = await _agent.ProcessAsync(envelope);

        if (response is null)
        {
            return;
        }

        var replyTo = envelope.Context.ReplyTo;

        if (string.IsNullOrWhiteSpace(replyTo))
        {
            _logger.LogWarning(
                "Agent {AgentId} returned a response but message has no ReplyTo — dropping response",
                _agent.AgentId);
            return;
        }

        // Carry forward reference code and set parent message ID
        var replyEnvelope = response with
        {
            ReferenceCode = envelope.ReferenceCode,
            Context = response.Context with
            {
                ParentMessageId = envelope.Message.MessageId
            }
        };

        await _messageBus.PublishAsync(replyEnvelope, replyTo);

        _logger.LogDebug(
            "Agent {AgentId} published reply to {ReplyTo}",
            _agent.AgentId, replyTo);
    }
}
```

Note: The `AgentType` property needs to come from the agent somehow. We have two options: add it to `IAgent` or use a separate marker interface. Since `IAgent` doesn't have `AgentType`, use a simple fallback: if the agent doesn't provide a type, default to `"unknown"`. Define a small optional interface:

```csharp
// Add to src/Cortex.Agents/IAgentTypeProvider.cs
namespace Cortex.Agents;

/// <summary>
/// Optional interface for agents to declare their type ("human" or "ai").
/// </summary>
public interface IAgentTypeProvider
{
    /// <summary>
    /// The agent type, typically "human" or "ai".
    /// </summary>
    string AgentType { get; }
}
```

Update the `EchoAgent` test helper to implement it:

```csharp
// In EchoAgent — no change needed, "unknown" is fine for a test agent
```

**Step 2: Run tests**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal`
Expected: All tests PASS.

**Step 3: Commit**

```bash
git add src/Cortex.Agents/ tests/Cortex.Agents.Tests/
git commit -m "feat: implement AgentHarness with message dispatch and reply routing"
```

---

### Task 9: IAgentRuntime Interface

**Files:**
- Create: `src/Cortex.Agents/IAgentRuntime.cs`

**Step 1: Create interface**

```csharp
namespace Cortex.Agents;

/// <summary>
/// Runtime for managing agent harnesses. Supports both static (DI-registered)
/// and dynamic (on-demand) agent lifecycle management.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Starts an agent and connects it to its message queue.
    /// Returns the agent's ID.
    /// </summary>
    Task<string> StartAgentAsync(IAgent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running agent and disconnects it from its queue.
    /// </summary>
    Task StopAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// IDs of all currently running agents.
    /// </summary>
    IReadOnlyList<string> RunningAgentIds { get; }
}
```

**Step 2: Verify build**

Run: `dotnet build src/Cortex.Agents --configuration Release`
Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/Cortex.Agents/IAgentRuntime.cs
git commit -m "feat: add IAgentRuntime interface for dynamic agent lifecycle"
```

---

### Task 10: AgentRuntime — Tests (red)

**Files:**
- Create: `src/Cortex.Agents/AgentRuntime.cs` (empty stub)
- Create: `tests/Cortex.Agents.Tests/AgentRuntimeTests.cs`

The test project needs `Microsoft.Extensions.Hosting` for `IHostedService` testing. Add to `tests/Cortex.Agents.Tests/Cortex.Agents.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.2" />
```

And the `Cortex.Agents` project needs the `Microsoft.Extensions.Hosting.Abstractions` package. Add to `src/Cortex.Agents/Cortex.Agents.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="9.0.2" />
```

**Step 1: Create empty stub**

```csharp
// src/Cortex.Agents/AgentRuntime.cs
using Microsoft.Extensions.Hosting;

namespace Cortex.Agents;

/// <summary>
/// Manages all agent harnesses. Implements <see cref="IHostedService"/> for host integration
/// and <see cref="IAgentRuntime"/> for dynamic agent creation by other agents.
/// </summary>
public sealed class AgentRuntime : IHostedService, IAgentRuntime
{
    /// <inheritdoc />
    public IReadOnlyList<string> RunningAgentIds => throw new NotImplementedException();

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    Task<string> IAgentRuntime.StartAgentAsync(IAgent agent, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <inheritdoc />
    Task IAgentRuntime.StopAgentAsync(string agentId, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
```

**Step 2: Write failing tests**

```csharp
// tests/Cortex.Agents.Tests/AgentRuntimeTests.cs
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class AgentRuntimeTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _registry = new();
    private readonly AgentRuntime _runtime;

    public AgentRuntimeTests()
    {
        _runtime = new AgentRuntime(
            _bus,
            _registry,
            [],  // no startup agents
            NullLoggerFactory.Instance);
    }

    private static MessageEnvelope CreateEnvelope(string content, string? replyTo = null) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo }
        };

    [Fact]
    public async Task StartAsync_WithStartupAgents_StartsAll()
    {
        var agent = new EchoAgent();
        var runtime = new AgentRuntime(
            _bus,
            _registry,
            [agent],
            NullLoggerFactory.Instance);

        await runtime.StartAsync(CancellationToken.None);

        Assert.Contains("echo-agent", runtime.RunningAgentIds);

        await runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAgentAsync_DynamicAgent_ReturnsAgentId()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        var agentId = await rt.StartAgentAsync(new EchoAgent());

        Assert.Equal("echo-agent", agentId);
        Assert.Contains("echo-agent", _runtime.RunningAgentIds);

        await _runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAgentAsync_RemovesFromRunning()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        await rt.StartAgentAsync(new EchoAgent());
        await rt.StopAgentAsync("echo-agent");

        Assert.DoesNotContain("echo-agent", _runtime.RunningAgentIds);

        await _runtime.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_StopsAllAgents()
    {
        var runtime = new AgentRuntime(
            _bus,
            _registry,
            [new EchoAgent()],
            NullLoggerFactory.Instance);

        await runtime.StartAsync(CancellationToken.None);
        Assert.Single(runtime.RunningAgentIds);

        await runtime.StopAsync(CancellationToken.None);

        Assert.Empty(runtime.RunningAgentIds);
    }

    [Fact]
    public async Task DynamicAgent_ReceivesAndReplies()
    {
        await _runtime.StartAsync(CancellationToken.None);

        IAgentRuntime rt = _runtime;
        await rt.StartAgentAsync(new EchoAgent());

        var replyReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("reply-queue", envelope =>
        {
            replyReceived.SetResult(envelope);
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(
            CreateEnvelope("hello", replyTo: "reply-queue"),
            "agent.echo-agent");

        var reply = await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var msg = Assert.IsType<TestMessage>(reply.Message);
        Assert.Equal("echo: hello", msg.Content);

        await _runtime.StopAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _bus.DisposeAsync();
    }
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal`
Expected: FAIL — `NotImplementedException` from stub methods.

**Step 4: Commit**

```bash
git add src/Cortex.Agents/ tests/Cortex.Agents.Tests/
git commit -m "test: add AgentRuntime tests (red)"
```

---

### Task 11: AgentRuntime — Implementation (green)

**Files:**
- Modify: `src/Cortex.Agents/AgentRuntime.cs`

**Step 1: Implement**

```csharp
using System.Collections.Concurrent;
using Cortex.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents;

/// <summary>
/// Manages all agent harnesses. Implements <see cref="IHostedService"/> for host integration
/// and <see cref="IAgentRuntime"/> for dynamic agent creation by other agents.
/// </summary>
public sealed class AgentRuntime : IHostedService, IAgentRuntime
{
    private readonly IMessageBus _messageBus;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IReadOnlyList<IAgent> _startupAgents;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AgentRuntime> _logger;
    private readonly ConcurrentDictionary<string, AgentHarness> _harnesses = new();

    /// <summary>
    /// Creates a new <see cref="AgentRuntime"/>.
    /// </summary>
    public AgentRuntime(
        IMessageBus messageBus,
        IAgentRegistry agentRegistry,
        IEnumerable<IAgent> startupAgents,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(agentRegistry);
        ArgumentNullException.ThrowIfNull(startupAgents);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _messageBus = messageBus;
        _agentRegistry = agentRegistry;
        _startupAgents = startupAgents.ToList();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AgentRuntime>();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> RunningAgentIds =>
        _harnesses.Keys.ToList();

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent runtime starting with {Count} startup agents", _startupAgents.Count);

        foreach (var agent in _startupAgents)
        {
            await ((IAgentRuntime)this).StartAgentAsync(agent, cancellationToken);
        }

        _logger.LogInformation("Agent runtime started");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent runtime stopping, draining {Count} agents", _harnesses.Count);

        var agentIds = _harnesses.Keys.ToList();

        foreach (var agentId in agentIds)
        {
            await ((IAgentRuntime)this).StopAgentAsync(agentId, cancellationToken);
        }

        _logger.LogInformation("Agent runtime stopped");
    }

    /// <inheritdoc />
    async Task<string> IAgentRuntime.StartAgentAsync(IAgent agent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var harness = new AgentHarness(
            agent,
            _messageBus,
            _agentRegistry,
            _loggerFactory.CreateLogger<AgentHarness>());

        if (!_harnesses.TryAdd(agent.AgentId, harness))
        {
            throw new InvalidOperationException($"Agent '{agent.AgentId}' is already running.");
        }

        await harness.StartAsync(cancellationToken);

        _logger.LogInformation("Started agent {AgentId}", agent.AgentId);

        return agent.AgentId;
    }

    /// <inheritdoc />
    async Task IAgentRuntime.StopAgentAsync(string agentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        if (!_harnesses.TryRemove(agentId, out var harness))
        {
            _logger.LogWarning("Cannot stop agent {AgentId}: not running", agentId);
            return;
        }

        await harness.StopAsync(cancellationToken);

        _logger.LogInformation("Stopped agent {AgentId}", agentId);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Cortex.Agents.Tests --configuration Release --verbosity normal`
Expected: All tests PASS.

**Step 3: Commit**

```bash
git add src/Cortex.Agents/AgentRuntime.cs
git commit -m "feat: implement AgentRuntime with IHostedService and dynamic agent management"
```

---

### Task 12: DI Extensions

**Files:**
- Create: `src/Cortex.Agents/AgentRuntimeBuilder.cs`
- Create: `src/Cortex.Agents/ServiceCollectionExtensions.cs`

The `Cortex.Agents` project needs `Microsoft.Extensions.DependencyInjection.Abstractions`. Add to `src/Cortex.Agents/Cortex.Agents.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.2" />
```

**Step 1: Create builder**

```csharp
// src/Cortex.Agents/AgentRuntimeBuilder.cs
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Agents;

/// <summary>
/// Builder for configuring agents that start with the runtime.
/// </summary>
public sealed class AgentRuntimeBuilder
{
    private readonly IServiceCollection _services;

    internal AgentRuntimeBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Registers an agent type to be started when the runtime starts.
    /// </summary>
    public AgentRuntimeBuilder AddAgent<TAgent>() where TAgent : class, IAgent
    {
        _services.AddSingleton<IAgent, TAgent>();
        return this;
    }
}
```

**Step 2: Create DI extensions**

```csharp
// src/Cortex.Agents/ServiceCollectionExtensions.cs
using Cortex.Agents.Delegation;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Agents;

/// <summary>
/// Extension methods for registering the Cortex agent runtime.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Cortex agent runtime to the service collection.
    /// </summary>
    public static IServiceCollection AddCortexAgentRuntime(
        this IServiceCollection services,
        Action<AgentRuntimeBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new AgentRuntimeBuilder(services);
        configure?.Invoke(builder);

        services.AddSingleton<InMemoryAgentRegistry>();
        services.AddSingleton<IAgentRegistry>(sp => sp.GetRequiredService<InMemoryAgentRegistry>());
        services.AddSingleton<InMemoryDelegationTracker>();
        services.AddSingleton<IDelegationTracker>(sp => sp.GetRequiredService<InMemoryDelegationTracker>());
        services.AddSingleton<AgentRuntime>();
        services.AddSingleton<IAgentRuntime>(sp => sp.GetRequiredService<AgentRuntime>());
        services.AddHostedService(sp => sp.GetRequiredService<AgentRuntime>());

        return services;
    }
}
```

**Step 3: Verify build**

Run: `dotnet build src/Cortex.Agents --configuration Release`
Expected: Build succeeds.

**Step 4: Commit**

```bash
git add src/Cortex.Agents/
git commit -m "feat: add DI extensions for agent runtime registration"
```

---

### Task 13: Full Test Suite Run

**Step 1: Run all tests**

Run: `dotnet test --configuration Release --verbosity normal`
Expected: All tests PASS across all projects. Check for any regressions.

**Step 2: Fix any issues discovered**

If any test failures or build warnings, fix them and commit the fixes.

---

### Task 14: Documentation Update

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `CLAUDE.md`

**Step 1: Update CHANGELOG.md**

Add to the `[Unreleased] > Added` section:

```markdown
- **Agent runtime harness** (Cortex.Agents)
  - AgentHarness: connects IAgent to message queue with dispatch and reply routing
  - AgentRuntime: IHostedService + IAgentRuntime for static and dynamic agent management
  - IAgentRuntime: injectable interface for dynamic agent creation/destruction
  - InMemoryAgentRegistry: thread-safe IAgentRegistry implementation
  - InMemoryDelegationTracker: thread-safe IDelegationTracker implementation
  - ServiceCollectionExtensions: `AddCortexAgentRuntime()` DI registration
- ReplyTo field on MessageContext for request/reply message patterns
```

**Step 2: Update CLAUDE.md**

Update the Design Decisions section to add:
```markdown
- **Agent runtime**: AgentHarness per agent, AgentRuntime as IHostedService, dynamic agent creation via IAgentRuntime
```

**Step 3: Commit**

```bash
git add CHANGELOG.md CLAUDE.md
git commit -m "docs: update documentation with agent harness implementation"
```
