# Getting Started

This walkthrough gets you from "nothing installed" to a hero gaining gold, taking a quest,
fighting a battle, and reading the result — entirely through the engine.

## Prerequisites

- .NET SDK 8.0 or later. Optional: SDK 9.0+ to build `Moonforge.slnx` directly.
- A code editor — Visual Studio, VS Code, or Rider.

## 1. Install

In your project:

```
dotnet add package Moonforge.Core
```

Or clone the repo and reference `src/Moonforge.Core/Moonforge.Core.csproj` directly.

## 2. Build the boilerplate context

Every interaction with the engine goes through three objects:

- A **`GameState`** — the aggregate root that holds all mutable gameplay state.
- A **`CommandContext`** — the carrier for non-state inputs (RNG, clock, formula evaluator,
  event sink, definitions catalog).
- A **`CommandDispatcher`** — dispatches commands through registered handlers.

```csharp
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;

GameState gameState = new();
InMemoryDomainEventSink eventSink = new();
InMemoryGameDefinitionCatalog definitions = new();

CommandContext context = new(
    randomSource: new Pcg32RandomSource(seed: 1234),  // seeded RNG → same seed yields identical runs
    clock: new SimulationClock(0),                    // explicit clock; never reads wall-clock time
    formulaEvaluator: new NoOpFormulaEvaluator(),     // placeholder; derived-stat formulas need a real one
    eventSink: eventSink,
    definitions: definitions);

CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
```

`DefaultCommandDispatcher.Create()` wires every built-in handler and reactor. You can build
your own dispatcher if you only need a subset, but for a tutorial this is the path of least
friction.

## 3. Register content

Definitions are the immutable, designer-authored part of your game: items, quests, shops,
loot tables, damage types. Register them on the catalog once at startup:

```csharp
using Moonforge.Core.Combat;
using Moonforge.Core.Quests;

definitions
    .AddCurrency(new CurrencyDefinition("gold", maxBalance: 999_999))  // balance is capped at this max
    .AddItem(new ItemDefinition("item.potion", stackLimit: 10))        // up to 10 per inventory slot
    .AddQuest(new QuestDefinition(
        id: "quest.tutorial",
        objectives:
        [
            // Collect objective: completes once 3 of item.potion are held.
            new QuestObjectiveDefinition(
                id: "obj.collect.potions",
                objectiveType: QuestObjectiveType.Collect,
                targetId: "item.potion",
                requiredCount: 3,
                displayName: "Collect 3 potions")
        ],
        displayName: "Stock the larder",
        rewardCurrency: [new Economy.Commands.CurrencyDelta("gold", 50)]));  // granted on reward claim
```

## 4. Mutate state through commands

```csharp
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Runtime.Results;

// Set up the player's inventory.
dispatcher.Dispatch(gameState, new ConfigureInventoryCapacityCommand(20), context);

// Start the tutorial quest.
dispatcher.Dispatch(gameState, new StartQuestCommand("quest.tutorial"), context);

// Pick up three potions. The QuestObjectiveTrackingReactor watches inventory events and
// advances the Collect objective automatically.
DomainResult result = dispatcher.Dispatch(
    gameState,
    new AddInventoryItemCommand("item.potion", quantity: 3),
    context);

System.Console.WriteLine($"Pickup succeeded: {result.IsSuccess}");
```

## 5. Read state through queries

```csharp
using Moonforge.Core.Inventory.Queries;
using Moonforge.Core.Quests.Queries;

int potions = new GetInventoryItemQuantityQueryHandler()
    .Query(gameState, new GetInventoryItemQuantityQuery("item.potion"));

QuestStatus questStatus = new GetQuestStatusQueryHandler()
    .Query(gameState, new GetQuestStatusQuery("quest.tutorial"));

System.Console.WriteLine($"Potions: {potions}, Quest: {questStatus}");
// Potions: 3, Quest: Completed
```

The quest is automatically `Completed` because the `QuestObjectiveTrackingReactor` saw the
`InventoryItemChangedEvent` and advanced the objective. Reactors are why modules integrate
without referencing each other.

## 6. Claim the reward

```csharp
using Moonforge.Core.Economy.Queries;

dispatcher.Dispatch(gameState, new ClaimQuestRewardsCommand("quest.tutorial"), context);

long gold = new GetCurrencyBalanceQueryHandler()
    .Query(gameState, new GetCurrencyBalanceQuery("gold"));

System.Console.WriteLine($"Gold: {gold}");  // Gold: 50
```

## 7. Drain published events

If you want to render battle logs, animations, or UI toasts, drain the event sink between
ticks:

```csharp
foreach (DomainEvent ev in eventSink.DrainNewEvents())
{
    // Pattern-match on event types and translate to UI.
}
```

Events buffered during a command only become visible on the sink after the command
*and all its reactors* succeed. A failed command discards both state changes and events.

## What to read next

- [Architecture](architecture.md) — how the pipeline guarantees the atomic + deterministic
  properties you just relied on.
- [Cookbook](cookbook.md) — task-shaped recipes (combat, shops, dialogue trees, save/load).
- The [per-module guides](README.md) when you need depth on a specific subsystem.
