# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Moonforge is a deterministic, modular RPG engine shipped two ways from one source tree: the `Moonforge.Core` NuGet package and the `com.moonforge.core` Unity Package Manager package. Engine source lives under `packages/com.moonforge.core/Runtime/` and is consumed by Unity directly via the `Moonforge.Core.asmdef`; the `src/Moonforge.Core/Moonforge.Core.csproj` project targets `netstandard2.1` and compiles those same files for the NuGet pack and the .NET tests/samples. Tests and samples target `net8.0`. Determinism (seeded RNG, explicit clock, no wall-clock or `DateTime.Now`) is a hard design constraint — gameplay logic must produce identical results from identical inputs.

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
dotnet test tests/Moonforge.Sample.MonsterCatcher.Console.Tests/Moonforge.Sample.MonsterCatcher.Console.Tests.csproj -c Release

# Run a single xUnit test by fully-qualified name (or substring)
dotnet test tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj --filter "FullyQualifiedName~CombatTests.Skill_Damage_Is_Deterministic"

# Run a single test class
dotnet test tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj --filter "FullyQualifiedName~LootTests"

# Run the samples
dotnet run --project samples/Moonforge.Sample.Console
dotnet run --project samples/Moonforge.Sample.MonsterCatcher.Console
dotnet run --project samples/Moonforge.Sample.Minimal

