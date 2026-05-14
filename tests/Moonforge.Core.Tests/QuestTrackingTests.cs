using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Inventory.Commands;
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

public sealed class QuestTrackingTests
{
    [Fact]
    public void Kill_Objective_Tracks_And_Completes_Quest()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcherWithQuestReactor();
        InMemoryGameDefinitionCatalog definitions = CreateDefinitions()
            .AddQuest(new QuestDefinition(
                "quest.hunt.wolf",
                new[]
                {
                    new QuestObjectiveDefinition("obj.kill.wolf", QuestObjectiveType.Kill, "enemy.wolf", requiredCount: 2)
                },
                rootObjectiveIds: new[] { "obj.kill.wolf" }));

        Assert.True(dispatcher.Dispatch(gameState, new StartQuestCommand("quest.hunt.wolf"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new EmitQuestSignalCommand(QuestSignalType.Kill, "enemy.wolf"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new EmitQuestSignalCommand(QuestSignalType.Kill, "enemy.wolf"), CreateContext(sink, definitions)).IsSuccess);

        Assert.True(gameState.QuestState.TryGet("quest.hunt.wolf", out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Completed, quest.Status);
        Assert.Equal(2, quest.GetObjectiveProgress("obj.kill.wolf"));
        Assert.Contains(sink.Events, e => e is QuestCompletedEvent completed && completed.QuestId == "quest.hunt.wolf");
    }

    [Fact]
    public void Collect_Objective_AutoTracks_From_Inventory_Event()
    {
        GameState gameState = new();
        gameState.InventoryBag.SetCapacity(10);
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcherWithQuestReactor();
        dispatcher.Register(new AddInventoryItemCommandHandler());

        InMemoryGameDefinitionCatalog definitions = CreateDefinitions()
            .AddItem(new ItemDefinition("item.herb", 10))
            .AddQuest(new QuestDefinition(
                "quest.collect.herb",
                new[]
                {
                    new QuestObjectiveDefinition("obj.collect.herb", QuestObjectiveType.Collect, "item.herb", requiredCount: 3)
                },
                rootObjectiveIds: new[] { "obj.collect.herb" }));

        Assert.True(dispatcher.Dispatch(gameState, new StartQuestCommand("quest.collect.herb"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddInventoryItemCommand("item.herb", 3), CreateContext(sink, definitions)).IsSuccess);

        Assert.True(gameState.QuestState.TryGet("quest.collect.herb", out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Completed, quest.Status);
        Assert.Equal(3, quest.GetObjectiveProgress("obj.collect.herb"));
    }

    [Fact]
    public void Composite_Or_Completes_When_Any_Child_Completes()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcherWithQuestReactor();
        InMemoryGameDefinitionCatalog definitions = CreateDefinitions()
            .AddQuest(new QuestDefinition(
                "quest.town.routes",
                new[]
                {
                    new QuestObjectiveDefinition("obj.talk.guard", QuestObjectiveType.Talk, "npc.guard"),
                    new QuestObjectiveDefinition("obj.visit.ruins", QuestObjectiveType.Visit, "location.ruins"),
                    new QuestObjectiveDefinition(
                        "obj.root.or",
                        QuestObjectiveType.CompositeOr,
                        childObjectiveIds: new[] { "obj.talk.guard", "obj.visit.ruins" })
                },
                rootObjectiveIds: new[] { "obj.root.or" }));

        Assert.True(dispatcher.Dispatch(gameState, new StartQuestCommand("quest.town.routes"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new EmitQuestSignalCommand(QuestSignalType.Talk, "npc.guard"), CreateContext(sink, definitions)).IsSuccess);

        Assert.True(gameState.QuestState.TryGet("quest.town.routes", out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Completed, quest.Status);
        Assert.Equal(1, quest.GetObjectiveProgress("obj.talk.guard"));
        Assert.Equal(0, quest.GetObjectiveProgress("obj.visit.ruins"));
    }

    [Fact]
    public void Abandoned_Quest_Does_Not_Track_Further_Progress()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcherWithQuestReactor();
        InMemoryGameDefinitionCatalog definitions = CreateDefinitions()
            .AddQuest(new QuestDefinition(
                "quest.abandon.test",
                new[]
                {
                    new QuestObjectiveDefinition("obj.visit.square", QuestObjectiveType.Visit, "location.square")
                },
                rootObjectiveIds: new[] { "obj.visit.square" }));

        Assert.True(dispatcher.Dispatch(gameState, new StartQuestCommand("quest.abandon.test"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AbandonQuestCommand("quest.abandon.test"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new EmitQuestSignalCommand(QuestSignalType.Visit, "location.square"), CreateContext(sink, definitions)).IsSuccess);

        Assert.True(gameState.QuestState.TryGet("quest.abandon.test", out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Abandoned, quest.Status);
        Assert.Equal(0, quest.GetObjectiveProgress("obj.visit.square"));
    }

    private static CommandDispatcher CreateDispatcherWithQuestReactor()
    {
        CommandDispatcher dispatcher = new();
        dispatcher.RegisterReactor(new QuestObjectiveTrackingReactor());
        dispatcher.Register(new StartQuestCommandHandler());
        dispatcher.Register(new AbandonQuestCommandHandler());
        dispatcher.Register(new EmitQuestSignalCommandHandler());
        return dispatcher;
    }

    private static InMemoryGameDefinitionCatalog CreateDefinitions()
    {
        return new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999));
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog definitions)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 2026, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
