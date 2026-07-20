# Loot tables

Loot tables describe weighted, conditional, possibly-nested sets of drops. The resolver is
deterministic — every roll is driven by a caller-supplied `IRandomSource`, so the same seed
always produces the same sequence of drops.

## Entry shapes

A `LootEntryDefinition` is one of three kinds, constructed via static factories:

```csharp
// Drops the item; under PickOne, weight 70 vs the others below = 70/(70+30+5) odds. → 1–3 potions.
LootEntryDefinition.Item("item.potion",       weight: 70, minQuantity: 1, maxQuantity: 3);
// Drops a currency amount instead of an item. → 10–25 gold.
LootEntryDefinition.Currency("currency.gold", weight: 30, minQuantity: 10, maxQuantity: 25);
// Recursively rolls another table (here weight 5 = rarest of the three).
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
    LootRollMode.RollEach,                // every entry rolls independently
    [
        LootEntryDefinition.NestedTable("loot.rare_gear", chancePercent: 25),                       // 25% chance to roll the nested gear table
        LootEntryDefinition.Currency("currency.gold", chancePercent: 100, minQuantity: 50, maxQuantity: 100), // always drops 50–100 gold
        LootEntryDefinition.Item("item.potion", chancePercent: 80, minQuantity: 1, maxQuantity: 3)  // 80% chance for 1–3 potions
    ]);
catalog.AddLootTable(table);              // register so RollLootTableQuery / RollAndGrantLootCommand can find it by id
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
        // party.hero must be at least level 20 (reads ProgressionState["party.hero"].Level)
        new LootConditionDefinition(LootConditionType.ActorLevelAtLeast, "party.hero", intValue: 20),
        // quest.main must be Completed (reads QuestState["quest.main"].Status)
        new LootConditionDefinition(LootConditionType.QuestStatusEquals,  "quest.main", questStatus: QuestStatus.Completed)
    ]); // all conditions must pass or the entry is excluded from the roll
```

## Two ways to roll

**Pure roll** (no side effects — useful for previews, custom deposit, replay tooling):

```csharp
var handler = new RollLootTableQueryHandler(catalog, randomSource);
LootRollResult result = handler.Query(gameState, new RollLootTableQuery("loot.boss")); // rolls; nothing is granted
foreach (var drop in result.Items)      { /* drop.ItemId, drop.Quantity */ }
foreach (var coin in result.Currencies) { /* coin.CurrencyId, coin.Amount */ }
```

**Roll and grant** (atomic deposit into wallet + bag, emits events):

```csharp
dispatcher.Register(new RollAndGrantLootCommandHandler());
// Rolls "loot.boss" and atomically deposits drops into wallet + bag; rolls back if any deposit fails.
DomainResult r = dispatcher.Dispatch(gameState, new RollAndGrantLootCommand("loot.boss"), context);
```

The grant routes through `EconomyTransactionCommand`, so if any deposit fails (inventory
full, unknown currency, etc.) the whole loot drop rolls back atomically and no partial state
is left behind.

Direct resolver access is also available for game code that needs more control:

```csharp
// Static resolver — takes the table definition directly (no catalog lookup by id) and rolls it.
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
