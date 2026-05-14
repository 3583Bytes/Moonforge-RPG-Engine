# Loot tables

Loot tables describe weighted, conditional, possibly-nested sets of drops. The resolver is
deterministic — every roll is driven by a caller-supplied `IRandomSource`, so the same seed
always produces the same sequence of drops.

## Entry shapes

A `LootEntryDefinition` is one of three kinds, constructed via static factories:

```csharp
LootEntryDefinition.Item("item.potion",       weight: 70, minQuantity: 1, maxQuantity: 3);
LootEntryDefinition.Currency("currency.gold", weight: 30, minQuantity: 10, maxQuantity: 25);
LootEntryDefinition.NestedTable("loot.rare",  weight: 5);
```

## Roll modes

Each `LootTableDefinition` declares one:

- **`PickOne`**: weighted random pick. Exactly one eligible entry is chosen; `Weight`
  contributes proportionally, `ChancePercent` is ignored.
- **`RollEach`**: every entry rolls independently. `ChancePercent` (0-100) determines whether
  it drops; `Weight` is ignored.

```csharp
var table = new LootTableDefinition(
    "loot.boss",
    LootRollMode.RollEach,
    [
        LootEntryDefinition.NestedTable("loot.rare_gear", chancePercent: 25),
        LootEntryDefinition.Currency("currency.gold", chancePercent: 100, minQuantity: 50, maxQuantity: 100),
        LootEntryDefinition.Item("item.potion", chancePercent: 80, minQuantity: 1, maxQuantity: 3)
    ]);
catalog.AddLootTable(table);
```

## Conditions

Any entry can declare a list of `LootConditionDefinition` gates. All must pass against the
current `GameState` for the entry to be eligible:

| Type | Reads |
|---|---|
| `WorldBoolEquals` | `gameState.WorldState[key]` as bool |
| `WorldIntAtLeast` | `gameState.WorldState[key]` as int |
| `QuestStatusEquals` | `gameState.QuestState[key].Status` |
| `ActorLevelAtLeast` | `gameState.ProgressionState[key].Level` |

```csharp
LootEntryDefinition.Item("item.legendary_blade",
    weight: 1,
    conditions: [
        new LootConditionDefinition(LootConditionType.ActorLevelAtLeast, "party.hero", intValue: 20),
        new LootConditionDefinition(LootConditionType.QuestStatusEquals,  "quest.main", questStatus: QuestStatus.Completed)
    ]);
```

## Two ways to roll

**Pure roll** (no side effects — useful for previews, custom deposit, replay tooling):

```csharp
var handler = new RollLootTableQueryHandler(catalog, randomSource);
LootRollResult result = handler.Query(gameState, new RollLootTableQuery("loot.boss"));
foreach (var drop in result.Items)      { /* ... */ }
foreach (var coin in result.Currencies) { /* ... */ }
```

**Roll and grant** (atomic deposit into wallet + bag, emits events):

```csharp
dispatcher.Register(new RollAndGrantLootCommandHandler());
DomainResult r = dispatcher.Dispatch(gameState, new RollAndGrantLootCommand("loot.boss"), context);
```

The grant routes through `EconomyTransactionCommand`, so if any deposit fails (inventory
full, unknown currency, etc.) the whole loot drop rolls back atomically and no partial state
is left behind.

Direct resolver access is also available for game code that needs more control:

```csharp
LootRollResult result = LootResolver.Roll(gameState, catalog, randomSource, tableDefinition);
```

## Events

| Event | Fired when |
|---|---|
| `LootRolledEvent` | After a `RollAndGrantLootCommand` completes (one per roll) |
| `LootItemDroppedEvent` | Per item drop |
| `LootCurrencyDroppedEvent` | Per currency drop |

`InventoryItemChangedEvent` and `CurrencyChangedEvent` are still fired by the underlying
transaction, so existing listeners keep working.

## Nested tables & cycles

Entries of kind `NestedTable` recursively roll another table and aggregate the result.
Recursion depth is capped at 8, and a cycle guard tracks visited table IDs on the current
roll stack — direct or transitive cycles terminate silently rather than infinite-looping.
