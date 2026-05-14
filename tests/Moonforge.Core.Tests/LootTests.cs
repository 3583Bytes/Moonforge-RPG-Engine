using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Loot;
using Moonforge.Core.Loot.Commands;
using Moonforge.Core.Loot.Events;
using Moonforge.Core.Loot.Queries;
using Moonforge.Core.Progression;
using Moonforge.Core.Quests;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.World;

namespace Moonforge.Core.Tests;

public sealed class LootTests
{
    private const string PotionId = "item.potion";
    private const string HerbId = "item.herb";
    private const string SwordId = "item.sword";
    private const string GoldId = "currency.gold";

    [Fact]
    public void PickOne_Selects_Exactly_One_Entry_With_Quantity_In_Range()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.simple",
            LootRollMode.PickOne,
            [
                LootEntryDefinition.Item(PotionId, weight: 1, minQuantity: 2, maxQuantity: 4)
            ]));

        LootRollResult result = LootResolver.Roll(new GameState(), defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.simple"));

        Assert.Single(result.Items);
        Assert.Empty(result.Currencies);
        Assert.Equal(PotionId, result.Items[0].ItemId);
        Assert.InRange(result.Items[0].Quantity, 2, 4);
    }

    [Fact]
    public void PickOne_Weighted_Distribution_Matches_Weights_Within_Tolerance()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.weighted",
            LootRollMode.PickOne,
            [
                LootEntryDefinition.Item(PotionId, weight: 70),
                LootEntryDefinition.Item(HerbId, weight: 30)
            ]));
        LootTableDefinition table = defs.GetLootTable("loot.weighted");

        Pcg32RandomSource rng = new(42, 1);
        int potionCount = 0;
        int herbCount = 0;
        for (int i = 0; i < 10_000; i++)
        {
            LootRollResult r = LootResolver.Roll(new GameState(), defs, rng, table);
            if (r.Items[0].ItemId == PotionId) potionCount++;
            else herbCount++;
        }

        // Expect ~7000 potions, ~3000 herbs. Tolerance 3% of trials.
        Assert.InRange(potionCount, 6700, 7300);
        Assert.InRange(herbCount, 2700, 3300);
    }

    [Fact]
    public void Same_Seed_Produces_Identical_Results()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.det",
            LootRollMode.PickOne,
            [
                LootEntryDefinition.Item(PotionId, weight: 1, minQuantity: 1, maxQuantity: 9),
                LootEntryDefinition.Item(HerbId, weight: 1, minQuantity: 1, maxQuantity: 9),
                LootEntryDefinition.Item(SwordId, weight: 1)
            ]));
        LootTableDefinition table = defs.GetLootTable("loot.det");

        List<string> a = RollSequence(defs, table, seed: 7777, count: 20);
        List<string> b = RollSequence(defs, table, seed: 7777, count: 20);

        Assert.Equal(a, b);
    }

    [Fact]
    public void RollEach_Rolls_Every_Entry_Independently()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.each",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(PotionId, chancePercent: 100),
                LootEntryDefinition.Item(HerbId, chancePercent: 100),
                LootEntryDefinition.Currency(GoldId, chancePercent: 100, minQuantity: 5, maxQuantity: 5)
            ]));

        LootRollResult result = LootResolver.Roll(new GameState(), defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.each"));

        Assert.Equal(2, result.Items.Count);
        Assert.Single(result.Currencies);
        Assert.Equal(5L, result.Currencies[0].Amount);
    }

    [Fact]
    public void RollEach_Zero_Chance_Entries_Are_Skipped()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.zero",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(PotionId, chancePercent: 0),
                LootEntryDefinition.Item(HerbId, chancePercent: 100)
            ]));

        LootRollResult result = LootResolver.Roll(new GameState(), defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.zero"));

        Assert.Single(result.Items);
        Assert.Equal(HerbId, result.Items[0].ItemId);
    }

    [Fact]
    public void Nested_Table_Drops_Aggregate_Into_Parent_Result()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.consumables",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(PotionId, chancePercent: 100),
                LootEntryDefinition.Item(HerbId, chancePercent: 100)
            ]));
        defs.AddLootTable(new LootTableDefinition(
            "loot.boss",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.NestedTable("loot.consumables", chancePercent: 100),
                LootEntryDefinition.Currency(GoldId, chancePercent: 100, minQuantity: 100, maxQuantity: 100)
            ]));

        LootRollResult result = LootResolver.Roll(new GameState(), defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.boss"));

        Assert.Equal(2, result.Items.Count);
        Assert.Single(result.Currencies);
        Assert.Equal(100L, result.Currencies[0].Amount);
    }

    [Fact]
    public void Cycle_Between_Tables_Does_Not_Hang_Or_Throw()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.a",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.NestedTable("loot.b", chancePercent: 100),
                LootEntryDefinition.Item(PotionId, chancePercent: 100)
            ]));
        defs.AddLootTable(new LootTableDefinition(
            "loot.b",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.NestedTable("loot.a", chancePercent: 100),
                LootEntryDefinition.Item(HerbId, chancePercent: 100)
            ]));

        LootRollResult result = LootResolver.Roll(new GameState(), defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.a"));

        // Should terminate, having visited each table once and yielded their leaf items.
        Assert.Contains(result.Items, d => d.ItemId == PotionId);
        Assert.Contains(result.Items, d => d.ItemId == HerbId);
    }

    [Fact]
    public void Condition_World_Bool_Filters_Entry()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.flag",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(SwordId, chancePercent: 100, conditions:
                [
                    new LootConditionDefinition(LootConditionType.WorldBoolEquals, "flag.unlocked", boolValue: true)
                ]),
                LootEntryDefinition.Item(PotionId, chancePercent: 100)
            ]));

        GameState locked = new();
        LootRollResult lockedResult = LootResolver.Roll(locked, defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.flag"));
        Assert.Single(lockedResult.Items);
        Assert.Equal(PotionId, lockedResult.Items[0].ItemId);

        GameState unlocked = new();
        unlocked.WorldState.Set("flag.unlocked", WorldVariableValue.FromBool(true));
        LootRollResult unlockedResult = LootResolver.Roll(unlocked, defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.flag"));
        Assert.Equal(2, unlockedResult.Items.Count);
        Assert.Contains(unlockedResult.Items, d => d.ItemId == SwordId);
    }

    [Fact]
    public void Condition_Actor_Level_Filters_Entry()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.tiered",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(SwordId, chancePercent: 100, conditions:
                [
                    new LootConditionDefinition(LootConditionType.ActorLevelAtLeast, "hero", intValue: 5)
                ])
            ]));

        GameState low = new();
        low.ProgressionState.GetOrCreate("hero", curveId: "curve.default", level: 3);
        Assert.Empty(LootResolver.Roll(low, defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.tiered")).Items);

        GameState high = new();
        high.ProgressionState.GetOrCreate("hero", curveId: "curve.default", level: 7);
        Assert.Single(LootResolver.Roll(high, defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.tiered")).Items);
    }

    [Fact]
    public void Condition_Quest_Status_Filters_Entry()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.quest",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(SwordId, chancePercent: 100, conditions:
                [
                    new LootConditionDefinition(LootConditionType.QuestStatusEquals, "quest.main", questStatus: QuestStatus.Completed)
                ])
            ]));

        GameState active = new();
        active.QuestState.GetOrCreate("quest.main").Status = QuestStatus.Active;
        Assert.Empty(LootResolver.Roll(active, defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.quest")).Items);

        GameState done = new();
        done.QuestState.GetOrCreate("quest.main").Status = QuestStatus.Completed;
        Assert.Single(LootResolver.Roll(done, defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.quest")).Items);
    }

    [Fact]
    public void RollAndGrant_Deposits_Items_And_Currency_And_Fires_Events()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddCurrency(new CurrencyDefinition(GoldId, 9_999_999));
        defs.AddLootTable(new LootTableDefinition(
            "loot.chest",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(PotionId, chancePercent: 100, minQuantity: 2, maxQuantity: 2),
                LootEntryDefinition.Currency(GoldId, chancePercent: 100, minQuantity: 25, maxQuantity: 25)
            ]));

        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new RollAndGrantLootCommandHandler());

        DomainResult result = dispatcher.Dispatch(gameState, new RollAndGrantLootCommand("loot.chest"), CreateContext(sink, defs));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, gameState.InventoryBag.GetTotalQuantity(PotionId));
        Assert.Equal(25L, gameState.CurrencyWallet.GetBalance(GoldId));
        Assert.Contains(sink.Events, e => e is LootItemDroppedEvent dropped && dropped.ItemId == PotionId && dropped.Quantity == 2);
        Assert.Contains(sink.Events, e => e is LootCurrencyDroppedEvent dropped && dropped.CurrencyId == GoldId && dropped.Amount == 25);
        Assert.Contains(sink.Events, e => e is LootRolledEvent rolled && rolled.TableId == "loot.chest" && rolled.ItemDropCount == 1 && rolled.CurrencyDropCount == 1);
    }

    [Fact]
    public void RollAndGrant_Rolls_Back_When_Inventory_Cannot_Accept()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddCurrency(new CurrencyDefinition(GoldId, 9_999_999));
        defs.AddLootTable(new LootTableDefinition(
            "loot.overflow",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Currency(GoldId, chancePercent: 100, minQuantity: 10, maxQuantity: 10),
                LootEntryDefinition.Item(PotionId, chancePercent: 100, minQuantity: 5, maxQuantity: 5)
            ]));

        // One slot, already occupied by a different item type → adding potion must fail.
        GameState gameState = new();
        gameState.InventoryBag.SetCapacity(1);
        Assert.True(gameState.InventoryBag.TryAdd(HerbId, 1, stackLimit: 99, out _));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new RollAndGrantLootCommandHandler());

        DomainResult result = dispatcher.Dispatch(gameState, new RollAndGrantLootCommand("loot.overflow"), CreateContext(sink, defs));

        Assert.False(result.IsSuccess);
        Assert.Equal(0L, gameState.CurrencyWallet.GetBalance(GoldId));
        Assert.Equal(0, gameState.InventoryBag.GetTotalQuantity(PotionId));
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity(HerbId));
        Assert.DoesNotContain(sink.Events, e => e is LootItemDroppedEvent);
        Assert.DoesNotContain(sink.Events, e => e is LootCurrencyDroppedEvent);
    }

    [Fact]
    public void RollLootTable_Query_Has_No_Side_Effects()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition(
            "loot.preview",
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(PotionId, chancePercent: 100, minQuantity: 3, maxQuantity: 3)
            ]));

        GameState gameState = new();
        RollLootTableQueryHandler handler = new(defs, new Pcg32RandomSource(1, 1));

        LootRollResult preview = handler.Query(gameState, new RollLootTableQuery("loot.preview"));

        Assert.Single(preview.Items);
        Assert.Equal(3, preview.Items[0].Quantity);
        Assert.Equal(0, gameState.InventoryBag.GetTotalQuantity(PotionId));
    }

    [Fact]
    public void Empty_Or_Unknown_Table_Returns_Empty_Result()
    {
        InMemoryGameDefinitionCatalog defs = BuildCatalog();
        defs.AddLootTable(new LootTableDefinition("loot.nothing", LootRollMode.PickOne, []));

        LootRollResult empty = LootResolver.Roll(new GameState(), defs, new Pcg32RandomSource(1, 1), defs.GetLootTable("loot.nothing"));
        Assert.True(empty.IsEmpty);

        RollLootTableQueryHandler handler = new(defs, new Pcg32RandomSource(1, 1));
        LootRollResult unknown = handler.Query(new GameState(), new RollLootTableQuery("loot.does_not_exist"));
        Assert.True(unknown.IsEmpty);
    }

    private static List<string> RollSequence(InMemoryGameDefinitionCatalog defs, LootTableDefinition table, ulong seed, int count)
    {
        Pcg32RandomSource rng = new(seed, 1);
        List<string> ids = new();
        for (int i = 0; i < count; i++)
        {
            LootRollResult r = LootResolver.Roll(new GameState(), defs, rng, table);
            foreach (LootDrop d in r.Items) ids.Add($"{d.ItemId}:{d.Quantity}");
        }

        return ids;
    }

    private static InMemoryGameDefinitionCatalog BuildCatalog()
    {
        return new InMemoryGameDefinitionCatalog()
            .AddItem(new ItemDefinition(PotionId, stackLimit: 99))
            .AddItem(new ItemDefinition(HerbId, stackLimit: 99))
            .AddItem(new ItemDefinition(SwordId, stackLimit: 1));
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 42, sequence: 1),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }
}

internal static class LootTableTestExtensions
{
    public static LootTableDefinition GetLootTable(this InMemoryGameDefinitionCatalog catalog, string id)
    {
        catalog.TryGetLootTable(id, out LootTableDefinition def);
        return def;
    }
}
