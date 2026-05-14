# Contributing

Thanks for contributing to Moonforge.

## Development Setup

```bash
dotnet restore src/Moonforge.Core/Moonforge.Core.csproj
dotnet build src/Moonforge.Core/Moonforge.Core.csproj -c Release
```

Run tests before opening a pull request:

```bash
dotnet test tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj -c Release
dotnet test tests/Moonforge.Sample.Console.Tests/Moonforge.Sample.Console.Tests.csproj -c Release
```

## Coding Guidelines

- Keep changes focused and cohesive.
- Preserve deterministic behavior in gameplay logic.
- Prefer explicit command/query boundaries over mixed responsibilities.
- Add tests for new behaviors and bug fixes.

## Pull Requests

- Describe the behavior change, not just file changes.
- Include test coverage or explain why it is not needed.
- Keep PRs small enough to review quickly.

## Commit Style

A conventional style is recommended:

- `feat: add shop restock cooldown`
- `fix: rollback events when command validation fails`
- `docs: clarify sample startup instructions`
