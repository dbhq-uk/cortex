# Chief of Staff Agent Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the first production agent (Chief of Staff) as a generic skill-driven agent with LLM-assisted triage, capability-based routing, and persona configuration.

**Architecture:** A `SkillDrivenAgent` receives messages, runs a configurable skill pipeline (starting with LLM-assisted triage), routes to specialist agents by capability match, and tracks delegations. The CoS is one persona configuration of this generic agent. A Claude CLI wrapper provides LLM capabilities. Routing logic lives in the agent (not a separate skill) — extract to a skill later if different routing strategies are needed.

**Tech Stack:** .NET 10, C#, xUnit, InMemoryMessageBus for tests, Claude CLI for LLM

**Design doc:** `docs/plans/2026-02-25-chief-of-staff-agent-design.md`

---

## Task 1: Pipeline Data Types

**Files:**
- Create: `src/Cortex.Agents/Pipeline/TriageResult.cs`
- Create: `src/Cortex.Agents/Pipeline/SkillPipelineContext.cs`
- Test: `tests/Cortex.Agents.Tests/Pipeline/TriageResultTests.cs`
- Test: `tests/Cortex.Agents.Tests/Pipeline/SkillPipelineContextTests.cs`

### Step 1: Write the TriageResult test

```csharp
// tests/Cortex.Agents.Tests/Pipeline/TriageResultTests.cs
using Cortex.Agents.Pipeline;
using Cortex.Core.Authority;

namespace Cortex.Agents.Tests.Pipeline;

public sealed class TriageResultTests
{
    [Fact]
    public void Construction_WithAllProperties_Succeeds()
    {
        var result = new TriageResult
        {
            Capability = "email-drafting",
            AuthorityTier = AuthorityTier.DoItAndShowMe,
            Summary = "Draft reply to client email",
            Confidence = 0.92
        };

        Assert.Equal("email-drafting", result.Capability);
        Assert.Equal(AuthorityTier.DoItAndShowMe, result.AuthorityTier);
        Assert.Equal("Draft reply to client email", result.Summary);
        Assert.Equal(0.92, result.Confidence);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new TriageResult
        {
            Capability = "email-drafting",
            AuthorityTier = AuthorityTier.JustDoIt,
            Summary = "Draft reply",
            Confidence = 0.85
        };

        var b = new TriageResult
        {
            Capability = "email-drafting",
            AuthorityTier = AuthorityTier.JustDoIt,
            Summary = "Draft reply",
            Confidence = 0.85
        };

        Assert.Equal(a, b);
    }
}
```

### Step 2: Run test to verify it fails

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~TriageResultTests" --verbosity normal`
Expected: FAIL — `TriageResult` type does not exist.

### Step 3: Write TriageResult implementation

```csharp
// src/Cortex.Agents/Pipeline/TriageResult.cs
using Cortex.Core.Authority;

namespace Cortex.Agents.Pipeline;

/// <summary>
/// Result of an LLM triage skill — the routing recommendation for a message.
/// </summary>
public sealed record TriageResult
{
    /// <summary>
    /// The capability name that should handle this message.
    /// </summary>
    public required string Capability { get; init; }

    /// <summary>
    /// The recommended authority tier for the delegated work.
    /// </summary>
    public required AuthorityTier AuthorityTier { get; init; }

    /// <summary>
    /// A brief summary of what needs to be done.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) in the triage decision.
    /// </summary>
    public required double Confidence { get; init; }
}
```

### Step 4: Run test to verify it passes

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~TriageResultTests" --verbosity normal`
Expected: PASS (2 tests).

### Step 5: Write the SkillPipelineContext test

```csharp
// tests/Cortex.Agents.Tests/Pipeline/SkillPipelineContextTests.cs
using Cortex.Agents.Pipeline;
using Cortex.Core.Messages;
using Cortex.Core.References;

namespace Cortex.Agents.Tests.Pipeline;

public sealed class SkillPipelineContextTests
{
    private static MessageEnvelope CreateEnvelope(string content = "test") =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    [Fact]
    public void Construction_SetsEnvelope()
    {
        var envelope = CreateEnvelope("hello");

        var context = new SkillPipelineContext { Envelope = envelope };

        Assert.Same(envelope, context.Envelope);
    }

    [Fact]
    public void Results_StartsEmpty()
    {
        var context = new SkillPipelineContext { Envelope = CreateEnvelope() };

        Assert.Empty(context.Results);
    }

    [Fact]
    public void Results_AccumulatesSkillOutputs()
    {
        var context = new SkillPipelineContext { Envelope = CreateEnvelope() };

        context.Results["skill-a"] = "output-a";
        context.Results["skill-b"] = 42;

        Assert.Equal(2, context.Results.Count);
        Assert.Equal("output-a", context.Results["skill-a"]);
        Assert.Equal(42, context.Results["skill-b"]);
    }

    [Fact]
    public void Parameters_StartsEmpty()
    {
        var context = new SkillPipelineContext { Envelope = CreateEnvelope() };

        Assert.Empty(context.Parameters);
    }

    [Fact]
    public void Parameters_CanBeSetAtConstruction()
    {
        var parameters = new Dictionary<string, object> { ["key"] = "value" };

        var context = new SkillPipelineContext
        {
            Envelope = CreateEnvelope(),
            Parameters = parameters
        };

        Assert.Equal("value", context.Parameters["key"]);
    }
}
```

### Step 6: Run test to verify it fails

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillPipelineContextTests" --verbosity normal`
Expected: FAIL — `SkillPipelineContext` type does not exist.

### Step 7: Write SkillPipelineContext implementation

```csharp
// src/Cortex.Agents/Pipeline/SkillPipelineContext.cs
using Cortex.Core.Messages;

namespace Cortex.Agents.Pipeline;

/// <summary>
/// Accumulates context as a skill pipeline executes.
/// Each skill receives the full context including results from all prior skills.
/// </summary>
public sealed class SkillPipelineContext
{
    /// <summary>
    /// The original incoming message envelope.
    /// </summary>
    public required MessageEnvelope Envelope { get; init; }

    /// <summary>
    /// Additional parameters available to all skills in the pipeline.
    /// Populated by the agent before pipeline execution.
    /// </summary>
    public IDictionary<string, object> Parameters { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Results from each skill, keyed by skill ID.
    /// </summary>
    public Dictionary<string, object?> Results { get; } = new();
}
```

### Step 8: Run test to verify it passes

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillPipelineContextTests" --verbosity normal`
Expected: PASS (5 tests).

### Step 9: Commit

```bash
git add src/Cortex.Agents/Pipeline/ tests/Cortex.Agents.Tests/Pipeline/
git commit -m "feat: pipeline data types — TriageResult and SkillPipelineContext"
```

---

## Task 2: InMemorySkillRegistry and Cortex.Skills.Tests Project

**Files:**
- Create: `tests/Cortex.Skills.Tests/Cortex.Skills.Tests.csproj`
- Create: `src/Cortex.Skills/InMemorySkillRegistry.cs`
- Modify: `Cortex.slnx` (add test project)
- Test: `tests/Cortex.Skills.Tests/InMemorySkillRegistryTests.cs`

### Step 1: Create the test project

Create `tests/Cortex.Skills.Tests/Cortex.Skills.Tests.csproj`:

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
    <ProjectReference Include="..\..\src\Cortex.Skills\Cortex.Skills.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

Add to `Cortex.slnx` in the `/tests/` folder:

```xml
<Project Path="tests/Cortex.Skills.Tests/Cortex.Skills.Tests.csproj" />
```

### Step 2: Write the InMemorySkillRegistry tests

```csharp
// tests/Cortex.Skills.Tests/InMemorySkillRegistryTests.cs
namespace Cortex.Skills.Tests;

public sealed class InMemorySkillRegistryTests
{
    private readonly InMemorySkillRegistry _registry = new();

    private static SkillDefinition CreateDefinition(
        string skillId = "test-skill",
        string name = "Test Skill",
        SkillCategory category = SkillCategory.Agent,
        string executorType = "csharp") =>
        new()
        {
            SkillId = skillId,
            Name = name,
            Description = $"Description for {name}",
            Category = category,
            ExecutorType = executorType
        };

    [Fact]
    public async Task RegisterAsync_NullDefinition_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _registry.RegisterAsync(null!));
    }

    [Fact]
    public async Task RegisterAsync_ThenFindById_ReturnsDefinition()
    {
        var def = CreateDefinition("triage-skill");
        await _registry.RegisterAsync(def);

        var result = await _registry.FindByIdAsync("triage-skill");

        Assert.NotNull(result);
        Assert.Equal("triage-skill", result.SkillId);
    }

    [Fact]
    public async Task FindByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _registry.FindByIdAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateId_OverwritesDefinition()
    {
        await _registry.RegisterAsync(CreateDefinition("skill-1", name: "First"));
        await _registry.RegisterAsync(CreateDefinition("skill-1", name: "Second"));

        var result = await _registry.FindByIdAsync("skill-1");

        Assert.NotNull(result);
        Assert.Equal("Second", result.Name);
    }

    [Fact]
    public async Task SearchAsync_MatchesName()
    {
        await _registry.RegisterAsync(CreateDefinition("s1", name: "Email Triage"));
        await _registry.RegisterAsync(CreateDefinition("s2", name: "Code Review"));

        var results = await _registry.SearchAsync("email");

        Assert.Single(results);
        Assert.Equal("s1", results[0].SkillId);
    }

    [Fact]
    public async Task SearchAsync_MatchesDescription()
    {
        await _registry.RegisterAsync(CreateDefinition("s1", name: "Triage"));

        var results = await _registry.SearchAsync("Description for Triage");

        Assert.Single(results);
    }

    [Fact]
    public async Task SearchAsync_NoMatch_ReturnsEmpty()
    {
        await _registry.RegisterAsync(CreateDefinition("s1"));

        var results = await _registry.SearchAsync("zzz-no-match");

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindByCategoryAsync_FiltersCorrectly()
    {
        await _registry.RegisterAsync(CreateDefinition("s1", category: SkillCategory.Agent));
        await _registry.RegisterAsync(CreateDefinition("s2", category: SkillCategory.Integration));

        var results = await _registry.FindByCategoryAsync(SkillCategory.Agent);

        Assert.Single(results);
        Assert.Equal("s1", results[0].SkillId);
    }

    [Fact]
    public async Task FindByCategoryAsync_NoMatch_ReturnsEmpty()
    {
        await _registry.RegisterAsync(CreateDefinition("s1", category: SkillCategory.Agent));

        var results = await _registry.FindByCategoryAsync(SkillCategory.Meta);

        Assert.Empty(results);
    }
}
```

