using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Quests.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class QuestRewardsTests
{
    private const string QuestId = "quest.test.bounty";
    private const string Gold = "currency.gold";
    private const string Herb = "item.herb";

    [Fact]
    public void Claim_Grants_Currency_And_Inventory_And_Marks_Rewarded()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        MarkQuestCompleted(gameState);

        DomainResult result = dispatcher.Dispatch(gameState, new ClaimQuestRewardsCommand(QuestId), CreateContext(sink, definitions));

        Assert.True(result.IsSuccess);
        Assert.Equal(50, gameState.CurrencyWallet.GetBalance(Gold));
        Assert.Equal(2, gameState.InventoryBag.GetTotalQuantity(Herb));
        Assert.True(gameState.QuestState.TryGet(QuestId, out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Rewarded, quest.Status);
        Assert.Contains(sink.Events, e => e is QuestRewardedEvent rewarded && rewarded.QuestId == QuestId);
    }

    [Fact]
    public void Claim_Fails_When_Quest_Not_Completed()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        gameState.QuestState.GetOrCreate(QuestId).Status = QuestStatus.Active;

        DomainResult result = dispatcher.Dispatch(gameState, new ClaimQuestRewardsCommand(QuestId), CreateContext(sink, definitions));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, result.Error!.Code);
        Assert.Equal(0, gameState.CurrencyWallet.GetBalance(Gold));
    }

    [Fact]
    public void Claim_Fails_When_Already_Rewarded()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        MarkQuestCompleted(gameState);
        Assert.True(dispatcher.Dispatch(gameState, new ClaimQuestRewardsCommand(QuestId), CreateContext(sink, definitions)).IsSuccess);

        DomainResult retry = dispatcher.Dispatch(gameState, new ClaimQuestRewardsCommand(QuestId), CreateContext(sink, definitions));

        Assert.False(retry.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, retry.Error!.Code);
        Assert.Equal(50, gameState.CurrencyWallet.GetBalance(Gold));
    }

    [Fact]
    public void Claim_Is_Atomic_When_Inventory_Full()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        gameState.InventoryBag.SetCapacity(1);
        Assert.True(gameState.InventoryBag.TryAdd("item.filler", 5, 5, out _));
        MarkQuestCompleted(gameState);

        DomainResult result = dispatcher.Dispatch(gameState, new ClaimQuestRewardsCommand(QuestId), CreateContext(sink, definitions));

        Assert.False(result.IsSuccess);
        Assert.Equal(0, gameState.CurrencyWallet.GetBalance(Gold));
        Assert.True(gameState.QuestState.TryGet(QuestId, out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Completed, quest.Status);
    }

    [Fact]
    public void AutoClaim_Grants_Rewards_Without_Explicit_Claim_Dispatch()
    {
        // Same shape as the bounty quest above, but with AutoClaim = true. The tracking
        // reactor should dispatch ClaimQuestRewardsCommand in the same transaction as the
        // completing inventory signal — caller never calls Claim explicitly.
        const string AutoQuestId = "quest.auto.bounty";
        GameState gameState = new();
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition(Gold, 999))
            .AddItem(new ItemDefinition(Herb, stackLimit: 20))
            .AddQuest(new QuestDefinition(
                id: AutoQuestId,
                objectives: [new QuestObjectiveDefinition("obj.collect", QuestObjectiveType.Collect, targetId: Herb, requiredCount: 2)],
                autoClaim: true,
                rewardCurrency: [new CurrencyDelta(Gold, 50)]));
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();

        Assert.True(dispatcher.Dispatch(gameState, new Moonforge.Core.Inventory.Commands.ConfigureInventoryCapacityCommand(10), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new StartQuestCommand(AutoQuestId), CreateContext(sink, definitions)).IsSuccess);

        // Adding 2 herbs to the inventory both advances the Collect objective and — via
        // AutoClaim — triggers the reward inside the same transaction.
        Assert.True(dispatcher.Dispatch(gameState, new Moonforge.Core.Inventory.Commands.AddInventoryItemCommand(Herb, 2), CreateContext(sink, definitions)).IsSuccess);

        Assert.Equal(50, gameState.CurrencyWallet.GetBalance(Gold));
        Assert.True(gameState.QuestState.TryGet(AutoQuestId, out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Rewarded, quest.Status);
        Assert.Contains(sink.Events, e => e is QuestRewardedEvent rewarded && rewarded.QuestId == AutoQuestId);
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) CreateWorld()
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition(Gold, 999))
            .AddItem(new ItemDefinition(Herb, stackLimit: 20))
            .AddItem(new ItemDefinition("item.filler", stackLimit: 5))
            .AddQuest(new QuestDefinition(
                id: QuestId,
                objectives: [new QuestObjectiveDefinition("obj.kill", QuestObjectiveType.Kill, targetId: "enemy.test", requiredCount: 1)],
                rewardCurrency: [new CurrencyDelta(Gold, 50)],
                rewardInventory: [new InventoryDelta(Herb, 2)]));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
        return (gameState, dispatcher, definitions, sink);
    }

    private static void MarkQuestCompleted(GameState gameState)
    {
        gameState.QuestState.GetOrCreate(QuestId).Status = QuestStatus.Completed;
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog definitions)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
