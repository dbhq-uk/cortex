# Contributing to Cortex

Thank you for your interest in contributing to Cortex. This guide will help you get started.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

### Development Setup

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR-USERNAME/cortex.git
   cd cortex
   ```
3. Build the project:
   ```bash
   dotnet restore
   dotnet build
   ```
4. Run tests:
   ```bash
   dotnet test
   ```

## How to Contribute

### Reporting Bugs

- Use the [bug report template](https://github.com/dbhq-uk/cortex/issues/new?template=bug_report.md)
- Include steps to reproduce, expected vs actual behaviour, and environment details
- Check existing issues first to avoid duplicates

### Suggesting Features

- Use the [feature request template](https://github.com/dbhq-uk/cortex/issues/new?template=feature_request.md)
- Describe the problem you're trying to solve, not just the solution
- Consider how it fits with the [project vision](docs/architecture/vision.md)

### Submitting Pull Requests

1. Create a feature branch from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. Make your changes, following the coding conventions below
3. Write or update tests as appropriate
4. Ensure the build passes with no warnings:
   ```bash
   dotnet build --configuration Release
   dotnet test --configuration Release
   ```
5. Commit your changes using [Conventional Commits](#commit-messages)
6. Push to your fork and open a pull request

## Conventions

### Branch Naming

| Prefix     | Use                    | Example                        |
|------------|------------------------|--------------------------------|
| `feature/` | New features           | `feature/cos-triage-engine`    |
| `fix/`     | Bug fixes              | `fix/reference-code-overflow`  |
| `docs/`    | Documentation changes  | `docs/update-vision`           |
| `chore/`   | Maintenance tasks      | `chore/update-dependencies`    |
| `refactor/`| Code refactoring       | `refactor/message-envelope`    |

### Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add authority claim validation
fix: handle null correlation ID in message envelope
docs: update architecture decision record for queue topology
chore: update xunit to latest version
refactor: extract message routing into dedicated service
test: add delegation tracker integration tests
```

### Code Style

- Code style is enforced by `.editorconfig` -- configure your editor to use it
- File-scoped namespaces
- `var` when type is apparent
- XML documentation on all public members
- `_camelCase` for private fields
- PascalCase for everything else public
- No warnings allowed (`TreatWarningsAsErrors` is enabled)

### Testing

- Write tests for new functionality
- Use xUnit
- Test file location mirrors source: `src/Cortex.Core/Foo.cs` -> `tests/Cortex.Core.Tests/FooTests.cs`
- Descriptive test names: `MethodName_Scenario_ExpectedResult`

## AI-Assisted Contributions

AI-assisted contributions are welcome and encouraged. If you use AI tools (Claude, Copilot, etc.) to help write code:

- You are still responsible for the quality and correctness of the contribution
- Mention AI assistance in your PR description if it was a significant part of the work
- All the same quality standards apply

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## Questions?

Open a [discussion](https://github.com/dbhq-uk/cortex/discussions) if you have questions that aren't bugs or feature requests.
