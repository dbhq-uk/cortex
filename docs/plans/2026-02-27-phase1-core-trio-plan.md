# Phase 1 Core Trio Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement authority claim validation (#8), AskMeFirst plan approval gating (#27), and delegation supervision service (#28) — completing the core Phase 1 framework.

**Architecture:** Authority enforcement in AgentHarness validates claims before agent processing. Plan approval gating adds AskMeFirst flow to SkillDrivenAgent after decomposition. Delegation supervision runs as an IHostedService polling for overdue delegations.

**Tech Stack:** C# / .NET 10, xUnit, InMemoryMessageBus, ConcurrentDictionary stores

---

## Feature #8 — Authority Claim Validation and Enforcement

### Task 1: Extend IAuthorityProvider with write methods

**Files:**
- Modify: `src/Cortex.Core/Authority/IAuthorityProvider.cs`

**Step 1: Add GrantAsync and RevokeAsync to the interface**

```csharp
namespace Cortex.Core.Authority;

/// <summary>
/// Resolves and validates authority claims for agents and actions.
/// </summary>
public interface IAuthorityProvider
{
    /// <summary>
    /// Gets the authority claim for a specific agent and action, if one exists.
    /// </summary>
    Task<AuthorityClaim?> GetClaimAsync(string agentId, string action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an agent has sufficient authority for an action at the specified minimum tier.
    /// </summary>
    Task<bool> HasAuthorityAsync(string agentId, string action, AuthorityTier minimumTier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants an authority claim.
    /// </summary>
    Task GrantAsync(AuthorityClaim claim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the authority claim for a specific agent and action.
    /// </summary>
    Task RevokeAsync(string agentId, string action, CancellationToken cancellationToken = default);
}
```

**Step 2: Verify build succeeds**

Run: `dotnet build --configuration Release`
Expected: Build succeeds (no existing implementations to break)

**Step 3: Commit**

```bash
git add src/Cortex.Core/Authority/IAuthorityProvider.cs
git commit -m "feat(authority): add GrantAsync and RevokeAsync to IAuthorityProvider (#8)"
```

---

### Task 2: Implement InMemoryAuthorityProvider — grant, get, has authority

**Files:**
- Create: `src/Cortex.Core/Authority/InMemoryAuthorityProvider.cs`
- Create: `tests/Cortex.Core.Tests/Authority/InMemoryAuthorityProviderTests.cs`

**Step 1: Write the failing tests**

```csharp
namespace Cortex.Core.Tests.Authority;

using Cortex.Core.Authority;

public sealed class InMemoryAuthorityProviderTests
{
    private readonly InMemoryAuthorityProvider _provider = new();

    private static AuthorityClaim CreateClaim(
        string grantedTo = "agent-1",
        string action = "send-email",
        AuthorityTier tier = AuthorityTier.DoItAndShowMe,
        DateTimeOffset? expiresAt = null) =>
        new()
        {
            GrantedBy = "founder",
            GrantedTo = grantedTo,
            Tier = tier,
            PermittedActions = [action],
            GrantedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

    [Fact]
    public async Task GrantAsync_ThenGetClaimAsync_ReturnsClaim()
    {
        var claim = CreateClaim();
        await _provider.GrantAsync(claim);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.NotNull(result);
        Assert.Equal(AuthorityTier.DoItAndShowMe, result.Tier);
        Assert.Equal("founder", result.GrantedBy);
    }

    [Fact]
    public async Task GetClaimAsync_NoGrant_ReturnsNull()
    {
        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.Null(result);
    }

    [Fact]
    public async Task HasAuthorityAsync_SufficientTier_ReturnsTrue()
    {
        await _provider.GrantAsync(CreateClaim(tier: AuthorityTier.AskMeFirst));

        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.DoItAndShowMe);

        Assert.True(result);
    }

    [Fact]
    public async Task HasAuthorityAsync_InsufficientTier_ReturnsFalse()
    {
        await _provider.GrantAsync(CreateClaim(tier: AuthorityTier.JustDoIt));

        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.DoItAndShowMe);

        Assert.False(result);
    }

    [Fact]
    public async Task HasAuthorityAsync_ExactTier_ReturnsTrue()
    {
        await _provider.GrantAsync(CreateClaim(tier: AuthorityTier.DoItAndShowMe));

        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.DoItAndShowMe);

        Assert.True(result);
    }

    [Fact]
    public async Task HasAuthorityAsync_NoGrant_ReturnsFalse()
    {
        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.JustDoIt);

        Assert.False(result);
    }

    [Fact]
    public async Task GrantAsync_MultipleActions_EachRetrievable()
    {
        var claim = new AuthorityClaim
        {
            GrantedBy = "founder",
            GrantedTo = "agent-1",
            Tier = AuthorityTier.DoItAndShowMe,
            PermittedActions = ["send-email", "draft-reply"],
            GrantedAt = DateTimeOffset.UtcNow
        };
        await _provider.GrantAsync(claim);

        var email = await _provider.GetClaimAsync("agent-1", "send-email");
        var draft = await _provider.GetClaimAsync("agent-1", "draft-reply");

        Assert.NotNull(email);
        Assert.NotNull(draft);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryAuthorityProviderTests" --verbosity normal`
Expected: Build failure — `InMemoryAuthorityProvider` does not exist

**Step 3: Write the implementation**

```csharp
using System.Collections.Concurrent;

namespace Cortex.Core.Authority;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IAuthorityProvider"/>.
/// Stores claims keyed by (agentId, action) pairs.
/// </summary>
public sealed class InMemoryAuthorityProvider : IAuthorityProvider
{
    private readonly ConcurrentDictionary<(string AgentId, string Action), AuthorityClaim> _claims = new();

    /// <inheritdoc />
    public Task GrantAsync(AuthorityClaim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);

        foreach (var action in claim.PermittedActions)
        {
            _claims[(claim.GrantedTo, action)] = claim;
        }

        // If no specific actions, store with a wildcard key
        if (claim.PermittedActions.Count == 0)
        {
            _claims[(claim.GrantedTo, "*")] = claim;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RevokeAsync(string agentId, string action, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        _claims.TryRemove((agentId, action), out _);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AuthorityClaim?> GetClaimAsync(string agentId, string action, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        // Check specific action first, then wildcard
        if (_claims.TryGetValue((agentId, action), out var claim))
        {
            if (IsExpired(claim))
            {
                _claims.TryRemove((agentId, action), out _);
                return Task.FromResult<AuthorityClaim?>(null);
            }

            return Task.FromResult<AuthorityClaim?>(claim);
        }

        if (_claims.TryGetValue((agentId, "*"), out var wildcardClaim))
        {
            if (IsExpired(wildcardClaim))
            {
                _claims.TryRemove((agentId, "*"), out _);
                return Task.FromResult<AuthorityClaim?>(null);
            }

            return Task.FromResult<AuthorityClaim?>(wildcardClaim);
        }

        return Task.FromResult<AuthorityClaim?>(null);
    }

    /// <inheritdoc />
    public async Task<bool> HasAuthorityAsync(string agentId, string action, AuthorityTier minimumTier, CancellationToken cancellationToken = default)
    {
        var claim = await GetClaimAsync(agentId, action, cancellationToken);

        return claim is not null && claim.Tier >= minimumTier;
    }

    private static bool IsExpired(AuthorityClaim claim) =>
        claim.ExpiresAt.HasValue && claim.ExpiresAt.Value < DateTimeOffset.UtcNow;
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryAuthorityProviderTests" --verbosity normal`
Expected: All 7 tests pass

**Step 5: Commit**

```bash
git add src/Cortex.Core/Authority/InMemoryAuthorityProvider.cs tests/Cortex.Core.Tests/Authority/InMemoryAuthorityProviderTests.cs
git commit -m "feat(authority): InMemoryAuthorityProvider with grant, get, has authority (#8)"
```

