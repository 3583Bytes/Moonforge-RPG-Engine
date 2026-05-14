# Shops

Shops sell items at designer-authored prices, may stock a finite quantity, and can restock
on a clock-driven cadence. Buys and sales are atomic — failures (insufficient gold, full
inventory, out of stock) roll back cleanly.

## Defining a shop

A `ShopDefinition` lists which items it carries and a restock interval. Per-item prices
are declared on the `ItemDefinition` itself, not the shop — the same item can be sold by
multiple shops at the same price (a sensible default; override per-shop if you need it
by registering different item ids).

```csharp
using Moonforge.Core.Data.Definitions;

definitions
    .AddItem(new ItemDefinition(
        "item.potion",
        maxStack: 10,
        buyPriceOptions:
        [
            // Single-currency option:
            new PriceOptionDefinition([new PriceComponentDefinition("gold", 18)]),

            // Alternative payment: 1 token instead of 18 gold.
            new PriceOptionDefinition([new PriceComponentDefinition("token", 1)])
        ],
        sellPrice: [new PriceComponentDefinition("gold", 8)]))
    .AddShop(new ShopDefinition(
        id: "shop.town.general",
        entries:
        [
            new ShopEntryDefinition(itemId: "item.potion", maxStock: 4),
            new ShopEntryDefinition(itemId: "item.herb")     // unlimited stock
        ],
        restockIntervalMinutes: 30));
```

A `null` `maxStock` means infinite supply. A finite `maxStock` is restocked to that value
every `restockIntervalMinutes` simulation minutes (see "Restock" below).

## Buying

```csharp
using Moonforge.Core.Shops.Commands;

DomainResult buy = dispatcher.Dispatch(gameState, new BuyFromShopCommand(
    shopId: "shop.town.general",
    itemId: "item.potion",
    quantity: 2,
    priceOptionIndex: 0,        // pay in gold (the first PriceOptionDefinition)
    actorId: "party.hero"),
    context);

if (!buy.IsSuccess)
{
    // buy.Error.Code can be:
    //   InsufficientResources — not enough gold
    //   NotFound              — shop or item doesn't exist / item not stocked
    //   ValidationFailed      — quantity ≤ 0, etc.
    //   Conflict              — out of stock
}
```

The handler runs the entire purchase through `EconomyTransactionCommand`:

1. Compute total cost = `priceOption × quantity` (across all currency components).
2. Decrement shop stock.
3. Spend currencies.
4. Add inventory.

If any step fails, the dispatcher's snapshot restores the world — no partial purchase
state is ever observable.

## Selling

```csharp
DomainResult sell = dispatcher.Dispatch(gameState, new SellToShopCommand(
    shopId: "shop.town.general",
    itemId: "item.herb",
    quantity: 5), context);
```

Selling reverses the flow: removes from inventory, grants currency, and increments shop
stock (capped at the entry's `maxStock`). Items without a `sellPrice` fail with
`ValidationFailed`.

## Reading shop state

```csharp
using Moonforge.Core.Shops.Queries;

int? stock = new GetShopStockQueryHandler()
    .Query(gameState, new GetShopStockQuery("shop.town.general", "item.potion"));
// null  ⇒ unlimited
// 0     ⇒ sold out
// 4     ⇒ four left
```

Direct read also works:

```csharp
foreach ((string key, int qty) in gameState.ShopState.EntryStock)
{
    // key is "<shopId>|<itemId>"
}
```

## Restock

Shops restock based on the `CommandContext.Clock`. The relevant field is
`GameState.SimulationMinutes` (a `long`), and each shop tracks
`LastRestockMinute` per `shopId`. Restock fires lazily when a `BuyFromShopCommand` or
`SellToShopCommand` checks whether `(currentMinute − lastRestock) >= restockInterval`.

**For restocks to fire, the simulation clock must advance**. The engine doesn't advance
time on its own — your host code does it. A typical pattern:

```csharp
// Whenever a meaningful chunk of in-game time passes (entering a new dungeon floor,
// resting at an inn, etc.):
gameState.SimulationMinutes += 30;
```

Once `SimulationMinutes` ticks past `lastRestockMinute + restockIntervalMinutes`, the
next buy/sell on that shop refills stock to each entry's `maxStock` and fires a
`ShopRestockedEvent`.

If your design has no notion of time (e.g. a roguelike), set `restockIntervalMinutes` to
0 and the shop is effectively always full.

## Multi-currency prices

A `PriceOptionDefinition` is a *list* of `PriceComponentDefinition`s — pay every
component at once. Use this for hybrid currencies:

```csharp
buyPriceOptions:
[
    // Option 0: 50 gold + 1 token (everything's paid)
    new PriceOptionDefinition(
    [
        new PriceComponentDefinition("gold",  50),
        new PriceComponentDefinition("token", 1)
    ]),

    // Option 1: 200 gold alone
    new PriceOptionDefinition([new PriceComponentDefinition("gold", 200)])
];
```

The `priceOptionIndex` parameter on `BuyFromShopCommand` picks which option to use. If a
chosen option's currency component is short, the whole buy fails — the engine doesn't
auto-fall-back to the next option.

## Events

| Event | When |
|---|---|
| `ShopTransactionEvent` | After successful buy or sell (carries `ShopTransactionType`) |
| `ShopRestockedEvent` | When stock auto-refills on time advance |
| `CurrencyChangedEvent`, `InventoryItemChangedEvent` | From the underlying transaction |

## Patterns

### "Reputation discount"

Conditional pricing isn't a first-class engine feature. The clean pattern is to declare
multiple `PriceOptionDefinition`s and let the UI pick which one to surface based on world
state:

```csharp
buyPriceOptions:
[
    new PriceOptionDefinition([new PriceComponentDefinition("gold", 100)]),   // regular
    new PriceOptionDefinition([new PriceComponentDefinition("gold", 70)])     // discount
];
```

```csharp
int priceOptionIndex = gameState.WorldState.GetInt("rep.thieves_guild") >= 50 ? 1 : 0;
dispatcher.Dispatch(gameState, new BuyFromShopCommand(shopId, itemId, qty, priceOptionIndex), ctx);
```

### Shop unlocks via quest state

Same idea — declare two shop entries with different items, and let the UI filter by
`GetQuestStatusQuery`. The engine doesn't gate `BuyFromShopCommand` based on state.

## See also

- [Economy](economy.md) — the `EconomyTransactionCommand` that powers shop transactions
- [World](world.md) — for game time advancement and storing reputation flags