# Pack a NuGet
dotnet pack src/Moonforge.Core/Moonforge.Core.csproj -c Release -o artifacts
```

The Unity Roguelike sample under `packages/com.moonforge.core/Samples~/Roguelike/` can't be built from the command line; it runs only when the package is imported into a Unity 2022.3+ project. The console sample at `samples/Moonforge.Sample.Console` is the canonical reference for the same game.

CI (`.github/workflows/ci.yml`) runs on `windows-latest` with .NET 10 SDK and builds via `Moonforge.slnx`.

## Architecture

### Command/Query + Reactor pipeline

All gameplay state lives on a single aggregate `GameState` (`packages/com.moonforge.core/Runtime/GameState.cs`). Mutation flows exclusively through commands; reads go through queries. The runtime contract is in `packages/com.moonforge.core/Runtime/Runtime/`:

- `ICommand` / `ICommandHandler<TCommand>` — handlers mutate `GameState` and return `DomainResult` (success or `DomainError` with a `DomainErrorCode`).
- `CommandDispatcher` (`packages/com.moonforge.core/Runtime/Runtime/Commands/CommandDispatcher.cs`) is the transactional core. For every dispatch it:
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

- `IRandomSource` — typically `Pcg32RandomSource` (seeded PCG32 in `packages/com.moonforge.core/Runtime/Runtime/Random/`). Never use `System.Random` or `Guid.NewGuid()` in engine code.
- `IGameClock` — typically `SimulationClock`. Never read `DateTime.Now` / `DateTimeOffset.UtcNow`.
- `IFormulaEvaluator` — used by derived stats and any formula-driven content. The engine ships `NoOpFormulaEvaluator` (placeholder) and `ExpressionFormulaEvaluator` (recursive-descent arithmetic parser supporting `+ - * / ( )` and identifiers).
- `IDomainEventSink` — the dispatcher swaps this for a buffered sink mid-transaction; handlers should just publish to it.
- `IGameDefinitionCatalog` — read-only content (item, quest, shop, loot, encounter, interactable, stat, status definitions) lives here, separate from mutable `GameState`. Default is `EmptyGameDefinitionCatalog`; use `InMemoryGameDefinitionCatalog` to register definitions.

This separation between **runtime state** (`GameState`, mutable) and **definitions/content** (`IGameDefinitionCatalog`, immutable per session) is load-bearing. Persistence only saves `GameState`; definitions are re-supplied at boot.

### Module layout

Each gameplay module under `packages/com.moonforge.core/Runtime/` follows the same shape: a state class hung off `GameState`, plus `Commands/`, `Events/`, and `Queries/` subfolders. Modules: `Bestiary`, `Combat`, `Crafting`, `Dialogue`, `Economy`, `Encounters`, `Equipment`, `Evolution`, `Exploration`, `Interactables`, `Inventory`, `Loot`, `Party`, `Progression`, `Quests`, `Shops`, `Stats`, `World`. Module integration happens through events + reactors, not direct references.

The monster-catcher feature stack (`Bestiary`, `Evolution`, `Party`, plus capture/swap/PP/type-chart additions in `Combat`) — see `docs/party.md`, `docs/evolution.md`, `docs/bestiary.md`, and the type-chart / capture / swap / PP sections in `docs/combat.md`. `samples/Moonforge.Sample.MonsterCatcher.Console` is the reference end-to-end consumer for all of them, expanded into a small Pokemon-style game with a procedurally-generated 45-screen world, eight gym leaders, the "Eight Wardens" main quest, town shops with tiered items, and a Champion ending — see `docs/monster-catcher-sample.md` for the walkthrough.

### Unity sample stack

The Unity port of the roguelike (`packages/com.moonforge.core/Samples~/Roguelike/`) sits on top of two shared-library pieces that both samples consume from a single source:

- **`Samples~/Roguelike/Shared/`** — `Roguelike.Shared.asmdef` (`noEngineReferences: true`). Contains `RoguelikeContent` (id constants, dialogue text, class/gear/meta-unlock catalogs, `BuildCatalog()`), `RoguelikeSession` (the headless game — owns `GameState`, all helpers, exposes `Enter()` / `Tick(PlayerAction)` / `CurrentScene` / `IsBattleAiTurn`), `IRoguelikeHost` (the rendering boundary), `WorldGen/` (Dungeon/Town/EncounterGenerator), `Persistence/RoguelikeSaveStore`, and the render-model + snapshot records.
- **`Samples~/Roguelike/Scripts/`** — `Roguelike.asmdef` references `Roguelike.Shared` + Unity TMP. `RoguelikeBootstrap` is the only `MonoBehaviour`: builds the Camera/Grid/Tilemap/Canvas at runtime, implements `IRoguelikeHost` by painting tilemap+sprites or HUD text, drives the session from `Update()` (polls input via `PlayerInputAdapter`, ticks AI battles automatically). Menu-style scenes spawn clickable TMP buttons; Town/Dungeon/Battle are keyboard-only.

`samples/Moonforge.Sample.Console/GameLoop/RoguelikeGame.cs` consumes the same `Shared/` source via its csproj's `<Compile Include="..\..\packages\com.moonforge.core\Samples~\Roguelike\Shared\**\*.cs" />` glob — it's just a Spectre.Console-backed `IRoguelikeHost`. Adding a new scene or changing game logic only touches `Shared/`; both samples pick it up.

### Stats pipeline

`Stats/` implements an ordered modifier pipeline: `base → Flat → Add% → Mult% → Override`. Modifiers are sorted by `(Bucket, SourceKind, SourceId)` so the same set yields the same value regardless of insertion order — preserve this when touching modifier code. Derived stats (e.g. `MaxHp = vit * 10 + level * 5`) go through `IFormulaEvaluator`. See `docs/stats.md`.

### Persistence

`Persistence/JsonGameStateSerializer` round-trips `GameState` via `GameStateSnapshot` DTOs (`Persistence/Snapshots/`). `GameState.SchemaVersion` + the `SaveMigrationPipeline` (`ISaveMigration`) handle upgrading old saves to `GameStateSnapshotMapper.CurrentSchemaVersion`. When changing any persisted shape, bump the schema version and add a migration.

## Coding conventions

- Preserve deterministic behavior. No `System.Random`, `DateTime.Now`, unordered dictionary iteration that affects outputs, or hash-based ordering in gameplay paths.
- Respect command/query separation — queries never mutate; commands return `DomainResult` and publish events rather than throwing for expected failures.
- Don't introduce cross-module references from a handler; use events + a reactor.
- `netstandard2.1` constrains the engine project — avoid APIs only available in newer TFMs in `packages/com.moonforge.core/Runtime/` (tests/samples on net8.0 are unconstrained). Unity 2022.3 LTS is the other consumer, which means engine code must also stay within C# 9 / Unity-compatible APIs.
- Add tests for new behaviors and bug fixes (xUnit, mirroring file-per-module naming under `tests/Moonforge.Core.Tests/`).