---

### Task 3: InMemoryAuthorityProvider — expiry and revoke

**Files:**
- Modify: `tests/Cortex.Core.Tests/Authority/InMemoryAuthorityProviderTests.cs`

**Step 1: Write the failing tests**

Add to the existing test class:

```csharp
    [Fact]
    public async Task GetClaimAsync_ExpiredClaim_ReturnsNull()
    {
        var claim = CreateClaim(expiresAt: DateTimeOffset.UtcNow.AddHours(-1));
        await _provider.GrantAsync(claim);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.Null(result);
    }

    [Fact]
    public async Task HasAuthorityAsync_ExpiredClaim_ReturnsFalse()
    {
        var claim = CreateClaim(expiresAt: DateTimeOffset.UtcNow.AddHours(-1));
        await _provider.GrantAsync(claim);

        var result = await _provider.HasAuthorityAsync("agent-1", "send-email", AuthorityTier.JustDoIt);

        Assert.False(result);
    }

    [Fact]
    public async Task GetClaimAsync_NotYetExpired_ReturnsClaim()
    {
        var claim = CreateClaim(expiresAt: DateTimeOffset.UtcNow.AddHours(1));
        await _provider.GrantAsync(claim);

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task RevokeAsync_RemovesClaim()
    {
        await _provider.GrantAsync(CreateClaim());

        await _provider.RevokeAsync("agent-1", "send-email");

        var result = await _provider.GetClaimAsync("agent-1", "send-email");
        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAsync_NonexistentClaim_DoesNotThrow()
    {
        await _provider.RevokeAsync("agent-1", "nonexistent");
        // Should not throw
    }

    [Fact]
    public async Task GrantAsync_OverwritesPreviousClaim()
    {
        await _provider.GrantAsync(CreateClaim(tier: AuthorityTier.JustDoIt));
        await _provider.GrantAsync(CreateClaim(tier: AuthorityTier.AskMeFirst));

        var result = await _provider.GetClaimAsync("agent-1", "send-email");

        Assert.NotNull(result);
        Assert.Equal(AuthorityTier.AskMeFirst, result.Tier);
    }
```

**Step 2: Run tests to verify they pass (implementation already handles these cases)**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~InMemoryAuthorityProviderTests" --verbosity normal`
Expected: All 13 tests pass

**Step 3: Commit**

```bash
git add tests/Cortex.Core.Tests/Authority/InMemoryAuthorityProviderTests.cs
git commit -m "test(authority): expiry and revoke coverage for InMemoryAuthorityProvider (#8)"
```

---

### Task 4: Authority validation in AgentHarness

**Files:**
- Modify: `src/Cortex.Agents/AgentHarness.cs`
- Create: `tests/Cortex.Agents.Tests/AgentHarnessAuthorityTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class AgentHarnessAuthorityTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _registry = new();
    private readonly InMemoryAuthorityProvider _authorityProvider = new();

    private AgentHarness CreateHarness(IAgent? agent = null, IAuthorityProvider? provider = null) =>
        new(
            agent ?? new EchoAgent(),
            _bus,
            _registry,
            NullLogger<AgentHarness>.Instance,
            provider);

    private static MessageEnvelope CreateEnvelope(
        string content = "test",
        string? replyTo = null,
        IReadOnlyList<AuthorityClaim>? claims = null) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo },
            AuthorityClaims = claims ?? []
        };

    [Fact]
    public async Task HandleMessage_ValidClaim_ProcessesMessage()
    {
        var claim = new AuthorityClaim
        {
            GrantedBy = "founder",
            GrantedTo = "echo-agent",
            Tier = AuthorityTier.DoItAndShowMe,
            PermittedActions = ["process"],
            GrantedAt = DateTimeOffset.UtcNow
        };
        await _authorityProvider.GrantAsync(claim);

        var harness = CreateHarness(provider: _authorityProvider);
        await harness.StartAsync();

        var replyReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("reply-queue", e =>
        {
            replyReceived.SetResult(e);
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(
            CreateEnvelope("hello", replyTo: "reply-queue", claims: [claim]),
            "agent.echo-agent");

        var reply = await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(reply);
    }

    [Fact]
    public async Task HandleMessage_ExpiredClaim_DropsMessage()
    {
        var claim = new AuthorityClaim
        {
            GrantedBy = "founder",
            GrantedTo = "echo-agent",
            Tier = AuthorityTier.DoItAndShowMe,
            PermittedActions = ["process"],
            GrantedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

        var processed = false;
        var agent = new CallbackAgent("echo-agent", e =>
        {
            processed = true;
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var harness = CreateHarness(agent: agent, provider: _authorityProvider);
        await harness.StartAsync();

        await _bus.PublishAsync(
            CreateEnvelope("hello", claims: [claim]),
            "agent.echo-agent");

        await Task.Delay(200);
        Assert.False(processed);
    }

    [Fact]
    public async Task HandleMessage_ClaimGrantedToDifferentAgent_DropsMessage()
    {
        var claim = new AuthorityClaim
        {
            GrantedBy = "founder",
            GrantedTo = "other-agent",
            Tier = AuthorityTier.DoItAndShowMe,
            PermittedActions = ["process"],
            GrantedAt = DateTimeOffset.UtcNow
        };

        var processed = false;
        var agent = new CallbackAgent("echo-agent", e =>
        {
            processed = true;
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var harness = CreateHarness(agent: agent, provider: _authorityProvider);
        await harness.StartAsync();

        await _bus.PublishAsync(
            CreateEnvelope("hello", claims: [claim]),
            "agent.echo-agent");

        await Task.Delay(200);
        Assert.False(processed);
    }

    [Fact]
    public async Task HandleMessage_NoClaims_ProcessesMessage()
    {
        // Messages with no claims should pass through — authority is optional
        var harness = CreateHarness(provider: _authorityProvider);
        await harness.StartAsync();

        var replyReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("reply-queue", e =>
        {
            replyReceived.SetResult(e);
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(
            CreateEnvelope("hello", replyTo: "reply-queue"),
            "agent.echo-agent");

        var reply = await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(reply);
    }

    [Fact]
    public async Task HandleMessage_NoProvider_ProcessesMessage()
    {
        // Backward compatibility: no IAuthorityProvider injected = no validation
        var harness = CreateHarness(provider: null);
        await harness.StartAsync();

        var claim = new AuthorityClaim
        {
            GrantedBy = "founder",
            GrantedTo = "echo-agent",
            Tier = AuthorityTier.DoItAndShowMe,
            PermittedActions = ["process"],
            GrantedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) // expired, but no provider to check
        };

        var replyReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("reply-queue", e =>
        {
            replyReceived.SetResult(e);
            return Task.CompletedTask;
        });

        await _bus.PublishAsync(
            CreateEnvelope("hello", replyTo: "reply-queue", claims: [claim]),
            "agent.echo-agent");

        var reply = await replyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(reply);
    }

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

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~AgentHarnessAuthorityTests" --verbosity normal`
Expected: Build failure — AgentHarness constructor doesn't accept IAuthorityProvider

**Step 3: Modify AgentHarness to accept optional IAuthorityProvider and validate claims**

Update `src/Cortex.Agents/AgentHarness.cs`:

The constructor gains an optional `IAuthorityProvider? authorityProvider = null` parameter. The `HandleMessageAsync` method validates claims before calling `_agent.ProcessAsync`.

Validation rules:
1. If no `IAuthorityProvider` injected, skip validation (backward compatible)
2. If envelope has no claims, allow through (claims are optional)
3. For each claim: reject if expired (`ExpiresAt < now`), reject if `GrantedTo` doesn't match agent ID

