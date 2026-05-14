# Economy & Inventory

The economy module manages **currencies** (gold, tokens, faction reputation) and the
inventory module manages **items** (consumables, gear, key items). They're separate
modules but live together in `GameState` and share an atomic transaction primitive,
`EconomyTransactionCommand`, used for purchases, sales, rewards, and loot grants.

## Currencies

A `CurrencyDefinition` registers a currency's max balance. Balances are stored as `long`,
so a single currency can hold up to `long.MaxValue` (or the configured cap, whichever is
lower).

```csharp
using Moonforge.Core.Data.Definitions;

definitions
    .AddCurrency(new CurrencyDefinition("gold",  maxBalance: 999_999))
    .AddCurrency(new CurrencyDefinition("token", maxBalance: 999))
    .AddCurrency(new CurrencyDefinition("rep.thieves_guild", maxBalance: 100));
```

### Grant / spend

```csharp
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Economy.Queries;

dispatcher.Dispatch(gameState, new GrantCurrencyCommand("gold", 100), context);
dispatcher.Dispatch(gameState, new SpendCurrencyCommand("gold", 30),  context);

long balance = new GetCurrencyBalanceQueryHandler()
    .Query(gameState, new GetCurrencyBalanceQuery("gold"));
// balance == 70
```

`SpendCurrencyCommand` fails with `InsufficientResources` when the balance is too low.
`GrantCurrencyCommand` clamps at the configured max and fires `CurrencyOverflowClampedEvent`
if any overflow was discarded.

### Update a cap at runtime

```csharp
dispatcher.Dispatch(gameState, new ConfigureCurrencyMaxCommand("gold", 9_999_999), context);
```

## Inventory

Inventory is a bag of `InventoryStack`s. Each stack carries an `itemId`, a `quantity`, and
a `stackLimit` (the per-stack cap). The bag has a `capacitySlots` ceiling.

```csharp
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Inventory.Queries;

// One-time setup at run start.
dispatcher.Dispatch(gameState, new ConfigureInventoryCapacityCommand(20), context);

dispatcher.Dispatch(gameState, new AddInventoryItemCommand("item.potion", quantity: 3), context);
dispatcher.Dispatch(gameState, new ConsumeInventoryItemCommand("item.potion", quantity: 1), context);

int potions = new GetInventoryItemQuantityQueryHandler()
    .Query(gameState, new GetInventoryItemQuantityQuery("item.potion"));
// potions == 2
```

`AddInventoryItemCommand` reads the item's `MaxStack` from the catalog to compute how many
stacks the quantity needs. If adding exceeds `capacitySlots`, the command fails with
`InsufficientResources` and **no items are added** (atomic).

`ConsumeInventoryItemCommand` fails with `InsufficientResources` if the bag doesn't hold
the requested quantity.

## Items

```csharp
definitions
    .AddItem(new ItemDefinition("item.potion",   maxStack: 10))
    .AddItem(new ItemDefinition("item.key.iron", maxStack: 1))
    .AddItem(new ItemDefinition("item.sword.bronze", maxStack: 1));
```

Items used in shops also declare prices — see [shops.md](shops.md).

## Atomic transactions

`EconomyTransactionCommand` bundles currency *and* inventory deltas into a single all-or-
nothing operation. Other modules use it under the hood:

- **`BuyFromShopCommand`** spends currency and adds inventory atomically.
- **`SellToShopCommand`** removes inventory and grants currency atomically.
- **`ClaimQuestRewardsCommand`** uses it for reward payouts.
- **`RollAndGrantLootCommand`** rolls a loot table and runs all drops as one transaction.

Use it directly when you need a custom transaction:

```csharp
DomainResult result = dispatcher.Dispatch(gameState, new EconomyTransactionCommand(
    currencyDeltas:
    [
        new CurrencyDelta("gold",  -50),    // spend 50 gold
        new CurrencyDelta("token", +1)      // earn 1 token
    ],
    inventoryDeltas:
    [
        new InventoryDelta("item.scroll", -2),    // consume 2 scrolls
        new InventoryDelta("item.relic",  +1)     // gain 1 relic
    ]), context);

if (!result.IsSuccess)
{
    // Could be insufficient gold, insufficient scrolls, or no room for the relic —
    // whatever the cause, nothing changed.
}
```

This is the canonical place to enforce "everything or nothing" semantics across modules.

## Events

| Event | When |
|---|---|
| `CurrencyChangedEvent` | After every successful grant/spend (per currency) |
| `CurrencyOverflowClampedEvent` | When grant hit the cap |
| `InventoryItemChangedEvent` | After every successful add/consume (per item) |

`InventoryItemChangedEvent` is what `QuestObjectiveTrackingReactor` watches to auto-advance
`Collect` quest objectives — see [quests.md](quests.md).

## Patterns

### Multi-currency purchase

`PriceOptionDefinition` (in [shops.md](shops.md)) supports paying in different combinations.
For a custom purchase outside the shop system:

```csharp
DomainResult result = dispatcher.Dispatch(gameState, new EconomyTransactionCommand(
    currencyDeltas:
    [
        new CurrencyDelta("gold",  -100),
        new CurrencyDelta("token", -1)
    ],
    inventoryDeltas: [new InventoryDelta("item.artifact", +1)]),
    context);
```

If either currency is short, the whole purchase fails.

### Inventory cap based on meta-unlocks

```csharp
int baseSlots = 16;
int extraSlots = HasMetaUnlock(MetaUnlockId.DeepPockets) ? 4 : 0;
dispatcher.Dispatch(gameState, new ConfigureInventoryCapacityCommand(baseSlots + extraSlots), context);
```

`ConfigureInventoryCapacityCommand` is idempotent — call it whenever the cap should change.

## See also

- [Shops](shops.md) — the canonical buyer/seller built on top of `EconomyTransactionCommand`
- [Loot](loot.md) — bulk grants via `RollAndGrantLootCommand`
- [Quests](quests.md) — reward payouts use `EconomyTransactionCommand`
