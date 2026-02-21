# Cortex Repository Setup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Set up the Cortex repository as a professional open-source C# project with foundational contracts, CI/CD, and documentation.

**Architecture:** Empty git repo → infrastructure files → .NET 10 solution with 5 src projects and 3 test projects → foundational interfaces and types → documentation and GitHub config → verified build → initial commit.

**Tech Stack:** .NET 10, C#, GitHub Actions, RabbitMQ (abstraction only), AGPL-3.0

---

### Task 1: Repository Infrastructure Files

**Files:**
- Create: `.gitignore`
- Create: `.gitattributes`
- Create: `.editorconfig`
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `LICENSE`

**Step 1: Create .gitignore**

Standard .NET .gitignore covering bin/, obj/, .vs/, *.user, publish output, NuGet packages, and OS files (.DS_Store, Thumbs.db).

**Step 2: Create .gitattributes**

Line ending normalisation: `* text=auto`, with explicit LF for shell scripts and CRLF for Windows-specific files.

**Step 3: Create .editorconfig**

C# conventions: 4-space indentation, file-scoped namespaces, `var` when type is apparent, nullable enabled, newline at end of file.

**Step 4: Create global.json**

Pin to .NET 10 SDK. Use `rollForward: latestFeature` so minor SDK updates work.

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

**Step 5: Create Directory.Build.props**

Shared settings: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, target `net10.0`.

**Step 6: Create LICENSE**

AGPL-3.0 full text. Copyright line: `Copyright (C) 2026 Daniel Grimes / dbhq-uk`

---

### Task 2: GitHub Infrastructure

**Files:**
- Create: `.github/ISSUE_TEMPLATE/bug_report.md`
- Create: `.github/ISSUE_TEMPLATE/feature_request.md`
- Create: `.github/ISSUE_TEMPLATE/config.yml`
- Create: `.github/PULL_REQUEST_TEMPLATE.md`
- Create: `.github/workflows/ci.yml`
- Create: `.github/FUNDING.yml`
- Create: `.github/SECURITY.md`

**Step 1: Create bug report issue template**

Sections: Description, Steps to Reproduce, Expected Behaviour, Actual Behaviour, Environment (.NET version, OS).

**Step 2: Create feature request issue template**

Sections: Problem Statement, Proposed Solution, Alternatives Considered, Additional Context.

**Step 3: Create issue template config**

Enable blank issues. Add link to discussions if enabled.

**Step 4: Create PR template**

Sections: Summary, Changes, Testing, Checklist (tests pass, docs updated, no warnings).

**Step 5: Create CI workflow**

```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release --verbosity normal
```

**Step 6: Create FUNDING.yml**

Placeholder with GitHub sponsors field commented out.

**Step 7: Create SECURITY.md**

Responsible disclosure policy. Email contact. Supported versions table.

---

### Task 3: Scaffold C# Solution and Projects

**Step 1: Create the solution file**

Run: `dotnet new sln -n Cortex -o /home/devops/cortex`

**Step 2: Create src projects**

```bash
dotnet new classlib -n Cortex.Core -o src/Cortex.Core -f net10.0
dotnet new classlib -n Cortex.Messaging -o src/Cortex.Messaging -f net10.0
dotnet new classlib -n Cortex.Agents -o src/Cortex.Agents -f net10.0
dotnet new classlib -n Cortex.Skills -o src/Cortex.Skills -f net10.0
dotnet new web -n Cortex.Web -o src/Cortex.Web -f net10.0
```

**Step 3: Create test projects**

```bash
dotnet new xunit -n Cortex.Core.Tests -o tests/Cortex.Core.Tests -f net10.0
dotnet new xunit -n Cortex.Messaging.Tests -o tests/Cortex.Messaging.Tests -f net10.0
dotnet new xunit -n Cortex.Agents.Tests -o tests/Cortex.Agents.Tests -f net10.0
```

**Step 4: Add all projects to solution**