```csharp
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Messaging;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents;

/// <summary>
/// Connects a single <see cref="IAgent"/> to its message queue.
/// Handles message dispatch, reply routing, FromAgentId stamping, authority validation, and lifecycle management.
/// Stores a per-consumer <see cref="IAsyncDisposable"/> handle so stopping this harness
/// does not affect other consumers on the shared message bus.
/// </summary>
public sealed class AgentHarness
{
    private readonly IAgent _agent;
    private readonly IMessageBus _messageBus;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ILogger<AgentHarness> _logger;
    private readonly IAuthorityProvider? _authorityProvider;
    private IAsyncDisposable? _consumerHandle;

    /// <summary>
    /// Creates a new <see cref="AgentHarness"/> for the specified agent.
    /// </summary>
    public AgentHarness(
        IAgent agent,
        IMessageBus messageBus,
        IAgentRegistry agentRegistry,
        ILogger<AgentHarness> logger,
        IAuthorityProvider? authorityProvider = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(agentRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _agent = agent;
        _messageBus = messageBus;
        _agentRegistry = agentRegistry;
        _logger = logger;
        _authorityProvider = authorityProvider;
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

        _consumerHandle = await _messageBus.StartConsumingAsync(
            QueueName, HandleMessageAsync, cancellationToken);

        IsRunning = true;

        _logger.LogInformation(
            "Agent {AgentId} started, consuming from {QueueName}",
            _agent.AgentId, QueueName);
    }

    /// <summary>
    /// Stops the harness: disposes consumer handle and marks the agent as unavailable.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_consumerHandle is not null)
        {
            await _consumerHandle.DisposeAsync();
            _consumerHandle = null;
        }

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

        if (!ValidateAuthorityClaims(envelope))
        {
            _logger.LogWarning(
                "Agent {AgentId} rejected message {MessageId}: authority validation failed",
                _agent.AgentId, envelope.Message.MessageId);
            return;
        }

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

        // Carry forward reference code, set parent message ID, and stamp sender identity
        var replyEnvelope = response with
        {
            ReferenceCode = envelope.ReferenceCode,
            Context = response.Context with
            {
                ParentMessageId = envelope.Message.MessageId,
                FromAgentId = _agent.AgentId
            }
        };

        await _messageBus.PublishAsync(replyEnvelope, replyTo);

        _logger.LogDebug(
            "Agent {AgentId} published reply to {ReplyTo}",
            _agent.AgentId, replyTo);
    }

    private bool ValidateAuthorityClaims(MessageEnvelope envelope)
    {
        // No provider = no validation (backward compatible)
        if (_authorityProvider is null)
        {
            return true;
        }

        // No claims = allow through (claims are optional)
        if (envelope.AuthorityClaims.Count == 0)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var claim in envelope.AuthorityClaims)
        {
            // Reject expired claims
            if (claim.ExpiresAt.HasValue && claim.ExpiresAt.Value < now)
            {
                _logger.LogWarning(
                    "Claim from {GrantedBy} to {GrantedTo} expired at {ExpiresAt}",
                    claim.GrantedBy, claim.GrantedTo, claim.ExpiresAt);
                return false;
            }

            // Reject claims granted to a different agent
            if (!string.Equals(claim.GrantedTo, _agent.AgentId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Claim granted to {GrantedTo} but agent is {AgentId}",
                    claim.GrantedTo, _agent.AgentId);
                return false;
            }
        }

        return true;
    }
}
```

**Step 4: Update AgentRuntime to pass IAuthorityProvider to harnesses**

Modify `src/Cortex.Agents/AgentRuntime.cs` — add optional `IAuthorityProvider?` constructor parameter, pass it through to `AgentHarness`:

In constructor, add `IAuthorityProvider? authorityProvider = null` parameter and store as `_authorityProvider`.

In `StartAgentInternalAsync`, change the harness creation:
```csharp
var harness = new AgentHarness(
    agent,
    _messageBus,
    _agentRegistry,
    _loggerFactory.CreateLogger<AgentHarness>(),
    _authorityProvider);
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~AgentHarnessAuthorityTests" --verbosity normal`
Expected: All 5 tests pass

**Step 6: Run all existing tests to check backward compatibility**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass — existing code uses the 4-param constructor which still works

**Step 7: Commit**

```bash
git add src/Cortex.Agents/AgentHarness.cs src/Cortex.Agents/AgentRuntime.cs tests/Cortex.Agents.Tests/AgentHarnessAuthorityTests.cs
git commit -m "feat(authority): authority claim validation in AgentHarness (#8)"
```

---

## Feature #27 — AskMeFirst Plan Approval Gating

### Task 5: PlanProposal and PlanApprovalResponse message types

**Files:**
- Create: `src/Cortex.Core/Messages/PlanProposal.cs`
- Create: `src/Cortex.Core/Messages/PlanApprovalResponse.cs`
- Create: `tests/Cortex.Core.Tests/Messages/PlanProposalTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Core.Tests.Messages;

public sealed class PlanProposalTests
{
    [Fact]
    public void PlanProposal_ImplementsIMessage()
    {
        var proposal = new PlanProposal
        {
            Summary = "Onboard new client",
            TaskDescriptions = ["Verify identity", "Create CRM record", "Send welcome email"],
            OriginalGoal = "Please onboard Acme Corp",
            WorkflowReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

        Assert.IsAssignableFrom<IMessage>(proposal);
        Assert.NotNull(proposal.MessageId);
        Assert.NotEqual(default, proposal.Timestamp);
    }

    [Fact]
    public void PlanProposal_CarriesTaskDescriptions()
    {
        var tasks = new[] { "Task A", "Task B", "Task C" };
        var proposal = new PlanProposal
        {
            Summary = "Test plan",
            TaskDescriptions = tasks,
            OriginalGoal = "Test goal",
            WorkflowReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

        Assert.Equal(3, proposal.TaskDescriptions.Count);
    }

    [Fact]
    public void PlanApprovalResponse_Approved()
    {
        var response = new PlanApprovalResponse
        {
            IsApproved = true,
            WorkflowReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

        Assert.IsAssignableFrom<IMessage>(response);
        Assert.True(response.IsApproved);
        Assert.Null(response.RejectionReason);
    }

    [Fact]
    public void PlanApprovalResponse_Rejected_WithReason()
    {
        var response = new PlanApprovalResponse
        {
            IsApproved = false,
            RejectionReason = "Too risky",
            WorkflowReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

        Assert.False(response.IsApproved);
        Assert.Equal("Too risky", response.RejectionReason);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~PlanProposalTests" --verbosity normal`
Expected: Build failure — types don't exist

**Step 3: Write the implementations**

`src/Cortex.Core/Messages/PlanProposal.cs`:

```csharp
using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Proposes a decomposition plan for human approval. Sent when AskMeFirst authority is in effect.
/// </summary>
public sealed record PlanProposal : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Brief summary of the proposed plan.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Descriptions of each proposed task in the plan.
    /// </summary>
    public required IReadOnlyList<string> TaskDescriptions { get; init; }

    /// <summary>
    /// The original goal that triggered this plan.
    /// </summary>
    public required string OriginalGoal { get; init; }

    /// <summary>
    /// Reference code for the workflow this plan belongs to.
    /// </summary>
    public required ReferenceCode WorkflowReferenceCode { get; init; }
}
```

`src/Cortex.Core/Messages/PlanApprovalResponse.cs`:

```csharp
using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Human response to a <see cref="PlanProposal"/>. Approves or rejects the proposed plan.
/// </summary>
public sealed record PlanApprovalResponse : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Whether the plan was approved.
    /// </summary>
    public required bool IsApproved { get; init; }

    /// <summary>
    /// Reason for rejection, if not approved.
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// Reference code of the workflow whose plan is being responded to.
    /// </summary>
    public required ReferenceCode WorkflowReferenceCode { get; init; }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~PlanProposalTests" --verbosity normal`
Expected: All 4 tests pass

**Step 5: Commit**