### Step 3: Run tests to verify they fail

Run: `dotnet test tests/Cortex.Skills.Tests --verbosity normal`
Expected: FAIL — `InMemorySkillRegistry` type does not exist.

### Step 4: Implement InMemorySkillRegistry

```csharp
// src/Cortex.Skills/InMemorySkillRegistry.cs
using System.Collections.Concurrent;

namespace Cortex.Skills;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ISkillRegistry"/>.
/// </summary>
public sealed class InMemorySkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, SkillDefinition> _skills = new();

    /// <inheritdoc />
    public Task RegisterAsync(SkillDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _skills[definition.SkillId] = definition;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<SkillDefinition?> FindByIdAsync(string skillId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillId);
        _skills.TryGetValue(skillId, out var definition);
        return Task.FromResult(definition);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SkillDefinition>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var matches = _skills.Values
            .Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<SkillDefinition>>(matches);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SkillDefinition>> FindByCategoryAsync(SkillCategory category, CancellationToken cancellationToken = default)
    {
        var matches = _skills.Values
            .Where(s => s.Category == category)
            .ToList();

        return Task.FromResult<IReadOnlyList<SkillDefinition>>(matches);
    }
}
```

### Step 5: Run tests to verify they pass

Run: `dotnet test tests/Cortex.Skills.Tests --verbosity normal`
Expected: PASS (9 tests).

### Step 6: Commit

```bash
git add src/Cortex.Skills/InMemorySkillRegistry.cs tests/Cortex.Skills.Tests/ Cortex.slnx
git commit -m "feat: InMemorySkillRegistry and Cortex.Skills.Tests project"
```

---

## Task 3: SkillPipelineRunner

**Files:**
- Create: `src/Cortex.Agents/Pipeline/SkillPipelineRunner.cs`
- Modify: `src/Cortex.Agents/Cortex.Agents.csproj` (add Cortex.Skills reference)
- Modify: `tests/Cortex.Agents.Tests/Cortex.Agents.Tests.csproj` (add Cortex.Skills reference)
- Create: `tests/Cortex.Agents.Tests/Pipeline/FakeSkillExecutor.cs`
- Test: `tests/Cortex.Agents.Tests/Pipeline/SkillPipelineRunnerTests.cs`

### Step 1: Add project references

Add to `src/Cortex.Agents/Cortex.Agents.csproj`:

```xml
<ProjectReference Include="..\Cortex.Skills\Cortex.Skills.csproj" />
```

Add to `tests/Cortex.Agents.Tests/Cortex.Agents.Tests.csproj`:

```xml
<ProjectReference Include="..\..\src\Cortex.Skills\Cortex.Skills.csproj" />
```

### Step 2: Create FakeSkillExecutor

```csharp
// tests/Cortex.Agents.Tests/Pipeline/FakeSkillExecutor.cs
using Cortex.Skills;

namespace Cortex.Agents.Tests.Pipeline;

/// <summary>
/// Test fake that returns preconfigured results keyed by skill ID.
/// </summary>
public sealed class FakeSkillExecutor : ISkillExecutor
{
    private readonly Dictionary<string, object?> _results = new();
    private readonly List<(string SkillId, IDictionary<string, object> Parameters)> _calls = [];

    /// <inheritdoc />
    public string ExecutorType { get; }

    public FakeSkillExecutor(string executorType = "fake")
    {
        ExecutorType = executorType;
    }

    /// <summary>
    /// Configures the result to return when the specified skill is executed.
    /// </summary>
    public void SetResult(string skillId, object? result)
    {
        _results[skillId] = result;
    }

    /// <summary>
    /// All calls made to this executor: (skillId, parameters).
    /// </summary>
    public IReadOnlyList<(string SkillId, IDictionary<string, object> Parameters)> Calls => _calls;

    /// <inheritdoc />
    public Task<object?> ExecuteAsync(
        SkillDefinition skill,
        IDictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        _calls.Add((skill.SkillId, new Dictionary<string, object>(parameters)));
        _results.TryGetValue(skill.SkillId, out var result);
        return Task.FromResult(result);
    }
}
```

### Step 3: Write the SkillPipelineRunner tests

```csharp
// tests/Cortex.Agents.Tests/Pipeline/SkillPipelineRunnerTests.cs
using Cortex.Agents.Pipeline;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests.Pipeline;

public sealed class SkillPipelineRunnerTests
{
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("fake");

    private SkillPipelineRunner CreateRunner(params ISkillExecutor[] executors)
    {
        var allExecutors = executors.Length > 0 ? executors : [_fakeExecutor];
        return new SkillPipelineRunner(
            _skillRegistry,
            allExecutors,
            NullLogger<SkillPipelineRunner>.Instance);
    }

    private static MessageEnvelope CreateEnvelope(string content = "test") =>
        new()
        {
            Message = new TestMessage { Content = content },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1)
        };

    private SkillDefinition RegisterSkill(
        string skillId = "test-skill",
        string executorType = "fake")
    {
        var def = new SkillDefinition
        {
            SkillId = skillId,
            Name = skillId,
            Description = $"Test skill {skillId}",
            Category = SkillCategory.Agent,
            ExecutorType = executorType
        };
        _skillRegistry.RegisterAsync(def).GetAwaiter().GetResult();
        return def;
    }

    [Fact]
    public async Task RunAsync_EmptyPipeline_ReturnsContextWithNoResults()
    {
        var runner = CreateRunner();
        var envelope = CreateEnvelope();

        var context = await runner.RunAsync([], envelope);

        Assert.Same(envelope, context.Envelope);
        Assert.Empty(context.Results);
    }

    [Fact]
    public async Task RunAsync_SingleSkill_ExecutesAndStoresResult()
    {
        RegisterSkill("triage");
        _fakeExecutor.SetResult("triage", "triage-output");
        var runner = CreateRunner();

        var context = await runner.RunAsync(["triage"], CreateEnvelope());

        Assert.Single(context.Results);
        Assert.Equal("triage-output", context.Results["triage"]);
    }

    [Fact]
    public async Task RunAsync_MultipleSkills_ExecutesInOrder()
    {
        RegisterSkill("skill-a");
        RegisterSkill("skill-b");
        _fakeExecutor.SetResult("skill-a", "output-a");
        _fakeExecutor.SetResult("skill-b", "output-b");
        var runner = CreateRunner();

        var context = await runner.RunAsync(["skill-a", "skill-b"], CreateEnvelope());

        Assert.Equal(2, context.Results.Count);
        Assert.Equal("output-a", context.Results["skill-a"]);
        Assert.Equal("output-b", context.Results["skill-b"]);

        // Verify execution order
        Assert.Equal("skill-a", _fakeExecutor.Calls[0].SkillId);
        Assert.Equal("skill-b", _fakeExecutor.Calls[1].SkillId);
    }

    [Fact]
    public async Task RunAsync_LaterSkillReceivesPriorResults()
    {
        RegisterSkill("skill-a");
        RegisterSkill("skill-b");
        _fakeExecutor.SetResult("skill-a", "output-a");
        var runner = CreateRunner();

        await runner.RunAsync(["skill-a", "skill-b"], CreateEnvelope());

        // Second skill should receive first skill's result in the parameters
        var secondCallParams = _fakeExecutor.Calls[1].Parameters;
        var results = (Dictionary<string, object?>)secondCallParams["results"];
        Assert.Equal("output-a", results["skill-a"]);
    }

    [Fact]
    public async Task RunAsync_UnknownSkill_SkipsWithoutError()
    {
        RegisterSkill("known");
        _fakeExecutor.SetResult("known", "known-output");
        var runner = CreateRunner();

        var context = await runner.RunAsync(["unknown", "known"], CreateEnvelope());

        Assert.Single(context.Results);
        Assert.Equal("known-output", context.Results["known"]);
    }

    [Fact]
    public async Task RunAsync_NoMatchingExecutor_SkipsWithoutError()
    {
        var def = new SkillDefinition
        {
            SkillId = "orphan",
            Name = "Orphan",
            Description = "No executor for this type",
            Category = SkillCategory.Agent,
            ExecutorType = "nonexistent-executor"
        };
        await _skillRegistry.RegisterAsync(def);
        var runner = CreateRunner();

        var context = await runner.RunAsync(["orphan"], CreateEnvelope());

        Assert.Empty(context.Results);
    }

    [Fact]
    public async Task RunAsync_AdditionalParameters_AvailableToSkills()
    {
        RegisterSkill("triage");
        var runner = CreateRunner();
        var extraParams = new Dictionary<string, object>
        {
            ["availableCapabilities"] = "email-drafting, code-review"
        };

        await runner.RunAsync(["triage"], CreateEnvelope(), extraParams);

        var callParams = _fakeExecutor.Calls[0].Parameters;
        Assert.Equal("email-drafting, code-review", callParams["availableCapabilities"]);
    }

    [Fact]
    public async Task RunAsync_EnvelopePassedInParameters()
    {
        RegisterSkill("triage");
        var runner = CreateRunner();
        var envelope = CreateEnvelope("hello world");

        await runner.RunAsync(["triage"], envelope);

        var callParams = _fakeExecutor.Calls[0].Parameters;
        Assert.Same(envelope, callParams["envelope"]);
    }
}
```

