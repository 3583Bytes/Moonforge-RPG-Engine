# Moonforge.Core

A deterministic, modular RPG engine for C# projects: combat, stats, inventory, dialogue,
quests, exploration, loot, encounters, interactables, persistence, and damage-type
resistance — built around a command/query/reactor pipeline with atomic state mutation.

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

GameState gameState = new();
InMemoryDomainEventSink sink = new();

CommandContext context = new(
    new Pcg32RandomSource(seed: 1234),
    new SimulationClock(0),
    new NoOpFormulaEvaluator(),
    sink);

CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();

dispatcher.Dispatch(gameState, new GrantCurrencyCommand("gold", 100), context);

long gold = new GetCurrencyBalanceQueryHandler()
    .Query(gameState, new GetCurrencyBalanceQuery("gold"));

System.Console.WriteLine($"Gold: {gold}");  // Gold: 100
```

## Why Moonforge

- **Deterministic** — seeded `IRandomSource` and explicit `IGameClock`; same inputs always
  produce the same outputs. Critical for save-resilience, replay, lockstep multiplayer.
- **Atomic** — every command runs inside a snapshot-rollback transaction; failures revert
  state and discard buffered events.
- **Composable** — modules integrate via `IDomainEventReactor`, never direct calls.
- **Pluggable** — bring your own formula evaluator, custom commands, custom reactors.

## Documentation

Full guides, architecture deep-dive, cookbook, and examples are in the
[repository](https://github.com/3583Bytes/moonforge-rpg-engine/blob/main/docs/README.md).

## License

MIT.