```bash
git add src/Cortex.Core/Messages/PlanProposal.cs src/Cortex.Core/Messages/PlanApprovalResponse.cs tests/Cortex.Core.Tests/Messages/PlanProposalTests.cs
git commit -m "feat(messages): PlanProposal and PlanApprovalResponse message types (#27)"
```

---

### Task 6: IPendingPlanStore and InMemoryPendingPlanStore

**Files:**
- Create: `src/Cortex.Agents/PendingPlan.cs`
- Create: `src/Cortex.Agents/IPendingPlanStore.cs`
- Create: `src/Cortex.Agents/InMemoryPendingPlanStore.cs`
- Create: `tests/Cortex.Agents.Tests/InMemoryPendingPlanStoreTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Agents.Pipeline;
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Agents.Tests;

public sealed class InMemoryPendingPlanStoreTests
{
    private readonly InMemoryPendingPlanStore _store = new();

    private static PendingPlan CreatePlan(ReferenceCode? refCode = null) =>
        new()
        {
            OriginalEnvelope = new MessageEnvelope
            {
                Message = new TestMessage { Content = "test" },
                ReferenceCode = refCode ?? ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
            },
            Decomposition = new DecompositionResult
            {
                Tasks =
                [
                    new DecompositionTask
                    {
                        Capability = "email-drafting",
                        Description = "Draft reply",
                        AuthorityTier = "AskMeFirst"
                    }
                ],
                Summary = "Test plan",
                Confidence = 0.9
            },
            StoredAt = DateTimeOffset.UtcNow
        };

    [Fact]
    public async Task StoreAsync_ThenGetAsync_ReturnsPlan()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        var plan = CreatePlan(refCode);

        await _store.StoreAsync(refCode, plan);
        var result = await _store.GetAsync(refCode);

        Assert.NotNull(result);
        Assert.Equal("Test plan", result.Decomposition.Summary);
    }

    [Fact]
    public async Task GetAsync_NoPlan_ReturnsNull()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 99);

        var result = await _store.GetAsync(refCode);

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_RemovesPlan()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1);
        await _store.StoreAsync(refCode, CreatePlan(refCode));

        await _store.RemoveAsync(refCode);
        var result = await _store.GetAsync(refCode);

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveAsync_NonexistentPlan_DoesNotThrow()
    {
        var refCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 99);

        await _store.RemoveAsync(refCode);
        // Should not throw
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~InMemoryPendingPlanStoreTests" --verbosity normal`
Expected: Build failure — types don't exist

**Step 3: Write the implementations**

`src/Cortex.Agents/PendingPlan.cs`:

```csharp
using Cortex.Agents.Pipeline;
using Cortex.Core.Messages;

namespace Cortex.Agents;

/// <summary>
/// A decomposition plan awaiting human approval before dispatch.
/// </summary>
public sealed record PendingPlan
{
    /// <summary>
    /// The original inbound message envelope that triggered this plan.
    /// </summary>
    public required MessageEnvelope OriginalEnvelope { get; init; }

    /// <summary>
    /// The decomposition result from the skill pipeline.
    /// </summary>
    public required DecompositionResult Decomposition { get; init; }

    /// <summary>
    /// When this plan was stored.
    /// </summary>
    public required DateTimeOffset StoredAt { get; init; }
}
```

`src/Cortex.Agents/IPendingPlanStore.cs`:

```csharp
using Cortex.Core.References;

namespace Cortex.Agents;

/// <summary>
/// Stores decomposition plans that are awaiting human approval (AskMeFirst gate).
/// </summary>
public interface IPendingPlanStore
{
    /// <summary>
    /// Stores a pending plan keyed by workflow reference code.
    /// </summary>
    Task StoreAsync(ReferenceCode referenceCode, PendingPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a pending plan by workflow reference code, or null if not found.
    /// </summary>
    Task<PendingPlan?> GetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a pending plan after approval or rejection.
    /// </summary>
    Task RemoveAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);
}
```

`src/Cortex.Agents/InMemoryPendingPlanStore.cs`:

```csharp
using System.Collections.Concurrent;
using Cortex.Core.References;

namespace Cortex.Agents;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IPendingPlanStore"/>.
/// </summary>
public sealed class InMemoryPendingPlanStore : IPendingPlanStore
{
    private readonly ConcurrentDictionary<string, PendingPlan> _plans = new();

    /// <inheritdoc />
    public Task StoreAsync(ReferenceCode referenceCode, PendingPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _plans[referenceCode.Value] = plan;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PendingPlan?> GetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        _plans.TryGetValue(referenceCode.Value, out var plan);
        return Task.FromResult(plan);
    }

    /// <inheritdoc />
    public Task RemoveAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        _plans.TryRemove(referenceCode.Value, out _);
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~InMemoryPendingPlanStoreTests" --verbosity normal`
Expected: All 4 tests pass

**Step 5: Commit**

```bash
git add src/Cortex.Agents/PendingPlan.cs src/Cortex.Agents/IPendingPlanStore.cs src/Cortex.Agents/InMemoryPendingPlanStore.cs tests/Cortex.Agents.Tests/InMemoryPendingPlanStoreTests.cs
git commit -m "feat(approval): IPendingPlanStore and InMemoryPendingPlanStore (#27)"
```

---

### Task 7: AskMeFirst gating in SkillDrivenAgent

**Files:**
- Modify: `src/Cortex.Agents/SkillDrivenAgent.cs`
- Create: `tests/Cortex.Agents.Tests/SkillDrivenAgentApprovalTests.cs`

**Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Tests.Pipeline;
using Cortex.Agents.Workflows;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Core.Workflows;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class SkillDrivenAgentApprovalTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;
    private readonly InMemoryPendingPlanStore _pendingPlanStore = new();
    private readonly InMemoryWorkflowTracker _workflowTracker = new();

    public SkillDrivenAgentApprovalTests()
    {
        _refCodeGenerator = new SequentialReferenceCodeGenerator(
            new InMemorySequenceStore(), TimeProvider.System);
    }

    private SkillDrivenAgent CreateAgent(PersonaDefinition? persona = null)
    {
        var p = persona ?? CreateDefaultPersona();
        var pipelineRunner = new SkillPipelineRunner(
            _skillRegistry,
            [_fakeExecutor],
            NullLogger<SkillPipelineRunner>.Instance);

        return new SkillDrivenAgent(
            p,
            pipelineRunner,
            _agentRegistry,
            _delegationTracker,
            _refCodeGenerator,
            _bus,
            NullLogger<SkillDrivenAgent>.Instance,
            pendingPlanStore: _pendingPlanStore,
            workflowTracker: _workflowTracker);
    }

    private static PersonaDefinition CreateDefaultPersona() => new()
    {
        AgentId = "cos",
        Name = "Chief of Staff",
        AgentType = "ai",
        Capabilities =
        [
            new AgentCapability { Name = "triage", Description = "Triage" }
        ],
        Pipeline = ["cos-decompose"],
        EscalationTarget = "agent.founder",
        ConfidenceThreshold = 0.6
    };

    private static MessageEnvelope CreateEnvelope(
        string content = "test",
        string? replyTo = null,
        AuthorityTier tier = AuthorityTier.AskMeFirst) =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = replyTo },
            AuthorityClaims =
            [
                new AuthorityClaim
                {
                    GrantedBy = "founder",
                    GrantedTo = "cos",
                    Tier = tier,
                    GrantedAt = DateTimeOffset.UtcNow
                }
            ]
        };

    private void RegisterDecomposeSkill()
    {
        _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-decompose",
            Name = "CoS Decompose",
            Description = "Decompose",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        }).GetAwaiter().GetResult();
    }

    private void SetDecompositionResult(
        string capability = "email-drafting",
        string authorityTier = "AskMeFirst",
        double confidence = 0.9)
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            tasks = new[]
            {
                new { capability, description = "Test task", authorityTier }
            },
            summary = "Test plan",
            confidence
        });
        _fakeExecutor.SetResult("cos-decompose", json);
    }

    private async Task RegisterSpecialistAgent(string agentId, string capabilityName)
    {
        await _agentRegistry.RegisterAsync(new AgentRegistration
        {
            AgentId = agentId,
            Name = $"Agent {agentId}",
            AgentType = "ai",
            Capabilities =
            [
                new AgentCapability { Name = capabilityName, Description = capabilityName }
            ],
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = true
        });
    }

    // --- AskMeFirst gating ---

    [Fact]
    public async Task ProcessAsync_AskMeFirst_PublishesPlanProposal()
    {
        RegisterDecomposeSkill();
        SetDecompositionResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope(replyTo: "agent.human"));

        var proposal = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<PlanProposal>(proposal.Message);
    }

    [Fact]
    public async Task ProcessAsync_AskMeFirst_StoresPendingPlan()
    {
        RegisterDecomposeSkill();
        SetDecompositionResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope(replyTo: "agent.human"));

        // Verify a pending plan was stored (we can't easily get the ref code, but we can check the store is non-empty)
        // The proposal message carries the workflow reference code
        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        // Re-process to capture the proposal
        var agent2 = CreateAgent();
        await agent2.ProcessAsync(CreateEnvelope(replyTo: "agent.human"));
        var envelope = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = (PlanProposal)envelope.Message;

        var plan = await _pendingPlanStore.GetAsync(proposal.WorkflowReferenceCode);
        Assert.NotNull(plan);
    }

    [Fact]
    public async Task ProcessAsync_AskMeFirst_DoesNotDispatchTasks()
    {
        RegisterDecomposeSkill();
        SetDecompositionResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var dispatched = false;
        await _bus.StartConsumingAsync("agent.email-agent", _ =>
        {
            dispatched = true;
            return Task.CompletedTask;
        });
        await _bus.StartConsumingAsync("agent.founder", _ => Task.CompletedTask);

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope(replyTo: "agent.human"));

        await Task.Delay(200);
        Assert.False(dispatched);
    }

    [Fact]
    public async Task ProcessAsync_JustDoIt_DispatchesWithoutGating()
    {
        RegisterDecomposeSkill();
        SetDecompositionResult(authorityTier: "JustDoIt");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var dispatched = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            dispatched.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope(tier: AuthorityTier.JustDoIt));

        var msg = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    [Fact]
    public async Task ProcessAsync_DoItAndShowMe_DispatchesWithoutGating()
    {
        RegisterDecomposeSkill();
        SetDecompositionResult(authorityTier: "DoItAndShowMe");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var dispatched = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            dispatched.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope(tier: AuthorityTier.DoItAndShowMe));

        var msg = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    // --- Approval handling ---

    [Fact]
    public async Task ProcessAsync_ApprovalResponse_ResumesDispatch()
    {
        RegisterDecomposeSkill();
        SetDecompositionResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        // Listen for proposal and dispatch
        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var dispatched = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            dispatched.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();

        // Step 1: Process original message (AskMeFirst) — should gate
        await agent.ProcessAsync(CreateEnvelope(replyTo: "agent.human"));
        var proposalEnvelope = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = (PlanProposal)proposalEnvelope.Message;

        // Step 2: Send approval — should resume dispatch
        var approvalEnvelope = new MessageEnvelope
        {
            Message = new PlanApprovalResponse
            {
                IsApproved = true,
                WorkflowReferenceCode = proposal.WorkflowReferenceCode
            },
            ReferenceCode = proposal.WorkflowReferenceCode,
            Context = new MessageContext
            {
                ParentMessageId = proposalEnvelope.Message.MessageId,
                FromAgentId = "founder"
            }
        };

        await agent.ProcessAsync(approvalEnvelope);

        var dispatchedMsg = await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(dispatchedMsg);
    }

    [Fact]
    public async Task ProcessAsync_RejectionResponse_DoesNotDispatch()
    {
        RegisterDecomposeSkill();
        SetDecompositionResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope(replyTo: "agent.human"));
        var proposalEnvelope = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = (PlanProposal)proposalEnvelope.Message;

        // Listen for rejection summary to original requester
        var rejectionSent = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.human", e =>
        {
            rejectionSent.SetResult(e);
            return Task.CompletedTask;
        });

        var rejectionEnvelope = new MessageEnvelope
        {
            Message = new PlanApprovalResponse
            {
                IsApproved = false,
                RejectionReason = "Too risky",
                WorkflowReferenceCode = proposal.WorkflowReferenceCode
            },
            ReferenceCode = proposal.WorkflowReferenceCode,
            Context = new MessageContext
            {
                ParentMessageId = proposalEnvelope.Message.MessageId,
                FromAgentId = "founder"
            }
        };

        await agent.ProcessAsync(rejectionEnvelope);

        var rejection = await rejectionSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(rejection);
        Assert.IsType<TextMessage>(rejection.Message);
    }

    [Fact]
    public async Task ProcessAsync_RejectionResponse_RemovesPendingPlan()
    {
        RegisterDecomposeSkill();
        SetDecompositionResult();
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var proposalReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            proposalReceived.SetResult(e);
            return Task.CompletedTask;
        });
        await _bus.StartConsumingAsync("agent.human", _ => Task.CompletedTask);

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope(replyTo: "agent.human"));
        var proposalEnvelope = await proposalReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var proposal = (PlanProposal)proposalEnvelope.Message;

        var rejectionEnvelope = new MessageEnvelope
        {
            Message = new PlanApprovalResponse
            {
                IsApproved = false,
                WorkflowReferenceCode = proposal.WorkflowReferenceCode
            },
            ReferenceCode = proposal.WorkflowReferenceCode,
            Context = new MessageContext
            {
                ParentMessageId = proposalEnvelope.Message.MessageId,
                FromAgentId = "founder"
            }
        };

        await agent.ProcessAsync(rejectionEnvelope);

        var plan = await _pendingPlanStore.GetAsync(proposal.WorkflowReferenceCode);
        Assert.Null(plan);
    }

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillDrivenAgentApprovalTests" --verbosity normal`
Expected: Build failure — SkillDrivenAgent constructor doesn't accept `pendingPlanStore`

**Step 3: Modify SkillDrivenAgent to support AskMeFirst gating**

Add to `src/Cortex.Agents/SkillDrivenAgent.cs`:

1. New constructor parameter: `IPendingPlanStore? pendingPlanStore = null`
2. Store as `_pendingPlanStore` field (default to `NullPendingPlanStore` if null)
3. At top of `ProcessAsync`, before the workflow check, add PlanApprovalResponse handling
4. After decomposition extraction, before routing, check if max authority is AskMeFirst — if so, gate

Changes to `ProcessAsync`:
- After extracting decomposition and before the routing `if` block, insert the AskMeFirst gate
- The gate checks `GetMaxAuthorityTier(envelope) >= AuthorityTier.AskMeFirst`
- If gated: generate ref code, store pending plan, publish `PlanProposal` to escalation target, return null

New private method `HandlePlanApprovalAsync`:
- Called when `envelope.Message is PlanApprovalResponse`
- If approved: retrieve pending plan, remove from store, resume routing with stored decomposition and original envelope
- If rejected: remove from store, publish rejection to original `ReplyTo`, return null

Create `src/Cortex.Agents/NullPendingPlanStore.cs` (no-op implementation for backward compatibility):

```csharp
using Cortex.Core.References;

namespace Cortex.Agents;

/// <summary>
/// No-op pending plan store for backward compatibility when approval gating is not needed.
/// </summary>
internal sealed class NullPendingPlanStore : IPendingPlanStore
{
    public Task StoreAsync(ReferenceCode referenceCode, PendingPlan plan, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<PendingPlan?> GetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default) =>
        Task.FromResult<PendingPlan?>(null);

    public Task RemoveAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillDrivenAgentApprovalTests" --verbosity normal`