### Step 4: Run tests to verify they fail

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillPipelineRunnerTests" --verbosity normal`
Expected: FAIL — `SkillPipelineRunner` type does not exist.

### Step 5: Implement SkillPipelineRunner

```csharp
// src/Cortex.Agents/Pipeline/SkillPipelineRunner.cs
using Cortex.Core.Messages;
using Cortex.Skills;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents.Pipeline;

/// <summary>
/// Executes an ordered list of skills, passing context between them.
/// Each skill receives the original envelope, additional parameters, and all prior skill results.
/// </summary>
public sealed class SkillPipelineRunner
{
    private readonly ISkillRegistry _skillRegistry;
    private readonly IReadOnlyDictionary<string, ISkillExecutor> _executors;
    private readonly ILogger<SkillPipelineRunner> _logger;

    /// <summary>
    /// Creates a new <see cref="SkillPipelineRunner"/>.
    /// </summary>
    public SkillPipelineRunner(
        ISkillRegistry skillRegistry,
        IEnumerable<ISkillExecutor> executors,
        ILogger<SkillPipelineRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(skillRegistry);
        ArgumentNullException.ThrowIfNull(executors);
        ArgumentNullException.ThrowIfNull(logger);

        _skillRegistry = skillRegistry;
        _executors = executors.ToDictionary(e => e.ExecutorType);
        _logger = logger;
    }

    /// <summary>
    /// Runs the skill pipeline and returns the accumulated context.
    /// </summary>
    public async Task<SkillPipelineContext> RunAsync(
        IReadOnlyList<string> skillIds,
        MessageEnvelope envelope,
        IDictionary<string, object>? additionalParameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skillIds);
        ArgumentNullException.ThrowIfNull(envelope);

        var context = new SkillPipelineContext
        {
            Envelope = envelope,
            Parameters = additionalParameters ?? new Dictionary<string, object>()
        };

        foreach (var skillId in skillIds)
        {
            var definition = await _skillRegistry.FindByIdAsync(skillId, cancellationToken);
            if (definition is null)
            {
                _logger.LogWarning("Skill {SkillId} not found in registry, skipping", skillId);
                continue;
            }

            if (!_executors.TryGetValue(definition.ExecutorType, out var executor))
            {
                _logger.LogWarning(
                    "No executor for type {ExecutorType}, skipping skill {SkillId}",
                    definition.ExecutorType, skillId);
                continue;
            }

            var parameters = new Dictionary<string, object>(context.Parameters)
            {
                ["envelope"] = context.Envelope,
                ["results"] = context.Results
            };

            var result = await executor.ExecuteAsync(definition, parameters, cancellationToken);
            context.Results[skillId] = result;

            _logger.LogDebug("Skill {SkillId} completed", skillId);
        }

        return context;
    }
}
```

### Step 6: Run tests to verify they pass

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillPipelineRunnerTests" --verbosity normal`
Expected: PASS (8 tests).

### Step 7: Commit

```bash
git add src/Cortex.Agents/Pipeline/SkillPipelineRunner.cs src/Cortex.Agents/Cortex.Agents.csproj tests/Cortex.Agents.Tests/Cortex.Agents.Tests.csproj tests/Cortex.Agents.Tests/Pipeline/FakeSkillExecutor.cs tests/Cortex.Agents.Tests/Pipeline/SkillPipelineRunnerTests.cs
git commit -m "feat: SkillPipelineRunner — ordered skill execution with context flow"
```

---

## Task 4: PersonaDefinition and PersonaParser

**Files:**
- Create: `src/Cortex.Agents/Personas/PersonaDefinition.cs`
- Create: `src/Cortex.Agents/Personas/PersonaParser.cs`
- Test: `tests/Cortex.Agents.Tests/Personas/PersonaParserTests.cs`

### Step 1: Write the PersonaParser tests

```csharp
// tests/Cortex.Agents.Tests/Personas/PersonaParserTests.cs
using Cortex.Agents.Personas;

namespace Cortex.Agents.Tests.Personas;

public sealed class PersonaParserTests
{
    private const string ValidPersona = """
        # Chief of Staff

        ## Identity
        - **agent-id**: cos
        - **name**: Chief of Staff
        - **type**: ai

        ## Capabilities
        - triage: Analyses incoming messages and determines routing
        - routing: Routes messages to specialist agents by capability

        ## Pipeline
        1. cos-triage

        ## Configuration
        - **escalation-target**: agent.founder
        - **model-tier**: balanced
        - **confidence-threshold**: 0.6
        """;

    [Fact]
    public void Parse_ValidMarkdown_ExtractsAgentId()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("cos", result.AgentId);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsName()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("Chief of Staff", result.Name);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsAgentType()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("ai", result.AgentType);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsCapabilities()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal(2, result.Capabilities.Count);
        Assert.Equal("triage", result.Capabilities[0].Name);
        Assert.Equal("Analyses incoming messages and determines routing", result.Capabilities[0].Description);
        Assert.Equal("routing", result.Capabilities[1].Name);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsPipeline()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Single(result.Pipeline);
        Assert.Equal("cos-triage", result.Pipeline[0]);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsEscalationTarget()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("agent.founder", result.EscalationTarget);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsModelTier()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal("balanced", result.ModelTier);
    }

    [Fact]
    public void Parse_ValidMarkdown_ExtractsConfidenceThreshold()
    {
        var result = PersonaParser.Parse(ValidPersona);

        Assert.Equal(0.6, result.ConfidenceThreshold);
    }

    [Fact]
    public void Parse_MultiplePipelineSteps_ExtractsAll()
    {
        var markdown = """
            # Multi-Step Agent

            ## Identity
            - **agent-id**: multi
            - **name**: Multi
            - **type**: ai

            ## Capabilities
            - analysis: Analyses things

            ## Pipeline
            1. step-one
            2. step-two
            3. step-three

            ## Configuration
            - **escalation-target**: agent.founder
            """;

        var result = PersonaParser.Parse(markdown);

        Assert.Equal(3, result.Pipeline.Count);
        Assert.Equal("step-one", result.Pipeline[0]);
        Assert.Equal("step-two", result.Pipeline[1]);
        Assert.Equal("step-three", result.Pipeline[2]);
    }

    [Fact]
    public void Parse_MissingConfidenceThreshold_DefaultsTo06()
    {
        var markdown = """
            # Minimal Agent

            ## Identity
            - **agent-id**: minimal
            - **name**: Minimal
            - **type**: ai

            ## Capabilities
            - work: Does work

            ## Pipeline
            1. do-work

            ## Configuration
            - **escalation-target**: agent.founder
            """;

        var result = PersonaParser.Parse(markdown);

        Assert.Equal(0.6, result.ConfidenceThreshold);
    }

    [Fact]
    public void Parse_EmptyMarkdown_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => PersonaParser.Parse(""));
    }

    [Fact]
    public void Parse_NullMarkdown_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => PersonaParser.Parse(null!));
    }
}
```

### Step 2: Run tests to verify they fail

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~PersonaParserTests" --verbosity normal`
Expected: FAIL — types do not exist.

### Step 3: Implement PersonaDefinition

```csharp
// src/Cortex.Agents/Personas/PersonaDefinition.cs
namespace Cortex.Agents.Personas;

/// <summary>
/// Parsed persona configuration that defines an agent's identity, capabilities, and skill pipeline.
/// Loaded from a persona markdown file.
/// </summary>
public sealed record PersonaDefinition
{
    /// <summary>
    /// Unique identifier for this agent.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Agent type: "ai" or "human".
    /// </summary>
    public required string AgentType { get; init; }

    /// <summary>
    /// Capabilities this agent possesses.
    /// </summary>
    public required IReadOnlyList<AgentCapability> Capabilities { get; init; }

    /// <summary>
    /// Ordered list of skill IDs that form this agent's processing pipeline.
    /// </summary>
    public required IReadOnlyList<string> Pipeline { get; init; }

    /// <summary>
    /// Queue name to publish to when a message cannot be routed.
    /// </summary>
    public required string EscalationTarget { get; init; }

    /// <summary>
    /// The model tier for LLM operations: "lightweight", "balanced", or "heavyweight".
    /// </summary>
    public string ModelTier { get; init; } = "balanced";

    /// <summary>
    /// Minimum confidence score required to route a triage result. Below this, escalate.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.6;
}
```

### Step 4: Implement PersonaParser