```bash
dotnet sln add src/Cortex.Core/Cortex.Core.csproj
dotnet sln add src/Cortex.Messaging/Cortex.Messaging.csproj
dotnet sln add src/Cortex.Agents/Cortex.Agents.csproj
dotnet sln add src/Cortex.Skills/Cortex.Skills.csproj
dotnet sln add src/Cortex.Web/Cortex.Web.csproj
dotnet sln add tests/Cortex.Core.Tests/Cortex.Core.Tests.csproj
dotnet sln add tests/Cortex.Messaging.Tests/Cortex.Messaging.Tests.csproj
dotnet sln add tests/Cortex.Agents.Tests/Cortex.Agents.Tests.csproj
```

**Step 5: Add project references**

- Cortex.Messaging references Cortex.Core
- Cortex.Agents references Cortex.Core
- Cortex.Skills references Cortex.Core
- Cortex.Web references Cortex.Core, Cortex.Messaging, Cortex.Agents, Cortex.Skills
- Test projects reference their corresponding src projects

**Step 6: Remove auto-generated placeholder files**

Delete Class1.cs from classlib projects, any auto-generated files that don't fit.

**Step 7: Verify build**

Run: `dotnet build --configuration Release`
Expected: Build succeeded with 0 errors, 0 warnings.

---

### Task 4: Foundational Contracts — Cortex.Core

**Files:**
- Create: `src/Cortex.Core/Messages/IMessage.cs`
- Create: `src/Cortex.Core/Messages/MessageEnvelope.cs`
- Create: `src/Cortex.Core/Messages/MessagePriority.cs`
- Create: `src/Cortex.Core/Messages/MessageContext.cs`
- Create: `src/Cortex.Core/Authority/AuthorityClaim.cs`
- Create: `src/Cortex.Core/Authority/AuthorityTier.cs`
- Create: `src/Cortex.Core/Authority/IAuthorityProvider.cs`
- Create: `src/Cortex.Core/References/ReferenceCode.cs`
- Create: `src/Cortex.Core/References/IReferenceCodeGenerator.cs`
- Create: `src/Cortex.Core/Channels/IChannel.cs`
- Create: `src/Cortex.Core/Channels/ChannelType.cs`
- Create: `src/Cortex.Core/Teams/ITeam.cs`
- Create: `src/Cortex.Core/Teams/TeamStatus.cs`

**Step 1: Write MessagePriority enum**

```csharp
namespace Cortex.Core.Messages;

public enum MessagePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
```

**Step 2: Write IMessage interface**

```csharp
namespace Cortex.Core.Messages;

public interface IMessage
{
    string MessageId { get; }
    DateTimeOffset Timestamp { get; }
    string? CorrelationId { get; }
}
```

**Step 3: Write MessageContext**

Record with ParentMessageId, OriginalGoal, TeamId, ChannelId.

**Step 4: Write MessageEnvelope**

Record wrapping IMessage with: ReferenceCode, AuthorityClaims (list), MessageContext, MessagePriority, SLA (TimeSpan?).

**Step 5: Write AuthorityTier enum**

JustDoIt, DoItAndShowMe, AskMeFirst.

**Step 6: Write AuthorityClaim**

Record with: GrantedBy, GrantedTo, Tier, PermittedActions (IReadOnlyList<string>), GrantedAt, ExpiresAt.

**Step 7: Write IAuthorityProvider**

Interface with: `Task<AuthorityClaim?> GetClaim(string agentId, string action)` and `Task<bool> HasAuthority(string agentId, string action, AuthorityTier minimumTier)`.

**Step 8: Write ReferenceCode value object**

Record struct with string Value. Factory method `Create(DateTimeOffset date, int sequence)` producing `CTX-YYYY-MMDD-NNN` format. Override ToString(). Validate format on construction.

**Step 9: Write IReferenceCodeGenerator**

Interface with: `ReferenceCode Generate()`.

**Step 10: Write ChannelType enum**

Default, Named, Direct, Team.

**Step 11: Write IChannel**

Interface with: Id, Name, ChannelType, IsOpen, CreatedAt.

**Step 12: Write TeamStatus enum**