Expected: All 8 tests pass

**Step 5: Run all existing tests to check backward compatibility**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass — existing code doesn't pass `pendingPlanStore` so gets NullPendingPlanStore

**Step 6: Commit**

```bash
git add src/Cortex.Agents/SkillDrivenAgent.cs src/Cortex.Agents/NullPendingPlanStore.cs tests/Cortex.Agents.Tests/SkillDrivenAgentApprovalTests.cs
git commit -m "feat(approval): AskMeFirst plan approval gating in SkillDrivenAgent (#27)"
```

---

## Feature #28 — Delegation Supervision Service

### Task 8: SupervisionAlert and EscalationAlert message types

**Files:**
- Create: `src/Cortex.Core/Messages/SupervisionAlert.cs`
- Create: `src/Cortex.Core/Messages/EscalationAlert.cs`
- Create: `tests/Cortex.Core.Tests/Messages/SupervisionAlertTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Core.Tests.Messages;

public sealed class SupervisionAlertTests
{
    [Fact]
    public void SupervisionAlert_ImplementsIMessage()
    {
        var alert = new SupervisionAlert
        {
            DelegationReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            DelegatedTo = "agent-1",
            Description = "Draft reply",
            RetryCount = 2,
            DueAt = DateTimeOffset.UtcNow.AddHours(-1),
            IsAgentRunning = true
        };

        Assert.IsAssignableFrom<IMessage>(alert);
        Assert.NotNull(alert.MessageId);
    }

    [Fact]
    public void EscalationAlert_ImplementsIMessage()
    {
        var alert = new EscalationAlert
        {
            DelegationReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            DelegatedTo = "agent-1",
            Description = "Draft reply",
            RetryCount = 3,
            Reason = "Max retries exceeded"
        };

        Assert.IsAssignableFrom<IMessage>(alert);
        Assert.Equal("Max retries exceeded", alert.Reason);
    }

    [Fact]
    public void SupervisionAlert_CarriesDeadAgentFlag()
    {
        var alert = new SupervisionAlert
        {
            DelegationReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            DelegatedTo = "agent-1",
            Description = "Task",
            RetryCount = 1,
            DueAt = DateTimeOffset.UtcNow,
            IsAgentRunning = false
        };

        Assert.False(alert.IsAgentRunning);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~SupervisionAlertTests" --verbosity normal`
Expected: Build failure — types don't exist

**Step 3: Write the implementations**

`src/Cortex.Core/Messages/SupervisionAlert.cs`:

```csharp
using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Alert published when a delegation is overdue. Sent to the CoS for re-dispatch.
/// </summary>
public sealed record SupervisionAlert : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Reference code of the overdue delegation.
    /// </summary>
    public required ReferenceCode DelegationReferenceCode { get; init; }

    /// <summary>
    /// Agent the task was delegated to.
    /// </summary>
    public required string DelegatedTo { get; init; }

    /// <summary>
    /// Description of the overdue task.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Number of times this delegation has been retried.
    /// </summary>
    public required int RetryCount { get; init; }

    /// <summary>
    /// When the delegation was due.
    /// </summary>
    public required DateTimeOffset DueAt { get; init; }

    /// <summary>
    /// Whether the delegated agent is currently running.
    /// </summary>
    public required bool IsAgentRunning { get; init; }
}
```

`src/Cortex.Core/Messages/EscalationAlert.cs`:

```csharp
using Cortex.Core.References;

namespace Cortex.Core.Messages;

/// <summary>
/// Alert published when a delegation has exceeded maximum retry attempts. Requires human intervention.
/// </summary>
public sealed record EscalationAlert : IMessage
{
    /// <inheritdoc />
    public string MessageId { get; init; } = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Reference code of the delegation that exceeded retries.
    /// </summary>
    public required ReferenceCode DelegationReferenceCode { get; init; }

    /// <summary>
    /// Agent the task was delegated to.
    /// </summary>
    public required string DelegatedTo { get; init; }

    /// <summary>
    /// Description of the failed task.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Number of retry attempts made.
    /// </summary>
    public required int RetryCount { get; init; }

    /// <summary>
    /// Reason for escalation.
    /// </summary>
    public required string Reason { get; init; }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Core.Tests --filter "FullyQualifiedName~SupervisionAlertTests" --verbosity normal`
Expected: All 3 tests pass

**Step 5: Commit**

```bash
git add src/Cortex.Core/Messages/SupervisionAlert.cs src/Cortex.Core/Messages/EscalationAlert.cs tests/Cortex.Core.Tests/Messages/SupervisionAlertTests.cs
git commit -m "feat(messages): SupervisionAlert and EscalationAlert message types (#28)"
```

---

### Task 9: IRetryCounter and InMemoryRetryCounter

**Files:**
- Create: `src/Cortex.Agents/Supervision/IRetryCounter.cs`
- Create: `src/Cortex.Agents/Supervision/InMemoryRetryCounter.cs`
- Create: `tests/Cortex.Agents.Tests/Supervision/InMemoryRetryCounterTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Agents.Supervision;
using Cortex.Core.References;

namespace Cortex.Agents.Tests.Supervision;

public sealed class InMemoryRetryCounterTests
{
    private readonly InMemoryRetryCounter _counter = new();

    private static ReferenceCode CreateRef(int seq = 1) =>
        ReferenceCode.Create(DateTimeOffset.UtcNow, seq);

    [Fact]
    public async Task GetCountAsync_NoIncrements_ReturnsZero()
    {
        var count = await _counter.GetCountAsync(CreateRef());

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task IncrementAsync_ReturnsNewCount()
    {
        var refCode = CreateRef();

        var count1 = await _counter.IncrementAsync(refCode);
        var count2 = await _counter.IncrementAsync(refCode);
        var count3 = await _counter.IncrementAsync(refCode);

        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
        Assert.Equal(3, count3);
    }

    [Fact]
    public async Task GetCountAsync_AfterIncrements_ReturnsCurrentCount()
    {
        var refCode = CreateRef();
        await _counter.IncrementAsync(refCode);
        await _counter.IncrementAsync(refCode);

        var count = await _counter.GetCountAsync(refCode);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ResetAsync_ResetsToZero()
    {
        var refCode = CreateRef();
        await _counter.IncrementAsync(refCode);
        await _counter.IncrementAsync(refCode);

        await _counter.ResetAsync(refCode);

        var count = await _counter.GetCountAsync(refCode);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ResetAsync_NonexistentKey_DoesNotThrow()
    {
        await _counter.ResetAsync(CreateRef(99));
        // Should not throw
    }

    [Fact]
    public async Task IndependentRefCodes_TrackSeparately()
    {
        var ref1 = CreateRef(1);
        var ref2 = CreateRef(2);

        await _counter.IncrementAsync(ref1);
        await _counter.IncrementAsync(ref1);
        await _counter.IncrementAsync(ref2);

        Assert.Equal(2, await _counter.GetCountAsync(ref1));
        Assert.Equal(1, await _counter.GetCountAsync(ref2));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~InMemoryRetryCounterTests" --verbosity normal`
Expected: Build failure — types don't exist

**Step 3: Write the implementations**

`src/Cortex.Agents/Supervision/IRetryCounter.cs`:

```csharp
using Cortex.Core.References;

namespace Cortex.Agents.Supervision;

/// <summary>
/// Tracks retry counts for overdue delegations. Separate from DelegationRecord
/// to keep records immutable and counters operational.
/// </summary>
public interface IRetryCounter
{
    /// <summary>
    /// Increments the retry count and returns the new value.
    /// </summary>
    Task<int> IncrementAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current retry count.
    /// </summary>
    Task<int> GetCountAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the retry count to zero.
    /// </summary>
    Task ResetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default);
}
```