```csharp
// src/Cortex.Agents/Personas/PersonaParser.cs
using System.Globalization;
using System.Text.RegularExpressions;

namespace Cortex.Agents.Personas;

/// <summary>
/// Parses persona markdown files into <see cref="PersonaDefinition"/> records.
/// </summary>
public static partial class PersonaParser
{
    /// <summary>
    /// Parses a persona markdown string into a <see cref="PersonaDefinition"/>.
    /// </summary>
    public static PersonaDefinition Parse(string markdown)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markdown);

        var sections = SplitSections(markdown);

        if (!sections.TryGetValue("identity", out var identityLines))
        {
            throw new FormatException("Persona markdown must contain an '## Identity' section.");
        }

        var identity = ParseKeyValues(identityLines);
        var capabilities = sections.TryGetValue("capabilities", out var capLines)
            ? ParseCapabilities(capLines)
            : [];
        var pipeline = sections.TryGetValue("pipeline", out var pipeLines)
            ? ParsePipeline(pipeLines)
            : [];
        var config = sections.TryGetValue("configuration", out var configLines)
            ? ParseKeyValues(configLines)
            : new Dictionary<string, string>();

        return new PersonaDefinition
        {
            AgentId = identity.GetValueOrDefault("agent-id")
                ?? throw new FormatException("Identity section must contain 'agent-id'."),
            Name = identity.GetValueOrDefault("name")
                ?? throw new FormatException("Identity section must contain 'name'."),
            AgentType = identity.GetValueOrDefault("type")
                ?? throw new FormatException("Identity section must contain 'type'."),
            Capabilities = capabilities,
            Pipeline = pipeline,
            EscalationTarget = config.GetValueOrDefault("escalation-target")
                ?? throw new FormatException("Configuration section must contain 'escalation-target'."),
            ModelTier = config.GetValueOrDefault("model-tier") ?? "balanced",
            ConfidenceThreshold = config.TryGetValue("confidence-threshold", out var ct)
                ? double.Parse(ct, CultureInfo.InvariantCulture)
                : 0.6
        };
    }

    /// <summary>
    /// Parses a persona from a markdown file on disk.
    /// </summary>
    public static async Task<PersonaDefinition> ParseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Parse(content);
    }

    private static Dictionary<string, List<string>> SplitSections(string markdown)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                currentSection = line[3..].Trim().ToLowerInvariant();
                sections[currentSection] = [];
            }
            else if (currentSection is not null && !string.IsNullOrWhiteSpace(line))
            {
                sections[currentSection].Add(line);
            }
        }

        return sections;
    }

    private static Dictionary<string, string> ParseKeyValues(List<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var match = BoldKeyValuePattern().Match(line);
            if (match.Success)
            {
                result[match.Groups["key"].Value.Trim()] = match.Groups["value"].Value.Trim();
            }
        }

        return result;
    }

    private static List<AgentCapability> ParseCapabilities(List<string> lines)
    {
        var capabilities = new List<AgentCapability>();

        foreach (var line in lines)
        {
            var match = CapabilityPattern().Match(line);
            if (match.Success)
            {
                capabilities.Add(new AgentCapability
                {
                    Name = match.Groups["name"].Value.Trim(),
                    Description = match.Groups["desc"].Value.Trim()
                });
            }
        }

        return capabilities;
    }

    private static List<string> ParsePipeline(List<string> lines)
    {
        var pipeline = new List<string>();

        foreach (var line in lines)
        {
            var match = PipelineStepPattern().Match(line);
            if (match.Success)
            {
                pipeline.Add(match.Groups["skill"].Value.Trim());
            }
        }

        return pipeline;
    }

    [GeneratedRegex(@"^-\s+\*\*(?<key>[^*]+)\*\*:\s*(?<value>.+)$")]
    private static partial Regex BoldKeyValuePattern();

    [GeneratedRegex(@"^-\s+(?<name>[^:]+):\s*(?<desc>.+)$")]
    private static partial Regex CapabilityPattern();

    [GeneratedRegex(@"^\d+\.\s+(?<skill>.+)$")]
    private static partial Regex PipelineStepPattern();
}
```

### Step 5: Run tests to verify they pass

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~PersonaParserTests" --verbosity normal`
Expected: PASS (12 tests).

### Step 6: Commit

```bash
git add src/Cortex.Agents/Personas/ tests/Cortex.Agents.Tests/Personas/
git commit -m "feat: PersonaDefinition and PersonaParser — markdown persona config"
```

---

## Task 5: SkillDrivenAgent — Routing, Escalation, Authority Narrowing

**Files:**
- Create: `src/Cortex.Agents/SkillDrivenAgent.cs`
- Test: `tests/Cortex.Agents.Tests/SkillDrivenAgentTests.cs`

### Step 1: Write the routing happy-path test

```csharp
// tests/Cortex.Agents.Tests/SkillDrivenAgentTests.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Tests.Pipeline;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

public sealed class SkillDrivenAgentTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;

    public SkillDrivenAgentTests()
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
            NullLogger<SkillDrivenAgent>.Instance);
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
        Pipeline = ["cos-triage"],
        EscalationTarget = "agent.founder",
        ConfidenceThreshold = 0.6
    };

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

    private void RegisterTriageSkill()
    {
        _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "CoS Triage",
            Description = "Triage",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        }).GetAwaiter().GetResult();
    }

    private void SetTriageResult(
        string capability,
        string authorityTier = "DoItAndShowMe",
        double confidence = 0.9,
        string summary = "Test task")
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            capability,
            authorityTier,
            summary,
            confidence
        });
        _fakeExecutor.SetResult("cos-triage", json);
    }

    private async Task RegisterSpecialistAgent(
        string agentId,
        string capabilityName)
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

    // --- Routing happy path ---

    [Fact]
    public async Task ProcessAsync_RoutesToMatchingAgent()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        var result = await agent.ProcessAsync(CreateEnvelope("Draft reply to John"));

        Assert.Null(result);

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(routedMsg);
    }

    [Fact]
    public async Task ProcessAsync_StampsFromAgentId()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("cos", routedMsg.Context.FromAgentId);
    }

    [Fact]
    public async Task ProcessAsync_PreservesReplyTo()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test", replyTo: "agent.human-user"));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("agent.human-user", routedMsg.Context.ReplyTo);
    }

    [Fact]
    public async Task ProcessAsync_CreatesDelegationRecord()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting", summary: "Draft reply");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var delegations = await _delegationTracker.GetByAssigneeAsync("email-agent");
        Assert.Single(delegations);
        Assert.Equal("cos", delegations[0].DelegatedBy);
        Assert.Equal("email-agent", delegations[0].DelegatedTo);
        Assert.Equal("Draft reply", delegations[0].Description);
        Assert.Equal(DelegationStatus.Assigned, delegations[0].Status);
    }

    [Fact]
    public async Task ProcessAsync_ExcludesSelfFromRouting()
    {
        RegisterTriageSkill();
        SetTriageResult("triage");

        // Register the CoS itself with the "triage" capability and another agent
        await _agentRegistry.RegisterAsync(new AgentRegistration
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = "triage", Description = "Triage" }],
            RegisteredAt = DateTimeOffset.UtcNow,
            IsAvailable = true
        });
        await RegisterSpecialistAgent("other-agent", "triage");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.other-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(routedMsg);
    }

    // --- Escalation ---

    [Fact]
    public async Task ProcessAsync_NoTriageResult_EscalatesToFounder()
    {
        RegisterTriageSkill();
        // Don't set any triage result — executor returns null

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    [Fact]
    public async Task ProcessAsync_LowConfidence_EscalatesToFounder()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting", confidence: 0.3); // below 0.6 threshold
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    [Fact]
    public async Task ProcessAsync_NoMatchingCapability_EscalatesToFounder()
    {
        RegisterTriageSkill();
        SetTriageResult("nonexistent-capability");
        // Don't register any agent with that capability

        var escalated = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            escalated.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var msg = await escalated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(msg);
    }

    [Fact]
    public async Task ProcessAsync_Escalation_CreatesDelegationRecord()
    {
        RegisterTriageSkill();
        SetTriageResult("nonexistent-capability");

        await _bus.StartConsumingAsync("agent.founder", _ => Task.CompletedTask);

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test"));

        var delegations = await _delegationTracker.GetByAssigneeAsync("agent.founder");
        Assert.Single(delegations);
        Assert.Contains("Escalated", delegations[0].Description);
    }

    // --- Authority narrowing ---

    [Fact]
    public async Task ProcessAsync_AuthorityNarrowing_NeverExceedsInbound()
    {
        RegisterTriageSkill();
        // Triage suggests AskMeFirst, but inbound only has DoItAndShowMe
        SetTriageResult("email-drafting", authorityTier: "AskMeFirst");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var inboundClaim = new AuthorityClaim
        {
            GrantedBy = "founder",
            GrantedTo = "cos",
            Tier = AuthorityTier.DoItAndShowMe,
            GrantedAt = DateTimeOffset.UtcNow
        };

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test", claims: [inboundClaim]));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var outboundClaim = Assert.Single(routedMsg.AuthorityClaims);
        Assert.Equal(AuthorityTier.DoItAndShowMe, outboundClaim.Tier);
    }

    [Fact]
    public async Task ProcessAsync_NoInboundClaims_DefaultsToJustDoIt()
    {
        RegisterTriageSkill();
        SetTriageResult("email-drafting", authorityTier: "AskMeFirst");
        await RegisterSpecialistAgent("email-agent", "email-drafting");

        var routed = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.email-agent", e =>
        {
            routed.SetResult(e);
            return Task.CompletedTask;
        });

        var agent = CreateAgent();
        await agent.ProcessAsync(CreateEnvelope("test", claims: []));

        var routedMsg = await routed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var outboundClaim = Assert.Single(routedMsg.AuthorityClaims);
        Assert.Equal(AuthorityTier.JustDoIt, outboundClaim.Tier);
    }

    // --- Agent identity ---

    [Fact]
    public void AgentId_ComesFromPersona()
    {
        var agent = CreateAgent();

        Assert.Equal("cos", agent.AgentId);
    }

    [Fact]
    public void Name_ComesFromPersona()
    {
        var agent = CreateAgent();

        Assert.Equal("Chief of Staff", agent.Name);
    }

    [Fact]
    public void Capabilities_ComesFromPersona()
    {
        var agent = CreateAgent();

        Assert.Single(agent.Capabilities);
        Assert.Equal("triage", agent.Capabilities[0].Name);
    }

    [Fact]
    public void AgentType_ComesFromPersona()
    {
        var agent = CreateAgent();
        var typed = Assert.IsAssignableFrom<IAgentTypeProvider>(agent);

        Assert.Equal("ai", typed.AgentType);
    }

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}
```

### Step 2: Run tests to verify they fail

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillDrivenAgentTests" --verbosity normal`
Expected: FAIL — `SkillDrivenAgent` type does not exist.

### Step 3: Implement SkillDrivenAgent