Assembling, Active, Dissolving, Complete.

**Step 13: Write ITeam**

Interface with: TeamId, ReferenceCode, TeamStatus, MemberIds, CreatedAt, CompletedAt.

**Step 14: Verify build**

Run: `dotnet build --configuration Release`
Expected: 0 errors, 0 warnings.

---

### Task 5: Foundational Contracts — Cortex.Messaging

**Files:**
- Create: `src/Cortex.Messaging/IMessageBus.cs`
- Create: `src/Cortex.Messaging/IMessageConsumer.cs`
- Create: `src/Cortex.Messaging/IMessagePublisher.cs`
- Create: `src/Cortex.Messaging/QueueBinding.cs`
- Create: `src/Cortex.Messaging/QueueTopology.cs`

**Step 1: Write IMessagePublisher**

Interface with: `Task PublishAsync(MessageEnvelope envelope, string queueName, CancellationToken ct)`.

**Step 2: Write IMessageConsumer**

Interface with: `Task StartConsumingAsync(string queueName, Func<MessageEnvelope, Task> handler, CancellationToken ct)` and `Task StopConsumingAsync()`.

**Step 3: Write IMessageBus**

Interface combining publish and subscribe: `IMessagePublisher` + `IMessageConsumer` + `Task<QueueTopology> GetTopologyAsync()`.

**Step 4: Write QueueBinding**

Record with: QueueName, RoutingPattern, ChannelId?, AgentId?, Priority.

**Step 5: Write QueueTopology**

Class with: Bindings list, methods to add/remove bindings, find queue by agent/channel.

**Step 6: Verify build**

Run: `dotnet build --configuration Release`

---

### Task 6: Foundational Contracts — Cortex.Agents

**Files:**
- Create: `src/Cortex.Agents/IAgent.cs`
- Create: `src/Cortex.Agents/AgentCapability.cs`
- Create: `src/Cortex.Agents/AgentRegistration.cs`
- Create: `src/Cortex.Agents/IAgentRegistry.cs`
- Create: `src/Cortex.Agents/Delegation/DelegationStatus.cs`
- Create: `src/Cortex.Agents/Delegation/DelegationRecord.cs`
- Create: `src/Cortex.Agents/Delegation/IDelegationTracker.cs`

**Step 1: Write IAgent**

Interface with: AgentId, Name, Capabilities (IReadOnlyList<AgentCapability>), `Task<MessageEnvelope?> ProcessAsync(MessageEnvelope envelope, CancellationToken ct)`.

**Step 2: Write AgentCapability**

Record with: Name, Description, SkillIds (IReadOnlyList<string>).

**Step 3: Write AgentRegistration**

Record with: AgentId, Name, AgentType (string — "human" or "ai"), Capabilities, RegisteredAt, IsAvailable.

**Step 4: Write IAgentRegistry**

Interface with: `Task RegisterAsync(AgentRegistration)`, `Task<AgentRegistration?> FindByIdAsync(string)`, `Task<IReadOnlyList<AgentRegistration>> FindByCapabilityAsync(string capabilityName)`.

**Step 5: Write DelegationStatus enum**

Assigned, InProgress, AwaitingReview, Complete, Overdue.

**Step 6: Write DelegationRecord**

Record with: ReferenceCode, DelegatedBy, DelegatedTo, Description, Status, AssignedAt, DueAt, CompletedAt.

**Step 7: Write IDelegationTracker**

Interface with: `Task DelegateAsync(DelegationRecord)`, `Task UpdateStatusAsync(ReferenceCode, DelegationStatus)`, `Task<IReadOnlyList<DelegationRecord>> GetByAssigneeAsync(string agentId)`, `Task<IReadOnlyList<DelegationRecord>> GetOverdueAsync()`.

**Step 8: Verify build**

Run: `dotnet build --configuration Release`

---

### Task 7: Foundational Contracts — Cortex.Skills

