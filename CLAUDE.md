# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Moonforge is a deterministic, modular RPG engine published as the `Moonforge.Core` NuGet package. `src/Moonforge.Core` targets `netstandard2.1` so it can be consumed by Unity; the tests and samples target `net8.0`. Determinism (seeded RNG, explicit clock, no wall-clock or `DateTime.Now`) is a hard design constraint — gameplay logic must produce identical results from identical inputs.

## Common commands

```bash
# Restore / build the engine
dotnet restore src/Moonforge.Core/Moonforge.Core.csproj
dotnet build   src/Moonforge.Core/Moonforge.Core.csproj -c Release

# Or use the solution (requires .NET 9 SDK+ for .slnx)
dotnet build Moonforge.slnx -c Release

# Run all tests
dotnet test tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj -c Release
dotnet test tests/Moonforge.Sample.Console.Tests/Moonforge.Sample.Console.Tests.csproj -c Release

# Run a single xUnit test by fully-qualified name (or substring)
dotnet test tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj --filter "FullyQualifiedName~CombatTests.Skill_Damage_Is_Deterministic"

# Run a single test class
dotnet test tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj --filter "FullyQualifiedName~LootTests"

# Run the samples
dotnet run --project samples/Moonforge.Sample.Console
dotnet run --project samples/Moonforge.Sample.Minimal

# Pack a NuGet
dotnet pack src/Moonforge.Core/Moonforge.Core.csproj -c Release -o artifacts
```

CI (`.github/workflows/ci.yml`) runs on `windows-latest` with .NET 10 SDK and builds via `Moonforge.slnx`.

## Architecture

### Command/Query + Reactor pipeline

All gameplay state lives on a single aggregate `GameState` (`src/Moonforge.Core/GameState.cs`). Mutation flows exclusively through commands; reads go through queries. The runtime contract is in `src/Moonforge.Core/Runtime/`:

- `ICommand` / `ICommandHandler<TCommand>` — handlers mutate `GameState` and return `DomainResult` (success or `DomainError` with a `DomainErrorCode`).
- `CommandDispatcher` (`Runtime/Commands/CommandDispatcher.cs`) is the transactional core. For every dispatch it:
  1. Clones `GameState` (`GameState.Clone()`) as a rollback snapshot.
  2. Swaps the caller's `IDomainEventSink` for a `BufferedDomainEventSink` so events are not externally visible mid-transaction.
  3. Invokes the handler. On failure or thrown exception, calls `gameState.RestoreFrom(snapshot)` and returns; buffered events are discarded.
  4. On success, fans buffered events through every registered `IDomainEventReactor`. A reactor returning failure also triggers rollback.
  5. Only after all reactors succeed are buffered events flushed to the original sink.
- `IDomainEventReactor` lets cross-module reactions (e.g. `QuestObjectiveTrackingReactor` watches `InventoryItemChangedEvent` and `QuestSignalEvent`) participate in the same atomic transaction.
- `DefaultCommandDispatcher.Create()` wires up every built-in handler and reactor — use it as the starting point and as the canonical list of which commands ship with the engine.

When adding a feature: write a new `ICommand`, a `ICommandHandler<TCommand>`, optional `DomainEvent`s, and register the handler in `DefaultCommandDispatcher.RegisterBuiltIns`. If the feature needs to react to *other* modules' events, write an `IDomainEventReactor` instead of adding cross-module calls.

### CommandContext and determinism

Every handler receives a `CommandContext` carrying the only non-`GameState` inputs allowed in gameplay logic:

- `IRandomSource` — typically `Pcg32RandomSource` (seeded PCG32 in `Runtime/Random/`). Never use `System.Random` or `Guid.NewGuid()` in engine code.
- `IGameClock` — typically `SimulationClock`. Never read `DateTime.Now` / `DateTimeOffset.UtcNow`.
- `IFormulaEvaluator` — used by derived stats and any formula-driven content. The engine ships `NoOpFormulaEvaluator` (placeholder) and `ExpressionFormulaEvaluator` (recursive-descent arithmetic parser supporting `+ - * / ( )` and identifiers).
- `IDomainEventSink` — the dispatcher swaps this for a buffered sink mid-transaction; handlers should just publish to it.
- `IGameDefinitionCatalog` — read-only content (item, quest, shop, loot, encounter, interactable, stat, status definitions) lives here, separate from mutable `GameState`. Default is `EmptyGameDefinitionCatalog`; use `InMemoryGameDefinitionCatalog` to register definitions.

This separation between **runtime state** (`GameState`, mutable) and **definitions/content** (`IGameDefinitionCatalog`, immutable per session) is load-bearing. Persistence only saves `GameState`; definitions are re-supplied at boot.

### Module layout

Each gameplay module under `src/Moonforge.Core/` follows the same shape: a state class hung off `GameState`, plus `Commands/`, `Events/`, and `Queries/` subfolders. Modules: `Combat`, `Crafting`, `Dialogue`, `Economy`, `Encounters`, `Equipment`, `Exploration`, `Interactables`, `Inventory`, `Loot`, `Progression`, `Quests`, `Shops`, `Stats`, `World`. Module integration happens through events + reactors, not direct references.

### Stats pipeline

`Stats/` implements an ordered modifier pipeline: `base → Flat → Add% → Mult% → Override`. Modifiers are sorted by `(Bucket, SourceKind, SourceId)` so the same set yields the same value regardless of insertion order — preserve this when touching modifier code. Derived stats (e.g. `MaxHp = vit * 10 + level * 5`) go through `IFormulaEvaluator`. See `docs/stats.md`.

### Persistence

`Persistence/JsonGameStateSerializer` round-trips `GameState` via `GameStateSnapshot` DTOs (`Persistence/Snapshots/`). `GameState.SchemaVersion` + the `SaveMigrationPipeline` (`ISaveMigration`) handle upgrading old saves to `GameStateSnapshotMapper.CurrentSchemaVersion`. When changing any persisted shape, bump the schema version and add a migration.

## Coding conventions

- Preserve deterministic behavior. No `System.Random`, `DateTime.Now`, unordered dictionary iteration that affects outputs, or hash-based ordering in gameplay paths.
- Respect command/query separation — queries never mutate; commands return `DomainResult` and publish events rather than throwing for expected failures.
- Don't introduce cross-module references from a handler; use events + a reactor.
- `netstandard2.1` constrains the engine project — avoid APIs only available in newer TFMs in `src/Moonforge.Core` (tests/samples on net8.0 are unconstrained).
- Add tests for new behaviors and bug fixes (xUnit, mirroring file-per-module naming under `tests/Moonforge.Core.Tests/`).