```csharp
// src/Cortex.Agents/SkillDrivenAgent.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Microsoft.Extensions.Logging;

namespace Cortex.Agents;

/// <summary>
/// Generic agent that processes messages through a configurable skill pipeline.
/// Identity, capabilities, and pipeline are defined by a <see cref="PersonaDefinition"/>.
/// Any persona (CoS, analyst, drafter) is an instance of this class with different config.
/// </summary>
public sealed class SkillDrivenAgent : IAgent, IAgentTypeProvider
{
    private readonly PersonaDefinition _persona;
    private readonly SkillPipelineRunner _pipelineRunner;
    private readonly IAgentRegistry _agentRegistry;
    private readonly IDelegationTracker _delegationTracker;
    private readonly IReferenceCodeGenerator _referenceCodeGenerator;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<SkillDrivenAgent> _logger;

    /// <summary>
    /// Creates a new <see cref="SkillDrivenAgent"/> with the given persona and dependencies.
    /// </summary>
    public SkillDrivenAgent(
        PersonaDefinition persona,
        SkillPipelineRunner pipelineRunner,
        IAgentRegistry agentRegistry,
        IDelegationTracker delegationTracker,
        IReferenceCodeGenerator referenceCodeGenerator,
        IMessagePublisher messagePublisher,
        ILogger<SkillDrivenAgent> logger)
    {
        ArgumentNullException.ThrowIfNull(persona);
        ArgumentNullException.ThrowIfNull(pipelineRunner);
        ArgumentNullException.ThrowIfNull(agentRegistry);
        ArgumentNullException.ThrowIfNull(delegationTracker);
        ArgumentNullException.ThrowIfNull(referenceCodeGenerator);
        ArgumentNullException.ThrowIfNull(messagePublisher);
        ArgumentNullException.ThrowIfNull(logger);

        _persona = persona;
        _pipelineRunner = pipelineRunner;
        _agentRegistry = agentRegistry;
        _delegationTracker = delegationTracker;
        _referenceCodeGenerator = referenceCodeGenerator;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public string AgentId => _persona.AgentId;

    /// <inheritdoc />
    public string Name => _persona.Name;

    /// <inheritdoc />
    public IReadOnlyList<AgentCapability> Capabilities => _persona.Capabilities;

    /// <inheritdoc />
    public string AgentType => _persona.AgentType;

    /// <inheritdoc />
    public async Task<MessageEnvelope?> ProcessAsync(
        MessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Agent {AgentId} processing message {MessageId}",
            AgentId, envelope.Message.MessageId);

        // Build parameters for the pipeline
        var capabilityNames = await GetAvailableCapabilitiesAsync(cancellationToken);
        var messageContent = JsonSerializer.Serialize(
            envelope.Message, envelope.Message.GetType());
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = messageContent,
            ["availableCapabilities"] = string.Join(", ", capabilityNames)
        };

        // Run the skill pipeline
        var context = await _pipelineRunner.RunAsync(
            _persona.Pipeline, envelope, parameters, cancellationToken);

        // Extract triage result from pipeline output
        var triageResult = ExtractTriageResult(context);

        if (triageResult is null || triageResult.Confidence < _persona.ConfidenceThreshold)
        {
            var reason = triageResult is null ? "No triage result" : "Low confidence";
            await EscalateAsync(envelope, reason, cancellationToken);
            return null;
        }

        // Find a matching agent (excluding self)
        var candidates = await _agentRegistry.FindByCapabilityAsync(
            triageResult.Capability, cancellationToken);
        var filtered = candidates.Where(a => a.AgentId != AgentId).ToList();

        if (filtered.Count == 0)
        {
            await EscalateAsync(
                envelope,
                $"No agent with capability '{triageResult.Capability}'",
                cancellationToken);
            return null;
        }

        var target = filtered[0];

        // Authority narrowing: outbound never exceeds inbound
        var maxInbound = GetMaxAuthorityTier(envelope);
        var effectiveTier = (AuthorityTier)Math.Min(
            (int)triageResult.AuthorityTier, (int)maxInbound);

        // Track delegation
        var refCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);
        await _delegationTracker.DelegateAsync(new DelegationRecord
        {
            ReferenceCode = refCode,
            DelegatedBy = AgentId,
            DelegatedTo = target.AgentId,
            Description = triageResult.Summary,
            Status = DelegationStatus.Assigned,
            AssignedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        // Build and publish the routed envelope
        var routedEnvelope = envelope with
        {
            ReferenceCode = refCode,
            AuthorityClaims =
            [
                new AuthorityClaim
                {
                    GrantedBy = AgentId,
                    GrantedTo = target.AgentId,
                    Tier = effectiveTier,
                    GrantedAt = DateTimeOffset.UtcNow
                }
            ],
            Context = envelope.Context with
            {
                ParentMessageId = envelope.Message.MessageId,
                FromAgentId = AgentId
            }
        };

        await _messagePublisher.PublishAsync(
            routedEnvelope, $"agent.{target.AgentId}", cancellationToken);

        _logger.LogInformation(
            "Routed {RefCode} to {TargetAgent} (capability: {Capability}, authority: {Authority})",
            refCode, target.AgentId, triageResult.Capability, effectiveTier);

        return null;
    }

    private async Task EscalateAsync(
        MessageEnvelope envelope,
        string reason,
        CancellationToken cancellationToken)
    {
        var refCode = await _referenceCodeGenerator.GenerateAsync(cancellationToken);

        await _delegationTracker.DelegateAsync(new DelegationRecord
        {
            ReferenceCode = refCode,
            DelegatedBy = AgentId,
            DelegatedTo = _persona.EscalationTarget,
            Description = $"Escalated: {reason}",
            Status = DelegationStatus.Assigned,
            AssignedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        var escalatedEnvelope = envelope with
        {
            ReferenceCode = refCode,
            Context = envelope.Context with
            {
                ParentMessageId = envelope.Message.MessageId,
                FromAgentId = AgentId
            }
        };

        await _messagePublisher.PublishAsync(
            escalatedEnvelope, _persona.EscalationTarget, cancellationToken);

        _logger.LogWarning(
            "Escalated {RefCode} to {Target}: {Reason}",
            refCode, _persona.EscalationTarget, reason);
    }

    private async Task<IReadOnlyList<string>> GetAvailableCapabilitiesAsync(
        CancellationToken cancellationToken)
    {
        // Query all capabilities from all known agents, excluding self
        var agents = new List<AgentRegistration>();

        // FindByCapabilityAsync filters by specific capability; we need all capabilities.
        // Use a broad search: check each known capability.
        // For Phase 1, collect from all running agents.
        // This is a workaround until IAgentRegistry exposes GetAllAsync.
        foreach (var cap in _persona.Capabilities)
        {
            var matches = await _agentRegistry.FindByCapabilityAsync(cap.Name, cancellationToken);
            agents.AddRange(matches);
        }

        // Also query commonly-known capabilities — in Phase 1, we rely on
        // the triage skill to determine capability from message content.
        // The available capabilities list is informational for the LLM prompt.
        return agents
            .Where(a => a.AgentId != AgentId)
            .SelectMany(a => a.Capabilities)
            .Select(c => c.Name)
            .Distinct()
            .ToList();
    }

    private static TriageResult? ExtractTriageResult(SkillPipelineContext context)
    {
        foreach (var result in context.Results.Values)
        {
            if (result is not JsonElement json)
            {
                continue;
            }

            try
            {
                var capability = json.GetProperty("capability").GetString();
                var authorityStr = json.GetProperty("authorityTier").GetString();
                var summary = json.GetProperty("summary").GetString();
                var confidence = json.GetProperty("confidence").GetDouble();

                if (capability is null || authorityStr is null || summary is null)
                {
                    continue;
                }

                if (!Enum.TryParse<AuthorityTier>(authorityStr, ignoreCase: true, out var authorityTier))
                {
                    continue;
                }

                return new TriageResult
                {
                    Capability = capability,
                    AuthorityTier = authorityTier,
                    Summary = summary,
                    Confidence = confidence
                };
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
            {
                continue;
            }
        }

        return null;
    }

    private static AuthorityTier GetMaxAuthorityTier(MessageEnvelope envelope)
    {
        if (envelope.AuthorityClaims.Count == 0)
        {
            return AuthorityTier.JustDoIt;
        }

        return envelope.AuthorityClaims.Max(c => c.Tier);
    }
}
```

### Step 4: Run all tests to verify they pass

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillDrivenAgentTests" --verbosity normal`
Expected: PASS (14 tests).

### Step 5: Run the full test suite to check for regressions

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass.

### Step 6: Commit

```bash
git add src/Cortex.Agents/SkillDrivenAgent.cs tests/Cortex.Agents.Tests/SkillDrivenAgentTests.cs
git commit -m "feat: SkillDrivenAgent — generic skill-driven agent with routing, escalation, authority narrowing"
```

---

## Task 6: ILlmClient Interface

**Files:**
- Create: `src/Cortex.Skills/ILlmClient.cs`
- Create: `tests/Cortex.Skills.Tests/FakeLlmClient.cs`

### Step 1: Create ILlmClient interface

```csharp
// src/Cortex.Skills/ILlmClient.cs
namespace Cortex.Skills;

/// <summary>
/// Abstraction for language model completions.
/// Implementations may wrap a CLI, API, or local model.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends a prompt and returns the completion text.
    /// </summary>
    Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
}
```

### Step 2: Create FakeLlmClient for tests

```csharp
// tests/Cortex.Skills.Tests/FakeLlmClient.cs
using Cortex.Skills;

namespace Cortex.Skills.Tests;

/// <summary>
/// Test fake that returns preconfigured LLM responses.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Queue<string> _responses = new();
    private string _defaultResponse = "{}";
    private readonly List<string> _prompts = [];

    /// <summary>
    /// All prompts sent to this client.
    /// </summary>
    public IReadOnlyList<string> Prompts => _prompts;

    /// <summary>
    /// Sets the default response for all calls.
    /// </summary>
    public void SetDefaultResponse(string response)
    {
        _defaultResponse = response;
    }

    /// <summary>
    /// Enqueues a response to return on the next call.
    /// </summary>
    public void EnqueueResponse(string response)
    {
        _responses.Enqueue(response);
    }

    /// <inheritdoc />
    public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
    {
        _prompts.Add(prompt);
        var response = _responses.Count > 0 ? _responses.Dequeue() : _defaultResponse;
        return Task.FromResult(response);
    }
}
```