**Files:**
- Create: `src/Cortex.Skills/ISkill.cs`
- Create: `src/Cortex.Skills/SkillDefinition.cs`
- Create: `src/Cortex.Skills/SkillCategory.cs`
- Create: `src/Cortex.Skills/ISkillRegistry.cs`
- Create: `src/Cortex.Skills/ISkillExecutor.cs`

**Step 1: Write SkillCategory enum**

Integration, Knowledge, Agent, Organisational, Meta.

**Step 2: Write SkillDefinition**

Record with: SkillId, Name, Description, Category, Triggers (IReadOnlyList<string>), ExecutorType (string — "csharp", "python", "cli", "api"), FilePath, Version.

**Step 3: Write ISkill**

Interface with: Definition (SkillDefinition), `Task<object?> ExecuteAsync(IDictionary<string, object> parameters, CancellationToken ct)`.

**Step 4: Write ISkillRegistry**

Interface with: `Task RegisterAsync(SkillDefinition)`, `Task<SkillDefinition?> FindByIdAsync(string)`, `Task<IReadOnlyList<SkillDefinition>> SearchAsync(string query)`, `Task<IReadOnlyList<SkillDefinition>> FindByCategoryAsync(SkillCategory)`.

**Step 5: Write ISkillExecutor**

Interface with: `string ExecutorType { get; }`, `Task<object?> ExecuteAsync(SkillDefinition skill, IDictionary<string, object> parameters, CancellationToken ct)`.

**Step 6: Create skills/README.md placeholder**

Brief explanation of what skill files are and how they work.

**Step 7: Verify build**

Run: `dotnet build --configuration Release`

---

### Task 8: Write ReferenceCode Unit Tests

**Files:**
- Create: `tests/Cortex.Core.Tests/References/ReferenceCodeTests.cs`

**Step 1: Write tests for ReferenceCode**

- Test format: `CTX-2026-0221-001` matches pattern
- Test equality: same value = equal
- Test inequality: different value = not equal
- Test ToString returns the value
- Test Create factory method produces correct format

**Step 2: Run tests**

Run: `dotnet test --configuration Release --verbosity normal`
Expected: All tests pass.

**Step 3: Commit contracts and tests**

---

### Task 9: Documentation

**Files:**
- Create: `README.md`
- Create: `CONTRIBUTING.md`
- Create: `CODE_OF_CONDUCT.md`
- Create: `CHANGELOG.md`
- Create: `CLAUDE.md`
- Create: `docs/architecture/vision.md`
- Create: `docs/adr/001-message-queue-rabbitmq.md`

**Step 1: Write README.md**

- Title, tagline, badges (CI, .NET 10, AGPL-3.0)
- What is Cortex? section
- Architecture overview with Mermaid diagram
- Project structure overview
- Quick start (build instructions)
- Project status: Phase 1
- Built with AI section
- Links to contributing, license, vision doc

**Step 2: Write CONTRIBUTING.md**

- How to report bugs and suggest features
- Development setup instructions
- Branch naming: feature/, fix/, docs/
- Commit format: Conventional Commits
- PR process
- Code style (reference .editorconfig)
- AI-assisted contributions welcome

**Step 3: Write CODE_OF_CONDUCT.md**

Contributor Covenant v2.1.

**Step 4: Write CHANGELOG.md**

Keep a Changelog format. Unreleased section with initial project setup.

**Step 5: Write CLAUDE.md**

- Project overview for AI context
- Architecture summary
- Build/test/run commands
- Namespace structure
- Key design decisions
- Coding conventions

**Step 6: Save vision document**

Copy the user's spec as `docs/architecture/vision.md`.

**Step 7: Write ADR-001**

RabbitMQ decision: context, decision, consequences.

---

### Task 10: Final Verification and Commit

**Step 1: Full build verification**

Run: `dotnet build --configuration Release`
Expected: 0 errors, 0 warnings.

**Step 2: Run all tests**

Run: `dotnet test --configuration Release --verbosity normal`
Expected: All tests pass.

**Step 3: Commit all files**

Stage all files, commit with message: `feat: initial project setup with foundational contracts and open-source infrastructure`

**Step 4: Push to GitHub**

Run: `git push -u origin main`
