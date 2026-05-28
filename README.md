# Moonforge RPG Engine

[![CI](https://github.com/3583Bytes/moonforge-rpg-engine/actions/workflows/ci.yml/badge.svg)](https://github.com/3583Bytes/moonforge-rpg-engine/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/Moonforge.Core.svg)](https://www.nuget.org/packages/Moonforge.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Moonforge.Core.svg)](https://www.nuget.org/packages/Moonforge.Core)

Moonforge is a C# game engine for turn-based RPGs — the kind with parties, stat blocks,
quest journals, and treasure chests.

It ships the systems every RPG re-invents: combat with status effects and damage types
(including a Pokemon-style 2× / 0.5× / 0× type effectiveness chart), per-monster move PP,
parties with mid-battle swap-in, capture-the-enemy into your roster, evolution on
level-up, a bestiary / codex that tracks itself, an inventory and economy that survives
the player buying things on a full bag, quests that track themselves from gameplay
events, dialogue trees, loot tables, save/load with migrations. All deterministic, all
atomic, all wired together so modules don't need to know about each other.

Unity-friendly (`netstandard2.1`), MIT-licensed.

## Highlights

- **Deterministic.** Seeded RNG, explicit clock — same inputs always produce the same
  outputs. Save resilience, replay, and lockstep multiplayer just work.
- **Atomic.** Every command runs inside a snapshot/rollback transaction. Failures revert
  state and discard buffered events — no half-applied purchases, no orphaned quest
  progress, no need to write your own undo path.
- **Composable.** Modules integrate through a typed event bus and a reactor model. Combat
  doesn't know about quests; quests don't know about inventory; everything still wires up.
- **Pluggable.** Bring your own formula evaluator, your own RNG, your own commands,
  reactors, and event types. The engine ships defaults you can replace one at a time.

## Install

**.NET / NuGet:**

```
dotnet add package Moonforge.Core
```

Or clone the repo and reference `src/Moonforge.Core/Moonforge.Core.csproj` directly.

**Unity (UPM):** Add via the Package Manager → Install package from git URL:

```
https://github.com/3583Bytes/moonforge-rpg-engine.git?path=unity-packages/com.moonforge.core
```

The engine source lives at `unity-packages/com.moonforge.core/Runtime/` and is shared
verbatim between the NuGet pack and Unity — single source of truth, no
duplication. Unity 2022.3 LTS or newer.

## A taste

The quest below auto-completes the moment the player picks up their third potion. No
explicit "advance the quest" call — a built-in reactor watches inventory events and
tracks `Collect` objectives for you.

```csharp
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Quests.Queries;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;

GameState gameState = new();
InMemoryDomainEventSink sink = new();

InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
    .AddItem(new ItemDefinition("item.potion", maxStack: 10))
    .AddQuest(new QuestDefinition(
        id: "quest.tutorial",
        objectives:
        [
            new QuestObjectiveDefinition(
                id: "obj.collect",
                objectiveType: QuestObjectiveType.Collect,
                targetId: "item.potion",
                requiredCount: 3,
                displayName: "Collect 3 potions")
        ],
        displayName: "Stock the larder"));

CommandContext context = new(
    new Pcg32RandomSource(seed: 1234),
    new SimulationClock(0),
    new NoOpFormulaEvaluator(),
    sink,
    definitions);

CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();

dispatcher.Dispatch(gameState, new ConfigureInventoryCapacityCommand(20), context);
dispatcher.Dispatch(gameState, new StartQuestCommand("quest.tutorial"), context);
dispatcher.Dispatch(gameState, new AddInventoryItemCommand("item.potion", 3), context);

QuestStatus status = new GetQuestStatusQueryHandler()
    .Query(gameState, new GetQuestStatusQuery("quest.tutorial"));

System.Console.WriteLine($"Quest: {status}");   // Quest: Completed
```

## Documentation

Start with the **[docs index](docs/README.md)**.

- [Getting Started](docs/getting-started.md) — first-time setup, the smallest working dispatch
- [Architecture](docs/architecture.md) — `GameState`, command/query/reactor pipeline, determinism contract
- [Cookbook](docs/cookbook.md) — recipes for common gameplay tasks
- [Troubleshooting](docs/troubleshooting.md) — gotchas and common errors

Per-module deep dives: [combat](docs/combat.md), [stats](docs/stats.md),
[quests](docs/quests.md), [dialogue](docs/dialogue.md), [economy & inventory](docs/economy.md),
[equipment](docs/equipment.md), [progression](docs/progression.md), [shops](docs/shops.md),
[loot](docs/loot.md), [encounters](docs/encounters.md), [interactables](docs/interactables.md),
[world variables](docs/world.md), [persistence](docs/persistence.md),
[party](docs/party.md), [evolution](docs/evolution.md), [bestiary](docs/bestiary.md).

## Sample games

```bash
# Full roguelike — town, procedurally-generated dungeons, quests, dialogue, save/load,
# combat with elemental damage types and fire-immune bosses.
dotnet run --project samples/Moonforge.Sample.Roguelike.Console

# Monster catcher — Pokemon-style game: procedural 45-screen world, eight gym leaders,
# Champion ending, shops, items, the works.
dotnet run --project samples/Moonforge.Sample.MonsterCatcher.Console

# Minimal API demo — copy-paste starting point.
dotnet run --project samples/Moonforge.Sample.Minimal
```

There's also a **Unity port of the roguelike** under
`unity-packages/com.moonforge.core/Samples~/Roguelike/` — the same `RoguelikeSession`
game logic the console sample uses, rendered through a runtime-built
`Tilemap` with the bundled Kenney 1-Bit Pack and a TextMeshPro HUD, hybrid
mouse + keyboard input. After installing Moonforge.Core via the Unity Package
Manager, click **Import** next to the Roguelike sample. See
[Samples~/Roguelike/README.md](unity-packages/com.moonforge.core/Samples~/Roguelike/README.md)
for the setup walkthrough and control reference.

`samples/Moonforge.Sample.Roguelike.Console` is the reference for how every engine subsystem fits
together: a stat block with derived `MaxHp`, a shop with multi-currency prices, locked
interactables, save migrations, weighted encounter tables, status effects, elemental
resistances. When the docs describe a pattern, this sample has the production-grade
version.

`samples/Moonforge.Sample.MonsterCatcher.Console` is a small Pokemon-style game built
end-to-end on the engine — procedurally-generated 45-screen world across seven biomes,
eight themed gym leaders with multi-monster rosters and badge gating, town shops with
tiered capture balls and potions, an "eight wardens" main quest tracked by the engine's
quest reactor, Pokemon-style faint-warp on party wipe, and a five-mon Champion battle.
The full monster-catcher feature stack (party + swap + type chart + capture + evolution
+ bestiary + per-skill PP) is wired together with the Quests / Shops / Inventory /
Economy / Loot / Dialogue / Interactables modules. See
[the sample walkthrough](docs/monster-catcher-sample.md) for the layout.

## Building from source

```bash
dotnet restore src/Moonforge.Core/Moonforge.Core.csproj
dotnet build   src/Moonforge.Core/Moonforge.Core.csproj -c Release
dotnet test    tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj -c Release
dotnet test    tests/Moonforge.Sample.Roguelike.Console.Tests/Moonforge.Sample.Roguelike.Console.Tests.csproj -c Release
```

Requires the .NET 8 SDK or later. To build via the solution file (`Moonforge.slnx`) you
need .NET 9 SDK or later; the per-project commands above work on .NET 8.

To produce a NuGet package:

```bash
dotnet pack src/Moonforge.Core/Moonforge.Core.csproj -c Release -o artifacts
```

Release notes are auto-categorized by [`.github/release.yml`](.github/release.yml) when
generating a GitHub release.

## Repository layout

```
unity-packages/com.moonforge.core/Runtime/       the engine source — one folder per gameplay module
unity-packages/com.moonforge.core/Samples~/      Unity-importable samples (e.g. Roguelike)
src/Moonforge.Core/                        the .NET / NuGet build project (compiles from Runtime/)
samples/Moonforge.Sample.Roguelike.Console/      roguelike reference sample (~3.5k lines)
samples/Moonforge.Sample.MonsterCatcher.Console/ monster-catcher Pokemon-style game (~3.3k lines)
samples/Moonforge.Sample.Minimal/                minimal API demo
tests/Moonforge.Core.Tests/                            engine unit + behavior tests (xUnit)
tests/Moonforge.Sample.Roguelike.Console.Tests/        roguelike sample-level tests
tests/Moonforge.Sample.MonsterCatcher.Console.Tests/   monster-catcher smoke test
docs/                         guides, architecture, cookbook, per-module deep dives
```

The engine source under `unity-packages/com.moonforge.core/Runtime/` is the canonical
location; `src/Moonforge.Core/Moonforge.Core.csproj` compiles those same files
into the NuGet, and Unity compiles them in-place via the package's `asmdef`.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development workflow, standards, and pull
request expectations.

## Security

See [SECURITY.md](SECURITY.md) for responsible disclosure guidance.

## License

MIT. See [LICENSE](LICENSE).
