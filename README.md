# Moonforge RPG Engine

[![CI](https://github.com/3583Bytes/moonforge-rpg-engine/actions/workflows/ci.yml/badge.svg)](https://github.com/3583Bytes/moonforge-rpg-engine/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/Moonforge.Core.svg)](https://www.nuget.org/packages/Moonforge.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Moonforge.Core.svg)](https://www.nuget.org/packages/Moonforge.Core)

Moonforge is a deterministic, modular RPG engine for C# projects. It gives you battle, economy, inventory, dialogue, quests, exploration, persistence, and domain-event workflows so you can focus on content instead of rewriting core systems.

## Why Moonforge

- Deterministic simulation with seeded random and explicit clock control.
- Strong domain boundaries with command/query separation.
- Atomic state mutation with rollback on failure.
- Event-driven module integration (combat, quests, dialogue, economy, and more).
- Unity-friendly core target (`netstandard2.1`).

## Feature Areas

- Combat: turn order, AI turns, skill execution, cooldown/resource handling.
- Quests and dialogue: objective tracking, branching choices, and side effects.
- Economy and inventory: currencies, transaction safety, slot and stack rules.
- Exploration: tile maps, actor movement validation, world interactions.
- Stats: per-actor stat blocks with stored primaries, derived stats via formulas, and an
  ordered modifier pipeline (Flat → Add% → Mult% → Override) that aggregates equipment,
  status, and ad-hoc buffs deterministically.
- Damage types & resistances: string-keyed damage types with optional flat defense plus
  percent resistance, immunity at 100, vulnerability below 0.
- Loot: weighted/independent drop tables with conditions, nested tables, and atomic
  wallet+bag deposits.
- Encounters: weighted spawn tables that produce deterministic enemy manifests, sharing the
  same roll-mode vocabulary as loot tables.
- Interactables: world objects on the map (chests, doors, levers, signs, pickups) with
  declarative effects that compose with loot tables, inventory keys, and world state.
- Persistence: JSON snapshots and migration pipeline primitives.

## Quick Start

### Prerequisites

- .NET SDK 8.0 or later.
- Optional: .NET SDK 9.0+ if you want to use `Moonforge.slnx` directly.

### Build and test

```bash
dotnet restore src/Moonforge.Core/Moonforge.Core.csproj
dotnet build src/Moonforge.Core/Moonforge.Core.csproj -c Release
dotnet test tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj -c Release
dotnet test tests/Moonforge.Sample.Console.Tests/Moonforge.Sample.Console.Tests.csproj -c Release
```

### Run the examples

```bash
# Full interactive roguelike sample
dotnet run --project samples/Moonforge.Sample.Console

# Minimal API sample
dotnet run --project samples/Moonforge.Sample.Minimal
```

## Minimal Usage Example

```csharp
using Moonforge.Core;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.World;
using Moonforge.Core.World.Commands;
using Moonforge.Core.World.Queries;

GameState gameState = new();
InMemoryDomainEventSink sink = new();

CommandContext context = new(
    new Pcg32RandomSource(seed: 1234, sequence: 54),
    new SimulationClock(0),
    new NoOpFormulaEvaluator(),
    sink);

CommandDispatcher dispatcher = new();
dispatcher.Register(new SetWorldVariableCommandHandler());

var result = dispatcher.Dispatch(
    gameState,
    new SetWorldVariableCommand("quest.main.started", WorldVariableValue.FromBool(true)),
    context);

if (!result.IsSuccess)
{
    Console.WriteLine($"Dispatch failed: {result.Error?.Code}");
    return;
}

GetWorldVariableQueryHandler worldQuery = new();
WorldVariableValue? value = worldQuery.Query(gameState, new GetWorldVariableQuery("quest.main.started"));
Console.WriteLine($"Quest started: {value?.TryGetBool(out bool started) == true && started}");
```

## Documentation

Start with the **[docs index](docs/README.md)**. Highlights:

- [Getting Started](docs/getting-started.md) — first-time setup, the smallest working dispatch
- [Architecture](docs/architecture.md) — `GameState`, command/query/reactor pipeline, determinism contract
- [Cookbook](docs/cookbook.md) — recipes for common gameplay tasks
- [Troubleshooting](docs/troubleshooting.md) — gotchas and common errors

Per-module deep dives: [combat](docs/combat.md), [stats](docs/stats.md),
[quests](docs/quests.md), [dialogue](docs/dialogue.md), [economy & inventory](docs/economy.md),
[equipment](docs/equipment.md), [progression](docs/progression.md), [shops](docs/shops.md),
[loot](docs/loot.md), [encounters](docs/encounters.md), [interactables](docs/interactables.md),
[world variables](docs/world.md), [persistence](docs/persistence.md).

## Package

Create a NuGet package from `Moonforge.Core`:

```bash
dotnet pack src/Moonforge.Core/Moonforge.Core.csproj -c Release -o artifacts
```

Release notes are categorized automatically by [.github/release.yml](.github/release.yml) when generating a GitHub release.

## Repository Layout

- `src/Moonforge.Core`: engine runtime and domain modules.
- `samples/Moonforge.Sample.Console`: full interactive sample game.
- `samples/Moonforge.Sample.Minimal`: minimal command/query sample.
- `tests/Moonforge.Core.Tests`: engine unit and behavior tests.
- `tests/Moonforge.Sample.Console.Tests`: sample-level tests.
- `docs`: guides and maintainers documentation.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development workflow, standards, and pull request expectations.

## Security

See [SECURITY.md](SECURITY.md) for responsible disclosure guidance.

## License

MIT. See [LICENSE](LICENSE).