### Step 3: Commit

```bash
git add src/Cortex.Skills/ILlmClient.cs tests/Cortex.Skills.Tests/FakeLlmClient.cs
git commit -m "feat: ILlmClient interface and FakeLlmClient test helper"
```

---

## Task 7: ClaudeCliClient

**Files:**
- Create: `src/Cortex.Skills/ClaudeCliClient.cs`
- Create: `src/Cortex.Skills/ClaudeCliOptions.cs`
- Modify: `src/Cortex.Skills/Cortex.Skills.csproj` (add logging package)
- Test: `tests/Cortex.Skills.Tests/ClaudeCliClientTests.cs`

### Step 1: Add logging package to Cortex.Skills

Add to `src/Cortex.Skills/Cortex.Skills.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.3" />
```

### Step 2: Write ClaudeCliClient tests

```csharp
// tests/Cortex.Skills.Tests/ClaudeCliClientTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cortex.Skills.Tests;

public sealed class ClaudeCliClientTests
{
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var options = Options.Create(new ClaudeCliOptions());

        Assert.Throws<ArgumentNullException>(() =>
            new ClaudeCliClient(null!, options));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ClaudeCliClient(NullLogger<ClaudeCliClient>.Instance, null!));
    }

    [Fact]
    public async Task CompleteAsync_NullPrompt_ThrowsArgumentException()
    {
        var client = new ClaudeCliClient(
            NullLogger<ClaudeCliClient>.Instance,
            Options.Create(new ClaudeCliOptions()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.CompleteAsync(null!));
    }

    [Fact]
    public async Task CompleteAsync_EmptyPrompt_ThrowsArgumentException()
    {
        var client = new ClaudeCliClient(
            NullLogger<ClaudeCliClient>.Instance,
            Options.Create(new ClaudeCliOptions()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.CompleteAsync(""));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompleteAsync_SimplePrompt_ReturnsNonEmptyResponse()
    {
        var client = new ClaudeCliClient(
            NullLogger<ClaudeCliClient>.Instance,
            Options.Create(new ClaudeCliOptions { TimeoutSeconds = 60 }));

        var result = await client.CompleteAsync("Respond with exactly: hello");

        Assert.False(string.IsNullOrWhiteSpace(result));
    }
}
```

Add package reference for `Microsoft.Extensions.Options` to test project:

```xml
<PackageReference Include="Microsoft.Extensions.Options" Version="10.0.3" />
```

### Step 3: Implement ClaudeCliOptions

```csharp
// src/Cortex.Skills/ClaudeCliOptions.cs
namespace Cortex.Skills;

/// <summary>
/// Configuration options for the <see cref="ClaudeCliClient"/>.
/// </summary>
public sealed class ClaudeCliOptions
{
    /// <summary>
    /// Timeout in seconds for each CLI invocation.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 120;

    /// <summary>
    /// Path to the Claude CLI executable. Defaults to "claude" (found via PATH).
    /// </summary>
    public string CliPath { get; init; } = "claude";
}
```

### Step 4: Implement ClaudeCliClient

```csharp
// src/Cortex.Skills/ClaudeCliClient.cs
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.Skills;

/// <summary>
/// Invokes the Claude CLI for one-shot stateless completions.
/// Shells out to the <c>claude</c> process with <c>-p</c> (print mode).
/// </summary>
public sealed class ClaudeCliClient : ILlmClient
{
    private readonly ILogger<ClaudeCliClient> _logger;
    private readonly ClaudeCliOptions _options;

    /// <summary>
    /// Creates a new <see cref="ClaudeCliClient"/>.
    /// </summary>
    public ClaudeCliClient(
        ILogger<ClaudeCliClient> logger,
        IOptions<ClaudeCliOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.CliPath,
            Arguments = "-p",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogDebug("Invoking Claude CLI with {PromptLength} character prompt", prompt.Length);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start Claude CLI at '{_options.CliPath}'. Is it installed and on PATH?", ex);
        }

        await process.StandardInput.WriteAsync(prompt.AsMemory(), cts.Token);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
        var error = await process.StandardError.ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(cts.Token);

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "Claude CLI exited with code {ExitCode}: {Error}",
                process.ExitCode, error);

            throw new InvalidOperationException(
                $"Claude CLI exited with code {process.ExitCode}: {error}");
        }

        _logger.LogDebug("Claude CLI returned {OutputLength} characters", output.Length);

        return output.Trim();
    }
}
```

### Step 5: Run unit tests to verify they pass

Run: `dotnet test tests/Cortex.Skills.Tests --filter "Category!=Integration" --verbosity normal`
Expected: PASS.

### Step 6: Commit

```bash
git add src/Cortex.Skills/ILlmClient.cs src/Cortex.Skills/ClaudeCliClient.cs src/Cortex.Skills/ClaudeCliOptions.cs src/Cortex.Skills/Cortex.Skills.csproj tests/Cortex.Skills.Tests/
git commit -m "feat: ClaudeCliClient — Claude CLI wrapper for one-shot LLM completions"
```

---

## Task 8: LlmSkillExecutor

**Files:**
- Create: `src/Cortex.Skills/LlmSkillExecutor.cs`
- Test: `tests/Cortex.Skills.Tests/LlmSkillExecutorTests.cs`

### Step 1: Write LlmSkillExecutor tests

```csharp
// tests/Cortex.Skills.Tests/LlmSkillExecutorTests.cs
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Skills.Tests;

public sealed class LlmSkillExecutorTests
{
    private readonly FakeLlmClient _fakeLlm = new();

    private LlmSkillExecutor CreateExecutor() =>
        new(_fakeLlm, NullLogger<LlmSkillExecutor>.Instance);

    private static SkillDefinition CreateSkillDefinition(
        string skillId = "test-skill",
        string? content = null) =>
        new()
        {
            SkillId = skillId,
            Name = "Test Skill",
            Description = "A test skill",
            Category = SkillCategory.Agent,
            ExecutorType = "llm",
            Content = content
        };

    [Fact]
    public void ExecutorType_IsLlm()
    {
        var executor = CreateExecutor();

        Assert.Equal("llm", executor.ExecutorType);
    }

    [Fact]
    public async Task ExecuteAsync_SendsPromptToLlmClient()
    {
        _fakeLlm.SetDefaultResponse("""{"capability":"test","authorityTier":"JustDoIt","summary":"test","confidence":0.9}""");
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: "You are a triage agent.");
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "Hello world",
            ["availableCapabilities"] = "email-drafting, code-review"
        };

        await executor.ExecuteAsync(skill, parameters);

        Assert.Single(_fakeLlm.Prompts);
        Assert.Contains("You are a triage agent.", _fakeLlm.Prompts[0]);
        Assert.Contains("Hello world", _fakeLlm.Prompts[0]);
        Assert.Contains("email-drafting", _fakeLlm.Prompts[0]);
    }

    [Fact]
    public async Task ExecuteAsync_UsesDescriptionIfNoContent()
    {
        _fakeLlm.SetDefaultResponse("{}");
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: null);
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "Hello",
            ["availableCapabilities"] = "none"
        };

        await executor.ExecuteAsync(skill, parameters);

        Assert.Contains("A test skill", _fakeLlm.Prompts[0]);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsJsonElement()
    {
        _fakeLlm.SetDefaultResponse("""{"capability":"email-drafting","authorityTier":"DoItAndShowMe","summary":"Draft reply","confidence":0.92}""");
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: "Triage prompt");
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "Draft a reply",
            ["availableCapabilities"] = "email-drafting"
        };

        var result = await executor.ExecuteAsync(skill, parameters);

        Assert.IsType<JsonElement>(result);
        var json = (JsonElement)result!;
        Assert.Equal("email-drafting", json.GetProperty("capability").GetString());
        Assert.Equal(0.92, json.GetProperty("confidence").GetDouble());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ReturnsNull()
    {
        _fakeLlm.SetDefaultResponse("not valid json at all");
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: "Triage prompt");
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "test",
            ["availableCapabilities"] = "none"
        };

        var result = await executor.ExecuteAsync(skill, parameters);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_JsonWrappedInMarkdown_ExtractsJson()
    {
        _fakeLlm.SetDefaultResponse("""
            ```json
            {"capability":"email-drafting","authorityTier":"JustDoIt","summary":"test","confidence":0.8}
            ```
            """);
        var executor = CreateExecutor();
        var skill = CreateSkillDefinition(content: "Triage");
        var parameters = new Dictionary<string, object>
        {
            ["messageContent"] = "test",
            ["availableCapabilities"] = "email-drafting"
        };

        var result = await executor.ExecuteAsync(skill, parameters);

        Assert.IsType<JsonElement>(result);
        var json = (JsonElement)result!;
        Assert.Equal("email-drafting", json.GetProperty("capability").GetString());
    }
}
```

### Step 2: Run tests to verify they fail

Run: `dotnet test tests/Cortex.Skills.Tests --filter "FullyQualifiedName~LlmSkillExecutorTests" --verbosity normal`
Expected: FAIL — `LlmSkillExecutor` does not exist, `SkillDefinition.Content` does not exist.

### Step 3: Add Content property to SkillDefinition

Modify `src/Cortex.Skills/SkillDefinition.cs` — add after the `Version` property:

```csharp
    /// <summary>
    /// Raw content of the skill definition file, loaded at registration time.
    /// Used by executors to extract prompts and configuration.
    /// </summary>
    public string? Content { get; init; }
```

### Step 4: Implement LlmSkillExecutor

