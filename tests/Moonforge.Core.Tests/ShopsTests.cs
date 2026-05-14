using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.Shops.Commands;
using Moonforge.Core.Shops.Events;

namespace Moonforge.Core.Tests;

public sealed class ShopsTests
{
    [Fact]
    public void Buy_Uses_Selected_Alternative_Price_Option_And_Updates_Stock()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.ConfigureMax("currency.token", 10);
        gameState.CurrencyWallet.Grant("currency.gold", 50);
        gameState.CurrencyWallet.Grant("currency.token", 2);
        gameState.InventoryBag.SetCapacity(10);

        InMemoryDomainEventSink sink = new();
        SimulationClock clock = new(0);
        CommandContext context = CreateContext(sink, clock, CreateDefinitions());

        CommandDispatcher dispatcher = CreateDispatcher();
        DomainResult result = dispatcher.Dispatch(
            gameState,
            new BuyFromShopCommand("shop.town.general", "item.potion.medium", quantity: 1, priceOptionIndex: 1),
            context);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Equal(1, gameState.CurrencyWallet.GetBalance("currency.token"));
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity("item.potion.medium"));
        Assert.Equal(1, gameState.ShopState.TryGetStock("shop.town.general", "item.potion.medium"));
        Assert.Contains(sink.Events, e => e is ShopTransactionEvent tx && tx.TransactionType == ShopTransactionType.Buy);
    }

    [Fact]
    public void Buy_Fails_Atomically_When_Funds_Are_Insufficient()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 5);
        gameState.InventoryBag.SetCapacity(10);

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        DomainResult result = dispatcher.Dispatch(
            gameState,
            new BuyFromShopCommand("shop.town.general", "item.potion.medium", quantity: 1, priceOptionIndex: 0),
            CreateContext(sink, new SimulationClock(0), CreateDefinitions()));

        Assert.False(result.IsSuccess);
        Assert.Equal(5, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Equal(0, gameState.InventoryBag.GetTotalQuantity("item.potion.medium"));
        Assert.Null(gameState.ShopState.TryGetStock("shop.town.general", "item.potion.medium"));
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Shop_Restocks_After_Interval()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 100);
        gameState.InventoryBag.SetCapacity(10);

        InMemoryDomainEventSink sink = new();
        SimulationClock clock = new(0);
        CommandContext context = CreateContext(sink, clock, CreateDefinitions());
        CommandDispatcher dispatcher = CreateDispatcher();

        Assert.True(dispatcher.Dispatch(
            gameState,
            new BuyFromShopCommand("shop.town.general", "item.potion.medium", quantity: 2, priceOptionIndex: 0),
            context).IsSuccess);
        Assert.Equal(0, gameState.ShopState.TryGetStock("shop.town.general", "item.potion.medium"));

        clock.AdvanceMinutes(60);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new BuyFromShopCommand("shop.town.general", "item.potion.medium", quantity: 1, priceOptionIndex: 0),
            context).IsSuccess);
        Assert.Equal(1, gameState.ShopState.TryGetStock("shop.town.general", "item.potion.medium"));
        Assert.Contains(sink.Events, e => e is ShopRestockedEvent);
    }

    [Fact]
    public void Sell_Consumes_Item_And_Grants_Currency()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 0 + 20);
        gameState.InventoryBag.SetCapacity(10);
        Assert.True(gameState.InventoryBag.TryAdd("item.potion.medium", 2, 10, out _));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        DomainResult result = dispatcher.Dispatch(
            gameState,
            new SellToShopCommand("shop.town.general", "item.potion.medium", quantity: 1),
            CreateContext(sink, new SimulationClock(0), CreateDefinitions()));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity("item.potion.medium"));
        Assert.Equal(27, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Contains(sink.Events, e => e is ShopTransactionEvent tx && tx.TransactionType == ShopTransactionType.Sell);
    }

    private static InMemoryGameDefinitionCatalog CreateDefinitions()
    {
        return new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddCurrency(new CurrencyDefinition("currency.token", 10))
            .AddItem(new ItemDefinition(
                "item.potion.medium",
                stackLimit: 10,
                buyPriceOptions: new[]
                {
                    new PriceOptionDefinition(new[] { new PriceComponentDefinition("currency.gold", 15) }),
                    new PriceOptionDefinition(new[] { new PriceComponentDefinition("currency.token", 1) })
                },
                sellPrice: new[]
                {
                    new PriceComponentDefinition("currency.gold", 7)
                }))
            .AddShop(new ShopDefinition(
                id: "shop.town.general",
                entries: new[]
                {
                    new ShopEntryDefinition("item.potion.medium", maxStock: 2)
                },
                restockIntervalMinutes: 60));
    }

    private static CommandDispatcher CreateDispatcher()
    {
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new BuyFromShopCommandHandler());
        dispatcher.Register(new SellToShopCommandHandler());
        return dispatcher;
    }

    private static CommandContext CreateContext(
        InMemoryDomainEventSink sink,
        SimulationClock clock,
        IGameDefinitionCatalog definitions)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 444, sequence: 54),
            clock,
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
