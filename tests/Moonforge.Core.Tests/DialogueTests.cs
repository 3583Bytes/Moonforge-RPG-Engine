using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Dialogue;
using Moonforge.Core.Dialogue.Commands;
using Moonforge.Core.Dialogue.Events;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.World;

namespace Moonforge.Core.Tests;

public sealed class DialogueTests
{
    [Fact]
    public void Dialogue_Choice_Conditions_And_World_SideEffects_Work()
    {
        GameState gameState = new();
        gameState.WorldState.Set("player.reputation", WorldVariableValue.FromInt(5));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryGameDefinitionCatalog definitions = CreateDefinitions()
            .AddDialogue(new DialogueDefinition(
                id: "dialogue.guard.checkpoint",
                startNodeId: "node.start",
                nodes: new[]
                {
                    new DialogueNodeDefinition(
                        id: "node.start",
                        textKey: "dlg.guard.start",
                        choices: new[]
                        {
                            new DialogueChoiceDefinition(
                                id: "choice.enter",
                                textKey: "dlg.guard.enter",
                                nextNodeId: "node.granted",
                                conditions: new[]
                                {
                                    new DialogueConditionDefinition(DialogueConditionType.WorldIntAtLeast, "player.reputation", intValue: 5)
                                }),
                            new DialogueChoiceDefinition(
                                id: "choice.leave",
                                textKey: "dlg.guard.leave",
                                nextNodeId: "node.end")
                        }),
                    new DialogueNodeDefinition(
                        id: "node.granted",
                        textKey: "dlg.guard.granted",
                        onEnterEffects: new[]
                        {
                            new DialogueEffectDefinition(DialogueEffectType.SetWorldBool, "gate.open", boolValue: true)
                        }),
                    new DialogueNodeDefinition(
                        id: "node.end",
                        textKey: "dlg.guard.end")
                }));

        Assert.True(dispatcher.Dispatch(gameState, new StartDialogueCommand("dialogue.guard.checkpoint"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ChooseDialogueChoiceCommand("dialogue.guard.checkpoint", "choice.enter"), CreateContext(sink, definitions)).IsSuccess);

        Assert.True(gameState.WorldState.TryGet("gate.open", out WorldVariableValue gateOpen));
        Assert.True(gateOpen.TryGetBool(out bool value));
        Assert.True(value);
        Assert.True(gameState.DialogueState.TryGet("dialogue.guard.checkpoint", out DialogueInstanceState instance));
        Assert.True(instance.Completed);
        Assert.Contains("node.granted", instance.VisitedNodes);
    }

    [Fact]
    public void Dialogue_Talk_Effect_Advances_Quest_Talk_Objective()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryGameDefinitionCatalog definitions = CreateDefinitions()
            .AddQuest(new QuestDefinition(
                id: "quest.talk.alchemist",
                objectives: new[]
                {
                    new QuestObjectiveDefinition("obj.talk", QuestObjectiveType.Talk, "npc.alchemist")
                },
                rootObjectiveIds: new[] { "obj.talk" }))
            .AddDialogue(new DialogueDefinition(
                id: "dialogue.alchemist",
                startNodeId: "node.start",
                nodes: new[]
                {
                    new DialogueNodeDefinition(
                        id: "node.start",
                        textKey: "dlg.alchemist.start",
                        choices: new[]
                        {
                            new DialogueChoiceDefinition(
                                id: "choice.greet",
                                textKey: "dlg.alchemist.greet",
                                nextNodeId: "node.done",
                                effects: new[]
                                {
                                    new DialogueEffectDefinition(DialogueEffectType.EmitTalkSignal, "npc.alchemist")
                                })
                        }),
                    new DialogueNodeDefinition(id: "node.done", textKey: "dlg.alchemist.done")
                }));

        Assert.True(dispatcher.Dispatch(gameState, new StartQuestCommand("quest.talk.alchemist"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new StartDialogueCommand("dialogue.alchemist"), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ChooseDialogueChoiceCommand("dialogue.alchemist", "choice.greet"), CreateContext(sink, definitions)).IsSuccess);

        Assert.True(gameState.QuestState.TryGet("quest.talk.alchemist", out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Completed, quest.Status);
        Assert.Equal(1, quest.GetObjectiveProgress("obj.talk"));
        Assert.Contains(sink.Events, e => e is DialogueChoiceSelectedEvent choice && choice.ChoiceId == "choice.greet");
    }

    [Fact]
    public void Dialogue_Choice_Fails_When_Conditions_Not_Met()
    {
        GameState gameState = new();
        gameState.WorldState.Set("player.reputation", WorldVariableValue.FromInt(1));
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryGameDefinitionCatalog definitions = CreateDefinitions()
            .AddDialogue(new DialogueDefinition(
                id: "dialogue.condition.test",
                startNodeId: "node.start",
                nodes: new[]
                {
                    new DialogueNodeDefinition(
                        id: "node.start",
                        textKey: "dlg.start",
                        choices: new[]
                        {
                            new DialogueChoiceDefinition(
                                id: "choice.locked",
                                textKey: "dlg.locked",
                                nextNodeId: "node.done",
                                conditions: new[]
                                {
                                    new DialogueConditionDefinition(DialogueConditionType.WorldIntAtLeast, "player.reputation", intValue: 5)
                                })
                        }),
                    new DialogueNodeDefinition(id: "node.done", textKey: "dlg.done")
                }));

        Assert.True(dispatcher.Dispatch(gameState, new StartDialogueCommand("dialogue.condition.test"), CreateContext(sink, definitions)).IsSuccess);
        DomainResult chooseResult = dispatcher.Dispatch(
            gameState,
            new ChooseDialogueChoiceCommand("dialogue.condition.test", "choice.locked"),
            CreateContext(sink, definitions));

        Assert.False(chooseResult.IsSuccess);
        Assert.Equal(DomainErrorCode.ValidationFailed, chooseResult.Error!.Code);
    }

    private static CommandDispatcher CreateDispatcher()
    {
        CommandDispatcher dispatcher = new();
        dispatcher.RegisterReactor(new QuestObjectiveTrackingReactor());
        dispatcher.Register(new StartQuestCommandHandler());
        dispatcher.Register(new EmitQuestSignalCommandHandler());
        dispatcher.Register(new StartDialogueCommandHandler());
        dispatcher.Register(new ChooseDialogueChoiceCommandHandler());
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
            new Pcg32RandomSource(seed: 77, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
