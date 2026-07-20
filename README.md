# Moonforge RPG Engine

[![CI](https://github.com/3583Bytes/moonforge-rpg-engine/actions/workflows/ci.yml/badge.svg)](https://github.com/3583Bytes/moonforge-rpg-engine/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/Moonforge.Core.svg)](https://www.nuget.org/packages/Moonforge.Core)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Moonforge.Core.svg)](https://www.nuget.org/packages/Moonforge.Core)

**The turn-based RPG systems you'd otherwise build from scratch — combat, loot, quests, parties, shops, save/load — ready to drop into your C# or Unity game.**

Want to make a roguelike? A Pokémon-style monster catcher? A dungeon crawler with a
quest journal and a shop full of potions? Moonforge gives you the plumbing so you can
spend your time on the *game*, not on reinventing damage formulas, inventory stacking,
and quest tracking for the hundredth time.

It's deterministic (same inputs → same outcome, every time), so save/load, replays, and
debugging "wait, why did that crit?" all just work. And it's MIT-licensed and
Unity-friendly (`netstandard2.1`).

```bash
dotnet add package Moonforge.Core
```

> 👉 New here? Jump to **[Getting Started](docs/getting-started.md)** for the smallest
> working example, or run a [sample game](#sample-games) to see it all in action.

## What you get out of the box

Every one of these ships ready to use — no glue code between them required.

| System | What it does for your game |
|---|---|
| ⚔️ **Combat** | Turn order, skills, AI, status effects, damage types, resistances, a Pokémon-style type chart (2× / ½× / 0×), mid-battle swaps, capture, and per-move PP |
| 📊 **Stats** | Stat blocks with a clean modifier pipeline (Flat → Add% → Mult% → Override) and derived stats like `MaxHp = vit * 10 + level * 5` |
| 📜 **Quests** | Kill / Collect / Talk / Visit / composite objectives that **track themselves** from gameplay events — no manual "advance quest" calls |
| 💬 **Dialogue** | Branching trees with choices, conditions, and effects that can flip world flags |
| 💰 **Economy & Inventory** | Multi-currency wallets, stackable bags, atomic buy/sell that never leaves you half-charged |
| 🛡️ **Equipment** | Slots that feed stat bonuses straight into the stat block |
| ⭐ **Progression** | XP curves and level-up events |
| 🧑‍🤝‍🧑 **Party** | Active vs. reserve roster, synced to combat swap-ins |
| 🐣 **Evolution** | Auto-evolve on level-up, or trigger it manually |
| 📖 **Bestiary** | A codex that records what you've seen and caught, automatically |
| 🏪 **Shops** | Catalogs, stock, restocks, multi-currency prices |
| 🎁 **Loot** | Weighted tables, pick-one or roll-each, nested tables, conditions |
| 👹 **Encounters** | Weighted spawn tables with deterministic rolls |
| 🚪 **Interactables** | Chests, doors, levers, signs — declarative effect chains |
| 🌍 **World variables** | Typed flags, counters, and gates |
| 💾 **Persistence** | JSON save/load with schema versions and a migration pipeline for old saves |

## Show me

**Quests that track themselves.** Set up a "collect 3 potions" objective, then just play
the game. The moment the third potion lands in the bag, the quest completes — a built-in
reactor watches inventory events for you. No `AdvanceQuest()` call anywhere.

```csharp
dispatcher.Dispatch(gameState, new StartQuestCommand("quest.tutorial"), context);
dispatcher.Dispatch(gameState, new AddInventoryItemCommand("item.potion", 3), context);

// quest.tutorial is now Completed — automatically.
```

**Catch the enemy.** Weaken a wild monster, then have one of your party throw a ball. The
lower the target's HP the better the odds (ball quality folds into `bonusPercent`), and on
success capture, party, and bestiary all update together in one atomic step.

```csharp
dispatcher.Dispatch(gameState,
    new AttemptCaptureCommand(actorId: "hero", targetActorId: "enemy.slime", bonusPercent: 150),
    context);
```

**Open a chest, roll the table.** One command rolls a weighted loot table and drops the
results straight into the player's inventory.

```csharp
dispatcher.Dispatch(gameState, new RollAndGrantLootCommand("loot.chest.common"), context);
```

**Buy something — safely.** A purchase debits currency, adds the item, and updates shop
stock as a single transaction. Can't afford it, or bag is full? Nothing changes at all.

```csharp
dispatcher.Dispatch(gameState,
    new BuyFromShopCommand(shopId: "shop.town", itemId: "item.potion", quantity: 2),
    context);
```

Want the full, runnable version (catalog setup and all)? See the
[quest auto-tracking walkthrough](#full-example-quest-auto-tracking) below.

## Quick start

**.NET / NuGet:**

```bash
dotnet add package Moonforge.Core
```

Or clone the repo and reference `src/Moonforge.Core/Moonforge.Core.csproj` directly.

**Unity (UPM):** Package Manager → *Install package from git URL*:

```
https://github.com/3583Bytes/moonforge-rpg-engine.git?path=unity-packages/com.moonforge.core
```

The engine source lives at `unity-packages/com.moonforge.core/Runtime/` and is shared
verbatim between the NuGet pack and Unity — single source of truth, no duplication.
Unity 2022.3 LTS or newer.

Every game wires up the same three things once: a `GameState` (your save data), a
`CommandContext` (seeded RNG + clock + content catalog), and a `CommandDispatcher` (runs
commands). Then you dispatch commands and read back queries.

```csharp
GameState gameState = new();
InMemoryDomainEventSink sink = new();
InMemoryGameDefinitionCatalog definitions = new();   // register your content here

CommandContext context = new(
    new Pcg32RandomSource(seed: 1234),   // deterministic RNG
    new SimulationClock(0),              // deterministic clock
    new NoOpFormulaEvaluator(),
    sink,
    definitions);

CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
```

## Why it's built this way

Three design choices that quietly save you a lot of debugging:

- **Deterministic.** Seeded RNG and an explicit clock — never `System.Random` or
  `DateTime.Now`. Identical inputs always produce identical outputs, so saves are
  reproducible, replays are exact, and lockstep multiplayer is on the table.
- **Atomic.** Every command runs inside a snapshot/rollback transaction. If a handler
  (or a reactor) fails, state reverts and buffered events are discarded — no
  half-applied purchases, no orphaned quest progress, no hand-written undo path.
- **Composable.** Modules talk through a typed event bus and reactors, not direct calls.
  Combat doesn't know quests exist; quests don't know about inventory — yet picking up an
  item still advances a quest. Add your own commands, events, and reactors the same way.

The deeper version of all this lives in
**[Architecture](docs/architecture.md)** and **[Pipeline](docs/pipeline.md)**.

## Full example: quest auto-tracking

The complete, copy-paste-runnable version of the self-tracking quest above:

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
    .AddItem(new ItemDefinition("item.potion", stackLimit: 10))
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

## Sample games

The fastest way to see what the engine can do is to play with it. All three run from a
single `dotnet run`:

```bash
# Full roguelike — town, procedurally-generated dungeons, quests, dialogue, save/load,
# combat with elemental damage types and fire-immune bosses.
dotnet run --project samples/Moonforge.Sample.Roguelike.Console

# Monster catcher — Pokémon-style: procedural 45-screen world, eight gym leaders,
# a Champion ending, shops, items, the works.
dotnet run --project samples/Moonforge.Sample.MonsterCatcher.Console

# Minimal API demo — the tiniest copy-paste starting point.
dotnet run --project samples/Moonforge.Sample.Minimal
```

There's also a **Unity port of the roguelike** under
`unity-packages/com.moonforge.core/Samples~/Roguelike/` — the same `RoguelikeSession`
game logic the console sample uses, rendered through a runtime-built `Tilemap` with the
bundled Kenney 1-Bit Pack and a TextMeshPro HUD, with hybrid mouse + keyboard input.
After installing Moonforge.Core via the Unity Package Manager, click **Import** next to
the Roguelike sample. See
[Samples~/Roguelike/README.md](unity-packages/com.moonforge.core/Samples~/Roguelike/README.md)
for setup and controls.

**The roguelike console sample is the reference for how everything fits together** — a
stat block with derived `MaxHp`, a shop with multi-currency prices, locked interactables,
save migrations, weighted encounter tables, status effects, elemental resistances. When
a doc describes a pattern, this sample has the production-grade version.

**The monster catcher** is a small Pokémon-style game built end-to-end on the engine: a
procedural 45-screen world across seven biomes, eight themed gym leaders with badge
gating, town shops with tiered balls and potions, an "Eight Wardens" main quest tracked
by the quest reactor, faint-warp on a party wipe, and a five-monster Champion battle. It
wires the full catcher stack (party + swap + type chart + capture + evolution + bestiary
+ per-skill PP) together with Quests / Shops / Inventory / Economy / Loot / Dialogue /
Interactables. See [the walkthrough](docs/monster-catcher-sample.md).

## Documentation

Start with the **[docs index](docs/README.md)**.

- [Getting Started](docs/getting-started.md) — first-time setup, the smallest working dispatch
- [Architecture](docs/architecture.md) — `GameState`, the command/query/reactor pipeline, the determinism contract
- [Cookbook](docs/cookbook.md) — recipes for common gameplay tasks
- [Troubleshooting](docs/troubleshooting.md) — gotchas and common errors

Per-module deep dives: [combat](docs/combat.md), [stats](docs/stats.md),
[quests](docs/quests.md), [dialogue](docs/dialogue.md), [economy & inventory](docs/economy.md),
[equipment](docs/equipment.md), [progression](docs/progression.md), [shops](docs/shops.md),
[loot](docs/loot.md), [encounters](docs/encounters.md), [interactables](docs/interactables.md),
[world variables](docs/world.md), [persistence](docs/persistence.md),
[party](docs/party.md), [evolution](docs/evolution.md), [bestiary](docs/bestiary.md).

## Building from source

```bash
dotnet restore src/Moonforge.Core/Moonforge.Core.csproj
dotnet build   src/Moonforge.Core/Moonforge.Core.csproj -c Release
dotnet test    tests/Moonforge.Core.Tests/Moonforge.Core.Tests.csproj -c Release
dotnet test    tests/Moonforge.Sample.Roguelike.Console.Tests/Moonforge.Sample.Roguelike.Console.Tests.csproj -c Release
```

Requires the .NET 8 SDK or later. To build via the solution file (`Moonforge.slnx`) you
need the .NET 9 SDK or later; the per-project commands above work on .NET 8.

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
src/Moonforge.Core/                               the .NET / NuGet build project (compiles from Runtime/)
samples/Moonforge.Sample.Roguelike.Console/       roguelike reference sample (~3.5k lines)
samples/Moonforge.Sample.MonsterCatcher.Console/  monster-catcher Pokémon-style game (~3.3k lines)
samples/Moonforge.Sample.Minimal/                 minimal API demo
tests/Moonforge.Core.Tests/                            engine unit + behavior tests (xUnit)
tests/Moonforge.Sample.Roguelike.Console.Tests/        roguelike sample-level tests
tests/Moonforge.Sample.MonsterCatcher.Console.Tests/   monster-catcher smoke test
docs/                                             guides, architecture, cookbook, per-module deep dives
```

The engine source under `unity-packages/com.moonforge.core/Runtime/` is the canonical
location; `src/Moonforge.Core/Moonforge.Core.csproj` compiles those same files into the
NuGet, and Unity compiles them in-place via the package's `asmdef`.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development workflow, standards, and pull
request expectations.

## Security

See [SECURITY.md](SECURITY.md) for responsible disclosure guidance.

## License

MIT. See [LICENSE](LICENSE).