`src/Cortex.Agents/Supervision/InMemoryRetryCounter.cs`:

```csharp
using System.Collections.Concurrent;
using Cortex.Core.References;

namespace Cortex.Agents.Supervision;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IRetryCounter"/>.
/// </summary>
public sealed class InMemoryRetryCounter : IRetryCounter
{
    private readonly ConcurrentDictionary<string, int> _counts = new();

    /// <inheritdoc />
    public Task<int> IncrementAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        var newCount = _counts.AddOrUpdate(referenceCode.Value, 1, (_, current) => current + 1);
        return Task.FromResult(newCount);
    }

    /// <inheritdoc />
    public Task<int> GetCountAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        _counts.TryGetValue(referenceCode.Value, out var count);
        return Task.FromResult(count);
    }

    /// <inheritdoc />
    public Task ResetAsync(ReferenceCode referenceCode, CancellationToken cancellationToken = default)
    {
        _counts.TryRemove(referenceCode.Value, out _);
        return Task.CompletedTask;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~InMemoryRetryCounterTests" --verbosity normal`
Expected: All 6 tests pass

**Step 5: Commit**

```bash
git add src/Cortex.Agents/Supervision/IRetryCounter.cs src/Cortex.Agents/Supervision/InMemoryRetryCounter.cs tests/Cortex.Agents.Tests/Supervision/InMemoryRetryCounterTests.cs
git commit -m "feat(supervision): IRetryCounter and InMemoryRetryCounter (#28)"
```

---

### Task 10: DelegationSupervisionService

**Files:**
- Create: `src/Cortex.Agents/Supervision/DelegationSupervisionService.cs`
- Create: `src/Cortex.Agents/Supervision/SupervisionOptions.cs`
- Create: `tests/Cortex.Agents.Tests/Supervision/DelegationSupervisionServiceTests.cs`

**Step 1: Write the failing tests**

```csharp
using Cortex.Agents.Delegation;
using Cortex.Agents.Supervision;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests.Supervision;

public sealed class DelegationSupervisionServiceTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemoryRetryCounter _retryCounter = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private int _sequenceCounter;

    private DelegationSupervisionService CreateService(
        int maxRetries = 3,
        string alertTarget = "agent.cos",
        string escalationTarget = "agent.founder",
        IAgentRuntime? runtime = null) =>
        new(
            _delegationTracker,
            _retryCounter,
            _bus,
            NullLogger<DelegationSupervisionService>.Instance,
            new SupervisionOptions
            {
                MaxRetries = maxRetries,
                AlertTarget = alertTarget,
                EscalationTarget = escalationTarget
            },
            runtime);

    private DelegationRecord CreateOverdueRecord(
        string delegatedTo = "agent-1",
        DelegationStatus status = DelegationStatus.Assigned) =>
        new()
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, Interlocked.Increment(ref _sequenceCounter)),
            DelegatedBy = "cos",
            DelegatedTo = delegatedTo,
            Description = "Overdue task",
            Status = status,
            AssignedAt = DateTimeOffset.UtcNow.AddHours(-2),
            DueAt = DateTimeOffset.UtcNow.AddHours(-1)
        };

    [Fact]
    public async Task CheckOverdueAsync_OverdueDelegation_PublishesSupervisionAlert()
    {
        var record = CreateOverdueRecord();
        await _delegationTracker.DelegateAsync(record);

        var alertReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.cos", e =>
        {
            alertReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var service = CreateService();
        await service.CheckOverdueAsync();

        var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<SupervisionAlert>(alert.Message);

        var supervision = (SupervisionAlert)alert.Message;
        Assert.Equal(record.DelegationReferenceCode, supervision.DelegationReferenceCode);
        Assert.Equal("agent-1", supervision.DelegatedTo);
        Assert.Equal(1, supervision.RetryCount);
    }

    [Fact]
    public async Task CheckOverdueAsync_MaxRetriesExceeded_PublishesEscalationAlert()
    {
        var record = CreateOverdueRecord();
        await _delegationTracker.DelegateAsync(record);

        // Pre-fill retry counter to max
        await _retryCounter.IncrementAsync(record.ReferenceCode);
        await _retryCounter.IncrementAsync(record.ReferenceCode);
        await _retryCounter.IncrementAsync(record.ReferenceCode);

        var escalationReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalationReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var service = CreateService(maxRetries: 3);
        await service.CheckOverdueAsync();

        var alert = await escalationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<EscalationAlert>(alert.Message);

        var escalation = (EscalationAlert)alert.Message;
        Assert.Equal("agent-1", escalation.DelegatedTo);
        Assert.Contains("Max retries exceeded", escalation.Reason);
    }

    [Fact]
    public async Task CheckOverdueAsync_NoOverdueDelegations_PublishesNothing()
    {
        var notOverdue = new DelegationRecord
        {
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            DelegatedBy = "cos",
            DelegatedTo = "agent-1",
            Description = "Not overdue",
            Status = DelegationStatus.Assigned,
            AssignedAt = DateTimeOffset.UtcNow,
            DueAt = DateTimeOffset.UtcNow.AddHours(1) // future
        };
        await _delegationTracker.DelegateAsync(notOverdue);

        var received = false;
        await _bus.StartConsumingAsync("agent.cos", _ =>
        {
            received = true;
            return Task.CompletedTask;
        });

        var service = CreateService();
        await service.CheckOverdueAsync();

        await Task.Delay(200);
        Assert.False(received);
    }

    [Fact]
    public async Task CheckOverdueAsync_IncrementsRetryCounter()
    {
        var record = CreateOverdueRecord();
        await _delegationTracker.DelegateAsync(record);
        await _bus.StartConsumingAsync("agent.cos", _ => Task.CompletedTask);

        var service = CreateService();
        await service.CheckOverdueAsync();
        await service.CheckOverdueAsync();

        var count = await _retryCounter.GetCountAsync(record.ReferenceCode);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CheckOverdueAsync_DeadAgent_AlertIncludesIsAgentRunningFalse()
    {
        var record = CreateOverdueRecord("dead-agent");
        await _delegationTracker.DelegateAsync(record);

        // Create a runtime with no running agents
        var runtime = new FakeAgentRuntime([]);

        var alertReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.cos", e =>
        {
            alertReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var service = CreateService(runtime: runtime);
        await service.CheckOverdueAsync();

        var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var supervision = (SupervisionAlert)alert.Message;
        Assert.False(supervision.IsAgentRunning);
    }

    [Fact]
    public async Task CheckOverdueAsync_RunningAgent_AlertIncludesIsAgentRunningTrue()
    {
        var record = CreateOverdueRecord("running-agent");
        await _delegationTracker.DelegateAsync(record);

        var runtime = new FakeAgentRuntime(["running-agent"]);

        var alertReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.cos", e =>
        {
            alertReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var service = CreateService(runtime: runtime);
        await service.CheckOverdueAsync();

        var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var supervision = (SupervisionAlert)alert.Message;
        Assert.True(supervision.IsAgentRunning);
    }

    [Fact]
    public async Task CheckOverdueAsync_NoRuntime_DefaultsToAgentRunningTrue()
    {
        var record = CreateOverdueRecord();
        await _delegationTracker.DelegateAsync(record);

        var alertReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.cos", e =>
        {
            alertReceived.SetResult(e);
            return Task.CompletedTask;
        });

        var service = CreateService(runtime: null);
        await service.CheckOverdueAsync();

        var alert = await alertReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var supervision = (SupervisionAlert)alert.Message;
        Assert.True(supervision.IsAgentRunning);
    }

    public async ValueTask DisposeAsync()
    {
        await _bus.DisposeAsync();
    }
}

/// <summary>
/// Minimal IAgentRuntime fake for testing — only exposes RunningAgentIds.
/// </summary>
file sealed class FakeAgentRuntime(IReadOnlyList<string> runningAgentIds) : IAgentRuntime
{
    public IReadOnlyList<string> RunningAgentIds { get; } = runningAgentIds;

    public Task<string> StartAgentAsync(IAgent agent, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
    public Task<string> StartAgentAsync(IAgent agent, string teamId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
    public Task StopAgentAsync(string agentId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
    public Task StopTeamAsync(string teamId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
    public IReadOnlyList<string> GetTeamAgentIds(string teamId) =>
        throw new NotImplementedException();
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~DelegationSupervisionServiceTests" --verbosity normal`
Expected: Build failure — types don't exist