```csharp
// src/Cortex.Skills/LlmSkillExecutor.cs
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Cortex.Skills;

/// <summary>
/// Skill executor for type "llm". Constructs a prompt from the skill definition
/// and parameters, sends it to an <see cref="ILlmClient"/>, and returns the
/// parsed JSON response.
/// </summary>
public sealed partial class LlmSkillExecutor : ISkillExecutor
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<LlmSkillExecutor> _logger;

    /// <summary>
    /// Creates a new <see cref="LlmSkillExecutor"/>.
    /// </summary>
    public LlmSkillExecutor(ILlmClient llmClient, ILogger<LlmSkillExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(llmClient);
        ArgumentNullException.ThrowIfNull(logger);

        _llmClient = llmClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ExecutorType => "llm";

    /// <inheritdoc />
    public async Task<object?> ExecuteAsync(
        SkillDefinition skill,
        IDictionary<string, object> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(parameters);

        var systemPrompt = skill.Content ?? skill.Description;
        var messageContent = parameters.TryGetValue("messageContent", out var mc)
            ? mc.ToString() ?? ""
            : "";
        var capabilities = parameters.TryGetValue("availableCapabilities", out var caps)
            ? caps.ToString() ?? "none"
            : "none";

        var fullPrompt = $"""
            {systemPrompt}

            Available capabilities: {capabilities}

            Message:
            {messageContent}

            Respond with JSON only, no markdown formatting.
            """;

        _logger.LogDebug("Executing LLM skill {SkillId}", skill.SkillId);

        var response = await _llmClient.CompleteAsync(fullPrompt, cancellationToken);

        return ParseJsonResponse(response, skill.SkillId);
    }

    private object? ParseJsonResponse(string response, string skillId)
    {
        // Strip markdown code fences if present
        var cleaned = ExtractJsonFromMarkdown(response);

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(cleaned);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse JSON response from LLM skill {SkillId}",
                skillId);
            return null;
        }
    }

    private static string ExtractJsonFromMarkdown(string response)
    {
        var match = JsonCodeFencePattern().Match(response);
        return match.Success ? match.Groups["json"].Value.Trim() : response.Trim();
    }

    [GeneratedRegex(@"```(?:json)?\s*(?<json>\{[\s\S]*?\})\s*```")]
    private static partial Regex JsonCodeFencePattern();
}
```

### Step 5: Run tests to verify they pass

Run: `dotnet test tests/Cortex.Skills.Tests --filter "FullyQualifiedName~LlmSkillExecutorTests" --verbosity normal`
Expected: PASS (6 tests).

### Step 6: Commit

```bash
git add src/Cortex.Skills/LlmSkillExecutor.cs src/Cortex.Skills/SkillDefinition.cs tests/Cortex.Skills.Tests/LlmSkillExecutorTests.cs
git commit -m "feat: LlmSkillExecutor — LLM skill executor with JSON response parsing"
```

---

## Task 9: Skill Definition and Persona Configuration Files

**Files:**
- Create: `skills/cos-triage.md`
- Create: `personas/chief-of-staff.md`

### Step 1: Create the cos-triage skill definition

```markdown
# cos-triage

## Metadata
- **skill-id**: cos-triage
- **category**: agent
- **executor**: llm
- **version**: 1.0.0

## Description

Analyses incoming messages and determines which agent capability should handle them.

## Prompt

You are a triage agent for a business operating system called Cortex. Your job is to analyse incoming messages and determine the best routing.

Given a message and a list of available agent capabilities, determine:

1. Which capability should handle this message
2. What authority tier is appropriate:
   - JustDoIt: internal actions with no external footprint (log, update, file)
   - DoItAndShowMe: prepare and present for approval (draft email, create plan)
   - AskMeFirst: novel, high-stakes, or uncertain (send email, publish, spend money)
3. A brief summary of the task
4. Your confidence in this routing decision (0.0 to 1.0)

If no available capability is a good match, set confidence below 0.5 so the message escalates.

Respond with JSON only, no markdown formatting:

{"capability": "capability-name", "authorityTier": "JustDoIt", "summary": "brief task description", "confidence": 0.95}
```

Save to: `skills/cos-triage.md`

### Step 2: Create the Chief of Staff persona

```markdown
# Chief of Staff

## Identity
- **agent-id**: cos
- **name**: Chief of Staff
- **type**: ai

## Capabilities
- triage: Analyses incoming messages and determines routing
- routing: Routes messages to specialist agents by capability
- delegation: Tracks delegated work and monitors completion

## Pipeline
1. cos-triage

## Configuration
- **escalation-target**: agent.founder
- **model-tier**: balanced
- **confidence-threshold**: 0.6
```

Save to: `personas/chief-of-staff.md`

### Step 3: Commit

```bash
git add skills/cos-triage.md personas/chief-of-staff.md
git commit -m "feat: cos-triage skill definition and Chief of Staff persona file"
```

---

## Task 10: DI Registration — AddPersona and Service Wiring

**Files:**
- Modify: `src/Cortex.Agents/AgentRuntimeBuilder.cs`
- Modify: `src/Cortex.Agents/ServiceCollectionExtensions.cs`
- Modify: `src/Cortex.Skills/Cortex.Skills.csproj` (add DI abstractions)
- Create: `src/Cortex.Skills/ServiceCollectionExtensions.cs`
- Test: `tests/Cortex.Agents.Tests/AgentRuntimeBuilderTests.cs`

### Step 1: Write test for AddPersona

```csharp
// tests/Cortex.Agents.Tests/AgentRuntimeBuilderTests.cs
using Cortex.Agents.Personas;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Agents.Tests;

public sealed class AgentRuntimeBuilderTests
{
    [Fact]
    public void AddPersona_FromDefinition_RegistersSkillDrivenAgent()
    {
        var services = new ServiceCollection();
        var builder = new AgentRuntimeBuilder(services);

        var persona = new PersonaDefinition
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities = [new AgentCapability { Name = "triage", Description = "Triage" }],
            Pipeline = ["cos-triage"],
            EscalationTarget = "agent.founder"
        };

        builder.AddPersona(persona);

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IAgent)
                && d.Lifetime == ServiceLifetime.Singleton);

        Assert.NotNull(descriptor);
    }
}
```

### Step 2: Run test to verify it fails

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~AgentRuntimeBuilderTests" --verbosity normal`
Expected: FAIL — `AddPersona` method does not exist.

### Step 3: Add DI abstractions to Cortex.Skills

Add to `src/Cortex.Skills/Cortex.Skills.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.3" />
<PackageReference Include="Microsoft.Extensions.Options" Version="10.0.3" />
```

### Step 4: Create Cortex.Skills ServiceCollectionExtensions

```csharp
// src/Cortex.Skills/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.Skills;

/// <summary>
/// Extension methods for registering Cortex skill services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Cortex skill infrastructure to the service collection.
    /// </summary>
    public static IServiceCollection AddCortexSkills(
        this IServiceCollection services,
        Action<ClaudeCliOptions>? configureCli = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InMemorySkillRegistry>();
        services.AddSingleton<ISkillRegistry>(sp => sp.GetRequiredService<InMemorySkillRegistry>());
        services.AddSingleton<ISkillExecutor, LlmSkillExecutor>();

        if (configureCli is not null)
        {
            services.Configure(configureCli);
        }

        services.AddSingleton<ILlmClient, ClaudeCliClient>();

        return services;
    }
}
```

### Step 5: Update AgentRuntimeBuilder with AddPersona

Modify `src/Cortex.Agents/AgentRuntimeBuilder.cs`:

```csharp
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Registers a <see cref="SkillDrivenAgent"/> from a <see cref="PersonaDefinition"/>.
    /// </summary>
    public AgentRuntimeBuilder AddPersona(PersonaDefinition persona)
    {
        ArgumentNullException.ThrowIfNull(persona);

        _services.AddSingleton<IAgent>(sp =>
        {
            var pipelineRunner = new SkillPipelineRunner(
                sp.GetRequiredService<ISkillRegistry>(),
                sp.GetServices<ISkillExecutor>(),
                sp.GetRequiredService<ILogger<SkillPipelineRunner>>());

            return new SkillDrivenAgent(
                persona,
                pipelineRunner,
                sp.GetRequiredService<IAgentRegistry>(),
                sp.GetRequiredService<IDelegationTracker>(),
                sp.GetRequiredService<IReferenceCodeGenerator>(),
                sp.GetRequiredService<IMessagePublisher>(),
                sp.GetRequiredService<ILogger<SkillDrivenAgent>>());
        });

        return this;
    }

    /// <summary>
    /// Registers a <see cref="SkillDrivenAgent"/> from a persona markdown file.
    /// </summary>
    public AgentRuntimeBuilder AddPersonaFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var persona = PersonaParser.ParseFileAsync(filePath).GetAwaiter().GetResult();
        return AddPersona(persona);
    }
}
```

### Step 6: Run test to verify it passes

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~AgentRuntimeBuilderTests" --verbosity normal`
Expected: PASS.

### Step 7: Run the full test suite

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass.

### Step 8: Commit

```bash
git add src/Cortex.Agents/AgentRuntimeBuilder.cs src/Cortex.Skills/ServiceCollectionExtensions.cs src/Cortex.Skills/Cortex.Skills.csproj tests/Cortex.Agents.Tests/AgentRuntimeBuilderTests.cs
git commit -m "feat: AddPersona DI registration and Cortex.Skills service wiring"
```

---

## Task 11: End-to-End Integration Test

**Files:**
- Create: `tests/Cortex.Agents.Tests/SkillDrivenAgentEndToEndTests.cs`

### Step 1: Write the end-to-end test

