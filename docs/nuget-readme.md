# Moonforge.Core

A modular RPG engine for C# projects with batteries-included gameplay systems: turn-based
combat (status effects, damage types, a Pokemon-style type effectiveness chart, mid-battle
swap, capture, and per-move PP), a stat-modifier pipeline, inventory, equipment, crafting,
dialogue, quests, economy, shops, loot, encounters, exploration, interactables, party
rosters, evolution, bestiary, and versioned JSON saves. Modules compose through a
command/query/reactor pipeline with atomic, deterministic state mutation.

Target framework: `netstandard2.1` (Unity-friendly).

## Install

```
dotnet add package Moonforge.Core
```

## Hello world

```csharp
using Moonforge.Core;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Economy.Queries;

GameState gameState = new();              // the single mutable aggregate
InMemoryDomainEventSink sink = new();      // collects events the host can drain

// Bundle the deterministic inputs handlers are allowed to use.
CommandContext context = new(
    new Pcg32RandomSource(seed: 1234),     // seeded RNG → reproducible results
    new SimulationClock(0),                // simulated time, starting at minute 0
    new NoOpFormulaEvaluator(),            // placeholder — returns 0 for every formula
    sink);                                 // 4-arg ctor defaults Definitions to the empty catalog

// Create() pre-registers every built-in command handler and reactor.
CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();

// Mutate state only through a command; returns a DomainResult (ignored here).
dispatcher.Dispatch(gameState, new GrantCurrencyCommand("gold", 100), context);

// Read state through a query — instantiate the handler and call Query directly.
long gold = new GetCurrencyBalanceQueryHandler()
    .Query(gameState, new GetCurrencyBalanceQuery("gold"));

System.Console.WriteLine($"Gold: {gold}");  // Gold: 100
```

## Why Moonforge

- **Batteries included** — combat, stats, inventory, equipment, crafting, dialogue, quests,
  economy, shops, loot, encounters, exploration, party, evolution, bestiary, and persistence
  ship in the box.
- **Composable** — modules integrate via `IDomainEventReactor`, never direct calls.
- **Pluggable** — bring your own formula evaluator, custom commands, custom reactors.
- **Atomic** — every command runs inside a snapshot-rollback transaction; failures revert
  state and discard buffered events.
- **Deterministic** — seeded `IRandomSource` and explicit `IGameClock`; same inputs always
  produce the same outputs. Critical for save-resilience, replay, lockstep multiplayer.

## Documentation

Full guides, architecture deep-dive, cookbook, and examples are in the
[repository](https://github.com/3583Bytes/moonforge-rpg-engine/blob/main/docs/README.md).

## License

MIT.
