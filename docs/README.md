# Moonforge Documentation

A deterministic, modular RPG engine for C# projects. These guides cover the engine's
architecture and each gameplay subsystem.

## Start here

| Doc | When to read |
|---|---|
| [Getting Started](getting-started.md) | First-time setup. The smallest working dispatch, end to end. |
| [Architecture](architecture.md) | How `GameState`, commands, queries, and reactors fit together. Read before integrating into a game. |
| [Pipeline](pipeline.md) | The command/query/reactor pipeline in detail — when to write each. |
| [Cookbook](cookbook.md) | Recipes for common gameplay tasks. Copy-paste targets. |
| [Troubleshooting](troubleshooting.md) | Common errors, footguns, and how to diagnose them. |

## Per-module guides

| Module | What lives there |
|---|---|
| [Combat](combat.md) | Battles, turn order, skills, AI, status effects, damage types, resistances |
| [Stats](stats.md) | Stat blocks, modifier pipeline (Flat → Add% → Mult% → Override), derived stats, damage-type resolution |
| [Quests](quests.md) | Objectives (Kill / Collect / Talk / Visit / composite), signals, rewards, auto-tracking |
| [Dialogue](dialogue.md) | Nodes, choices, conditions, effects, world-variable side effects |
| [Economy](economy.md) | Currencies, atomic transactions, inventory bag and stacks |
| [Equipment](equipment.md) | Slots, equip/unequip, stat-bonus integration with the stat block |
| [Progression](progression.md) | Experience curves, level-up events |
| [Shops](shops.md) | Shop catalogs, stock, restocks, multi-currency prices |
| [Loot](loot.md) | Weighted tables, RollEach / PickOne, nested tables, conditions |
| [Encounters](encounters.md) | Weighted enemy spawn manifests, deterministic rolls |
| [Interactables](interactables.md) | Chests, doors, levers, signs; declarative effect chains |
| [World variables](world.md) | Typed key/value state for flags, counters, gates |
| [Persistence](persistence.md) | JSON snapshots, schema versions, migration pipeline |

## Conventions

All examples assume these `using` directives unless noted:

```csharp
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
```

A typical setup creates these once per game session and threads them through commands:

```csharp
GameState gameState = new();
InMemoryDomainEventSink sink = new();
InMemoryGameDefinitionCatalog definitions = new();  // register definitions here

CommandContext context = new(
    new Pcg32RandomSource(seed: 1234),
    new SimulationClock(0),
    new NoOpFormulaEvaluator(),
    sink,
    definitions);

CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
```