```csharp
// tests/Cortex.Agents.Tests/SkillDrivenAgentEndToEndTests.cs
using System.Text.Json;
using Cortex.Agents.Delegation;
using Cortex.Agents.Personas;
using Cortex.Agents.Pipeline;
using Cortex.Agents.Tests.Pipeline;
using Cortex.Core.Authority;
using Cortex.Core.Messages;
using Cortex.Core.References;
using Cortex.Messaging;
using Cortex.Skills;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.Agents.Tests;

/// <summary>
/// End-to-end test: CoS agent receives a message, triages via mocked LLM skill,
/// routes to specialist, and tracks delegation. Uses real InMemoryMessageBus,
/// real AgentHarness, real delegation tracker — only the LLM is faked.
/// </summary>
public sealed class SkillDrivenAgentEndToEndTests : IAsyncDisposable
{
    private readonly InMemoryMessageBus _bus = new();
    private readonly InMemoryAgentRegistry _agentRegistry = new();
    private readonly InMemoryDelegationTracker _delegationTracker = new();
    private readonly InMemorySkillRegistry _skillRegistry = new();
    private readonly FakeSkillExecutor _fakeExecutor = new("llm");
    private readonly SequentialReferenceCodeGenerator _refCodeGenerator;

    public SkillDrivenAgentEndToEndTests()
    {
        _refCodeGenerator = new SequentialReferenceCodeGenerator(
            new InMemorySequenceStore(), TimeProvider.System);
    }

    [Fact]
    public async Task FullFlow_MessageRoutedThroughCosToSpecialist()
    {
        // --- Arrange ---

        // Register the triage skill
        await _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "CoS Triage",
            Description = "Triage",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        });

        // Configure triage result
        var triageJson = JsonSerializer.SerializeToElement(new
        {
            capability = "email-drafting",
            authorityTier = "DoItAndShowMe",
            summary = "Draft reply to client email",
            confidence = 0.92
        });
        _fakeExecutor.SetResult("cos-triage", triageJson);

        // Create and start the CoS agent via harness
        var persona = new PersonaDefinition
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities =
            [
                new AgentCapability { Name = "triage", Description = "Triage" }
            ],
            Pipeline = ["cos-triage"],
            EscalationTarget = "agent.founder",
            ConfidenceThreshold = 0.6
        };

        var pipelineRunner = new SkillPipelineRunner(
            _skillRegistry,
            [_fakeExecutor],
            NullLogger<SkillPipelineRunner>.Instance);

        var cosAgent = new SkillDrivenAgent(
            persona,
            pipelineRunner,
            _agentRegistry,
            _delegationTracker,
            _refCodeGenerator,
            _bus,
            NullLogger<SkillDrivenAgent>.Instance);

        var cosHarness = new AgentHarness(
            cosAgent,
            _bus,
            _agentRegistry,
            NullLogger<AgentHarness>.Instance);

        await cosHarness.StartAsync();

        // Create and start a specialist agent (echo agent standing in)
        var specialistReceived = new TaskCompletionSource<MessageEnvelope>();
        var specialist = new CallbackAgent("email-agent", "email-drafting", envelope =>
        {
            specialistReceived.SetResult(envelope);
            return Task.FromResult<MessageEnvelope?>(null);
        });

        var specialistHarness = new AgentHarness(
            specialist,
            _bus,
            _agentRegistry,
            NullLogger<AgentHarness>.Instance);

        await specialistHarness.StartAsync();

        // --- Act ---

        // Send a message to the CoS
        var envelope = new MessageEnvelope
        {
            Message = new TestMessage { Content = "Please draft a reply to John's email about the Q1 report" },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = "agent.human-user" },
            AuthorityClaims =
            [
                new AuthorityClaim
                {
                    GrantedBy = "founder",
                    GrantedTo = "cos",
                    Tier = AuthorityTier.DoItAndShowMe,
                    GrantedAt = DateTimeOffset.UtcNow
                }
            ]
        };

        await _bus.PublishAsync(envelope, "agent.cos");

        // --- Assert ---

        // Specialist received the routed message
        var routedMsg = await specialistReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(routedMsg);
        Assert.Equal("cos", routedMsg.Context.FromAgentId);
        Assert.Equal("agent.human-user", routedMsg.Context.ReplyTo);

        // Authority was set correctly
        var outboundClaim = Assert.Single(routedMsg.AuthorityClaims);
        Assert.Equal(AuthorityTier.DoItAndShowMe, outboundClaim.Tier);
        Assert.Equal("cos", outboundClaim.GrantedBy);
        Assert.Equal("email-agent", outboundClaim.GrantedTo);

        // Delegation was tracked
        var delegations = await _delegationTracker.GetByAssigneeAsync("email-agent");
        Assert.Single(delegations);
        Assert.Equal("cos", delegations[0].DelegatedBy);
        Assert.Equal("Draft reply to client email", delegations[0].Description);
        Assert.Equal(DelegationStatus.Assigned, delegations[0].Status);

        // --- Cleanup ---
        await cosHarness.StopAsync();
        await specialistHarness.StopAsync();
    }

    [Fact]
    public async Task FullFlow_UnroutableMessage_EscalatesToFounder()
    {
        // Register the triage skill
        await _skillRegistry.RegisterAsync(new SkillDefinition
        {
            SkillId = "cos-triage",
            Name = "CoS Triage",
            Description = "Triage",
            Category = SkillCategory.Agent,
            ExecutorType = "llm"
        });

        // Triage returns a capability no one has
        var triageJson = JsonSerializer.SerializeToElement(new
        {
            capability = "quantum-physics",
            authorityTier = "JustDoIt",
            summary = "Solve quantum equations",
            confidence = 0.95
        });
        _fakeExecutor.SetResult("cos-triage", triageJson);

        // Create CoS
        var persona = new PersonaDefinition
        {
            AgentId = "cos",
            Name = "Chief of Staff",
            AgentType = "ai",
            Capabilities =
            [
                new AgentCapability { Name = "triage", Description = "Triage" }
            ],
            Pipeline = ["cos-triage"],
            EscalationTarget = "agent.founder",
            ConfidenceThreshold = 0.6
        };

        var pipelineRunner = new SkillPipelineRunner(
            _skillRegistry,
            [_fakeExecutor],
            NullLogger<SkillPipelineRunner>.Instance);

        var cosAgent = new SkillDrivenAgent(
            persona,
            pipelineRunner,
            _agentRegistry,
            _delegationTracker,
            _refCodeGenerator,
            _bus,
            NullLogger<SkillDrivenAgent>.Instance);

        var cosHarness = new AgentHarness(
            cosAgent,
            _bus,
            _agentRegistry,
            NullLogger<AgentHarness>.Instance);

        await cosHarness.StartAsync();

        // Listen on founder queue
        var founderReceived = new TaskCompletionSource<MessageEnvelope>();
        await _bus.StartConsumingAsync("agent.founder", e =>
        {
            founderReceived.SetResult(e);
            return Task.CompletedTask;
        });

        // Send a message
        var envelope = new MessageEnvelope
        {
            Message = new TestMessage { Content = "Solve the Schrodinger equation" },
            ReferenceCode = ReferenceCode.Create(DateTimeOffset.UtcNow, 1),
            Context = new MessageContext { ReplyTo = "agent.human-user" }
        };

        await _bus.PublishAsync(envelope, "agent.cos");

        // Founder received the escalation
        var escalatedMsg = await founderReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(escalatedMsg);
        Assert.Equal("cos", escalatedMsg.Context.FromAgentId);

        // Delegation tracked
        var delegations = await _delegationTracker.GetByAssigneeAsync("agent.founder");
        Assert.Single(delegations);
        Assert.Contains("Escalated", delegations[0].Description);

        await cosHarness.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _refCodeGenerator.Dispose();
        await _bus.DisposeAsync();
    }
}

/// <summary>
/// Test agent with configurable callback and a specific capability.
/// </summary>
file sealed class CallbackAgent(
    string agentId,
    string capabilityName,
    Func<MessageEnvelope, Task<MessageEnvelope?>> callback) : IAgent
{
    public string AgentId { get; } = agentId;
    public string Name { get; } = $"Agent {agentId}";
    public IReadOnlyList<AgentCapability> Capabilities { get; } =
    [
        new AgentCapability { Name = capabilityName, Description = capabilityName }
    ];

    public Task<MessageEnvelope?> ProcessAsync(
        MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => callback(envelope);
}
```

### Step 2: Run end-to-end tests

Run: `dotnet test tests/Cortex.Agents.Tests --filter "FullyQualifiedName~SkillDrivenAgentEndToEndTests" --verbosity normal`
Expected: PASS (2 tests).

### Step 3: Run the full test suite

Run: `dotnet test --configuration Release --verbosity normal --filter "Category!=Integration"`
Expected: All tests pass — no regressions.

### Step 4: Commit

```bash
git add tests/Cortex.Agents.Tests/SkillDrivenAgentEndToEndTests.cs
git commit -m "feat: end-to-end tests — CoS routing and escalation through harness"
```

---

## Summary

| Task | Component | New Files |
|------|-----------|-----------|
| 1 | Pipeline data types | `TriageResult.cs`, `SkillPipelineContext.cs` + tests |
| 2 | InMemorySkillRegistry | `InMemorySkillRegistry.cs` + test project |
| 3 | SkillPipelineRunner | `SkillPipelineRunner.cs`, `FakeSkillExecutor.cs` + tests |
| 4 | Persona config | `PersonaDefinition.cs`, `PersonaParser.cs` + tests |
| 5 | SkillDrivenAgent | `SkillDrivenAgent.cs` + tests |
| 6 | ILlmClient | `ILlmClient.cs`, `FakeLlmClient.cs` |
| 7 | ClaudeCliClient | `ClaudeCliClient.cs`, `ClaudeCliOptions.cs` + tests |
| 8 | LlmSkillExecutor | `LlmSkillExecutor.cs` + tests |
| 9 | Config files | `skills/cos-triage.md`, `personas/chief-of-staff.md` |
| 10 | DI wiring | `AgentRuntimeBuilder.cs` update, `ServiceCollectionExtensions.cs` + tests |
| 11 | End-to-end | `SkillDrivenAgentEndToEndTests.cs` |
