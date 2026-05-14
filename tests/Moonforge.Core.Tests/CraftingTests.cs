using Moonforge.Core;
using Moonforge.Core.Crafting.Commands;
using Moonforge.Core.Crafting.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class CraftingTests
{
    [Fact]
    public void Craft_Success_Consumes_Inputs_And_Grants_Output()
    {
        GameState gameState = SeedCraftInventoryAndCurrency();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new AttemptCraftCommandHandler());

        InMemoryGameDefinitionCatalog definitions = BaseDefinitions()
            .AddRecipe(new RecipeDefinition(
                id: "recipe.success",
                difficulty: 10,
                successChanceAtEqualSkill: 1.0,
                skillDeltaPerPoint: 0.0,
                minSuccessChance: 1.0,
                maxSuccessChance: 1.0,
                failConsumePolicy: CraftFailConsumePolicy.ConsumeAll,
                ingredients: new[] { new CraftIngredientDefinition("item.herb", 2) },
                currencyCosts: new[] { new CraftCurrencyCostDefinition("currency.gold", 10) },
                outputs: new[] { new CraftOutputDefinition("item.potion", 1) }));

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new AttemptCraftCommand("recipe.success", crafterSkill: 10),
            CreateContext(sink, definitions));

        Assert.True(result.IsSuccess);
        Assert.Equal(8, gameState.InventoryBag.GetTotalQuantity("item.herb"));
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity("item.potion"));
        Assert.Equal(90, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Contains(sink.Events, e => e is CraftAttemptedEvent attempted && attempted.Success);
    }

    [Fact]
    public void Craft_Failure_ConsumeAll_Consumes_Inputs_Without_Output()
    {
        GameState gameState = SeedCraftInventoryAndCurrency();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new AttemptCraftCommandHandler());

        InMemoryGameDefinitionCatalog definitions = BaseDefinitions()
            .AddRecipe(new RecipeDefinition(
                id: "recipe.fail.consumeall",
                difficulty: 10,
                successChanceAtEqualSkill: 0.0,
                skillDeltaPerPoint: 0.0,
                minSuccessChance: 0.0,
                maxSuccessChance: 0.0,
                failConsumePolicy: CraftFailConsumePolicy.ConsumeAll,
                ingredients: new[] { new CraftIngredientDefinition("item.herb", 2) },
                currencyCosts: new[] { new CraftCurrencyCostDefinition("currency.gold", 10) },
                outputs: new[] { new CraftOutputDefinition("item.potion", 1) }));

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new AttemptCraftCommand("recipe.fail.consumeall", crafterSkill: 10),
            CreateContext(sink, definitions));

        Assert.True(result.IsSuccess);
        Assert.Equal(8, gameState.InventoryBag.GetTotalQuantity("item.herb"));
        Assert.Equal(0, gameState.InventoryBag.GetTotalQuantity("item.potion"));
        Assert.Equal(90, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Contains(sink.Events, e => e is CraftAttemptedEvent attempted && !attempted.Success);
    }

    [Fact]
    public void Craft_Failure_ConsumeNone_Keeps_Inputs()
    {
        GameState gameState = SeedCraftInventoryAndCurrency();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new AttemptCraftCommandHandler());

        InMemoryGameDefinitionCatalog definitions = BaseDefinitions()
            .AddRecipe(new RecipeDefinition(
                id: "recipe.fail.consumenone",
                difficulty: 10,
                successChanceAtEqualSkill: 0.0,
                skillDeltaPerPoint: 0.0,
                minSuccessChance: 0.0,
                maxSuccessChance: 0.0,
                failConsumePolicy: CraftFailConsumePolicy.ConsumeNone,
                ingredients: new[] { new CraftIngredientDefinition("item.herb", 2) },
                currencyCosts: new[] { new CraftCurrencyCostDefinition("currency.gold", 10) },
                outputs: new[] { new CraftOutputDefinition("item.potion", 1) }));

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new AttemptCraftCommand("recipe.fail.consumenone", crafterSkill: 10),
            CreateContext(sink, definitions));

        Assert.True(result.IsSuccess);
        Assert.Equal(10, gameState.InventoryBag.GetTotalQuantity("item.herb"));
        Assert.Equal(0, gameState.InventoryBag.GetTotalQuantity("item.potion"));
        Assert.Equal(100, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Contains(sink.Events, e => e is CraftAttemptedEvent attempted && !attempted.Success);
    }

    [Fact]
    public void Craft_Unknown_Recipe_Fails()
    {
        GameState gameState = SeedCraftInventoryAndCurrency();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new AttemptCraftCommandHandler());

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new AttemptCraftCommand("recipe.unknown", crafterSkill: 10),
            CreateContext(sink, BaseDefinitions()));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.NotFound, result.Error!.Code);
        Assert.Empty(sink.Events);
    }

    private static GameState SeedCraftInventoryAndCurrency()
    {
        GameState gameState = new();
        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 100);
        gameState.InventoryBag.TryAdd("item.herb", 10, 10, out _);
        return gameState;
    }

    private static InMemoryGameDefinitionCatalog BaseDefinitions()
    {
        return new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddItem(new ItemDefinition("item.herb", 10))
            .AddItem(new ItemDefinition("item.potion", 10));
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog definitions)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 12345, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