**Step 3: Write the implementations**

`src/Cortex.Agents/Supervision/SupervisionOptions.cs`:

```csharp
namespace Cortex.Agents.Supervision;

/// <summary>
/// Configuration for the <see cref="DelegationSupervisionService"/>.
/// </summary>
public sealed record SupervisionOptions
{
    /// <summary>
    /// Interval between supervision checks. Default: 60 seconds.
    /// </summary>
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum retry attempts before escalating. Default: 3.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Queue to publish supervision alerts to. Default: agent.cos.
    /// </summary>
    public string AlertTarget { get; init; } = "agent.cos";

    /// <summary>
    /// Queue to publish escalation alerts to. Default: agent.founder.
    /// </summary>
    public string EscalationTarget { get; init; } = "agent.founder";
}
```

`src/Cortex.Agents/Supervision/DelegationSupervisionService.cs`:

```csharp
using Cortex.Agents.Delegation;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents.Supervision;

/// <summary>
/// Background service that periodically checks for overdue delegations
/// and publishes alert messages. Queries delegation records (cheap, deterministic),
/// not agents (expensive, token-consuming).
/// </summary>
public sealed class DelegationSupervisionService : IHostedService, IDisposable
{
    private readonly IDelegationTracker _delegationTracker;
    private readonly IRetryCounter _retryCounter;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<DelegationSupervisionService> _logger;
    private readonly SupervisionOptions _options;
    private readonly IAgentRuntime? _agentRuntime;
    private PeriodicTimer? _timer;
    private Task? _timerTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Creates a new <see cref="DelegationSupervisionService"/>.
    /// </summary>
    public DelegationSupervisionService(
        IDelegationTracker delegationTracker,
        IRetryCounter retryCounter,
        IMessagePublisher messagePublisher,
        ILogger<DelegationSupervisionService> logger,
        SupervisionOptions options,
        IAgentRuntime? agentRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(delegationTracker);
        ArgumentNullException.ThrowIfNull(retryCounter);
        ArgumentNullException.ThrowIfNull(messagePublisher);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _delegationTracker = delegationTracker;
        _retryCounter = retryCounter;
        _messagePublisher = messagePublisher;
        _logger = logger;
        _options = options;
        _agentRuntime = agentRuntime;
    }

    /// <summary>
    /// Starts the periodic supervision timer.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _timer = new PeriodicTimer(_options.CheckInterval);
        _timerTask = RunTimerLoopAsync(_cts.Token);

        _logger.LogInformation(
            "Delegation supervision started (interval: {Interval}s, max retries: {MaxRetries})",
            _options.CheckInterval.TotalSeconds, _options.MaxRetries);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the periodic supervision timer.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
        }

        if (_timerTask is not null)
        {
            await _timerTask;
        }

        _logger.LogInformation("Delegation supervision stopped");
    }

    /// <summary>
    /// Checks for overdue delegations and publishes alerts.
    /// Exposed for direct testing without the timer.
    /// </summary>
    public async Task CheckOverdueAsync(CancellationToken cancellationToken = default)
    {
        var overdue = await _delegationTracker.GetOverdueAsync(cancellationToken);

        if (overdue.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Supervision check found {Count} overdue delegations", overdue.Count);

        var runningAgentIds = _agentRuntime?.RunningAgentIds ?? [];

        foreach (var record in overdue)
        {
            var retryCount = await _retryCounter.IncrementAsync(record.ReferenceCode, cancellationToken);
            var isAgentRunning = _agentRuntime is null || runningAgentIds.Contains(record.DelegatedTo);

            if (retryCount > _options.MaxRetries)
            {
                await PublishEscalationAlertAsync(record, retryCount, cancellationToken);
            }
            else
            {
                await PublishSupervisionAlertAsync(record, retryCount, isAgentRunning, cancellationToken);
            }
        }
    }

    private async Task PublishSupervisionAlertAsync(
        DelegationRecord record, int retryCount, bool isAgentRunning,
        CancellationToken cancellationToken)
    {
        var alert = new SupervisionAlert
        {
            DelegationReferenceCode = record.ReferenceCode,
            DelegatedTo = record.DelegatedTo,
            Description = record.Description,
            RetryCount = retryCount,
            DueAt = record.DueAt!.Value,
            IsAgentRunning = isAgentRunning
        };

        var envelope = new MessageEnvelope
        {
            Message = alert,
            ReferenceCode = record.ReferenceCode,
            Context = new MessageContext { FromAgentId = "supervision-service" }
        };

        await _messagePublisher.PublishAsync(envelope, _options.AlertTarget, cancellationToken);

        _logger.LogWarning(
            "Supervision alert for {RefCode}: delegated to {Agent}, retry {Retry}, agent running: {Running}",
            record.ReferenceCode, record.DelegatedTo, retryCount, isAgentRunning);
    }

    private async Task PublishEscalationAlertAsync(
        DelegationRecord record, int retryCount,
        CancellationToken cancellationToken)
    {
        var alert = new EscalationAlert
        {
            DelegationReferenceCode = record.ReferenceCode,
            DelegatedTo = record.DelegatedTo,
            Description = record.Description,
            RetryCount = retryCount,
            Reason = $"Max retries exceeded ({_options.MaxRetries})"
        };

        var envelope = new MessageEnvelope
        {
            Message = alert,
            ReferenceCode = record.ReferenceCode,
            Context = new MessageContext { FromAgentId = "supervision-service" }
        };

        await _messagePublisher.PublishAsync(envelope, _options.EscalationTarget, cancellationToken);

        _logger.LogError(
            "Escalation alert for {RefCode}: delegated to {Agent}, max retries ({MaxRetries}) exceeded",
            record.ReferenceCode, record.DelegatedTo, _options.MaxRetries);
    }

    private async Task RunTimerLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_timer is not null && await _timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await CheckOverdueAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error during supervision check");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _timer?.Dispose();
        _cts?.Dispose();
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~DelegationSupervisionServiceTests" --verbosity normal`
Expected: All 7 tests pass

**Step 5: Commit**

```bash
git add src/Cortex.Agents/Supervision/SupervisionOptions.cs src/Cortex.Agents/Supervision/DelegationSupervisionService.cs tests/Cortex.Agents.Tests/Supervision/DelegationSupervisionServiceTests.cs
git commit -m "feat(supervision): DelegationSupervisionService with overdue detection and escalation (#28)"
```

---

### Task 11: Final verification and issue closure

**Step 1: Run full test suite**

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass

**Step 2: Build in Release mode**

Run: `dotnet build --configuration Release`
Expected: Build succeeds with zero warnings

**Step 3: Close GitHub issues**

```bash
gh issue close 8 --comment "Implemented in this branch: InMemoryAuthorityProvider, AgentHarness authority validation, team ceiling support"
gh issue close 27 --comment "Implemented in this branch: PlanProposal/PlanApprovalResponse messages, IPendingPlanStore, AskMeFirst gating in SkillDrivenAgent"
gh issue close 28 --comment "Implemented in this branch: DelegationSupervisionService, IRetryCounter, SupervisionAlert/EscalationAlert messages, dead agent detection"
```

**Step 4: Commit any remaining changes and create PR**
