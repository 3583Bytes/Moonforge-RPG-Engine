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

namespace Moonforge.Core.Tests;

/// <summary>
/// Handlers that compose another module's handler (e.g. shops embedding the economy
/// transaction) accept that handler via their constructor. These tests prove that the
/// instance supplied there is the one actually used — so a host replacing a built-in
/// handler gets consistent behavior on composed paths and directly dispatched commands.
/// </summary>
public sealed class ComposedHandlerTests
{
    [Fact]
    public void BuyFromShop_Uses_The_Injected_Economy_Handler()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 50);
        gameState.InventoryBag.SetCapacity(10);

        CountingEconomyHandler economy = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register<EconomyTransactionCommand>(economy);
        dispatcher.Register(new BuyFromShopCommandHandler(economy));

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new BuyFromShopCommand("shop.town.general", "item.potion.medium", quantity: 1, priceOptionIndex: 0),
            CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, economy.HandledCount);
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity("item.potion.medium"));
    }

    [Fact]
    public void Direct_Dispatch_And_Composed_Path_Share_The_Same_Handler_Instance()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 50);
        gameState.InventoryBag.SetCapacity(10);

        CountingEconomyHandler economy = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register<EconomyTransactionCommand>(economy);
        dispatcher.Register(new BuyFromShopCommandHandler(economy));

        CommandContext context = CreateContext();

        Assert.True(dispatcher.Dispatch(
            gameState,
            new EconomyTransactionCommand(
                new[] { new CurrencyDelta("currency.gold", -5) },
                System.Array.Empty<InventoryDelta>()),
            context).IsSuccess);
        Assert.True(dispatcher.Dispatch(
            gameState,
            new BuyFromShopCommand("shop.town.general", "item.potion.medium", quantity: 1, priceOptionIndex: 0),
            context).IsSuccess);

        Assert.Equal(2, economy.HandledCount);
    }

    [Fact]
    public void Default_Constructors_Still_Work_Without_Injection()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 50);
        gameState.InventoryBag.SetCapacity(10);

        CommandDispatcher dispatcher = new();
        dispatcher.Register(new BuyFromShopCommandHandler());

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new BuyFromShopCommand("shop.town.general", "item.potion.medium", quantity: 1, priceOptionIndex: 0),
            CreateContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(35, gameState.CurrencyWallet.GetBalance("currency.gold"));
    }

    /// <summary>
    /// Decorates the built-in economy handler, counting invocations from any path.
    /// </summary>
    private sealed class CountingEconomyHandler : ICommandHandler<EconomyTransactionCommand>
    {
        private readonly EconomyTransactionCommandHandler _inner = new();

        public int HandledCount { get; private set; }

        public DomainResult Handle(GameState gameState, EconomyTransactionCommand command, CommandContext context)
        {
            HandledCount++;
            return _inner.Handle(gameState, command, context);
        }
    }

    private static CommandContext CreateContext()
    {
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddItem(new ItemDefinition(
                "item.potion.medium",
                stackLimit: 10,
                buyPriceOptions: new[]
                {
                    new PriceOptionDefinition(new[] { new PriceComponentDefinition("currency.gold", 15) })
                },
                sellPrice: new[]
                {
                    new PriceComponentDefinition("currency.gold", 7)
                }))
            .AddShop(new ShopDefinition(
                id: "shop.town.general",
                entries: new[]
                {
                    new ShopEntryDefinition("item.potion.medium", maxStock: 5)
                },
                restockIntervalMinutes: 60));

        return new CommandContext(
            new Pcg32RandomSource(seed: 444, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            new InMemoryDomainEventSink(),
            definitions);
    }
}
