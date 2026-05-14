using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.Inventory.Commands;

namespace Moonforge.Core.Tests;

public sealed class EconomyInventoryTests
{
    [Fact]
    public void Grant_Currency_Clamps_At_Max_And_Emits_Warnings()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new GrantCurrencyCommandHandler());
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 100));

        DomainResult grantResult = dispatcher.Dispatch(
            gameState,
            new GrantCurrencyCommand("currency.gold", 120),
            CreateContext(sink, definitions));

        Assert.True(grantResult.IsSuccess);
        Assert.Equal(100, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Equal(3, sink.Events.Count);
    }

    [Fact]
    public void Inventory_Enforces_Slots_And_Stacking()
    {
        GameState gameState = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new ConfigureInventoryCapacityCommandHandler());
        dispatcher.Register(new AddInventoryItemCommandHandler());
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddItem(new ItemDefinition("item.potion.small", 10));

        Assert.True(dispatcher.Dispatch(
            gameState,
            new ConfigureInventoryCapacityCommand(2),
            CreateContext(new InMemoryDomainEventSink(), definitions)).IsSuccess);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new AddInventoryItemCommand("item.potion.small", 12),
            CreateContext(new InMemoryDomainEventSink(), definitions)).IsSuccess);

        DomainResult overflow = dispatcher.Dispatch(
            gameState,
            new AddInventoryItemCommand("item.potion.small", 9),
            CreateContext(new InMemoryDomainEventSink(), definitions));

        Assert.False(overflow.IsSuccess);
        Assert.Equal(12, gameState.InventoryBag.GetTotalQuantity("item.potion.small"));
        Assert.Equal(2, gameState.InventoryBag.UsedSlots);
    }

    [Fact]
    public void Economy_Transaction_Is_Atomic_On_Failure()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 50);
        Assert.True(gameState.InventoryBag.TryAdd("item.potion.small", 1, 10, out _));

        InMemoryDomainEventSink sink = new();
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddItem(new ItemDefinition("item.potion.small", 10));
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new EconomyTransactionCommandHandler());

        EconomyTransactionCommand command = new(
            currencyDeltas:
            [
                new CurrencyDelta("currency.gold", -20)
            ],
            inventoryDeltas:
            [
                new InventoryDelta("item.potion.small", -2)
            ]);

        DomainResult result = dispatcher.Dispatch(gameState, command, CreateContext(sink, definitions));

        Assert.False(result.IsSuccess);
        Assert.Equal(50, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity("item.potion.small"));
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void Unknown_Definition_Fails_Validation()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new AddInventoryItemCommandHandler());

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new AddInventoryItemCommand("item.unknown", 1),
            CreateContext(sink, new InMemoryGameDefinitionCatalog()));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.NotFound, result.Error!.Code);
        Assert.Empty(sink.Events);
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog definitions)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 999, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
