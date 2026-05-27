using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.World;
using Moonforge.Core.World.Commands;

namespace Moonforge.Core.Dialogue.Commands
{

    internal sealed class DialogueRuntime
    {
        private readonly SetWorldVariableCommandHandler _setWorldHandler = new();
        private readonly EmitQuestSignalCommandHandler _questSignalHandler = new();

        public bool TryBuildNodeMap(
            DialogueDefinition dialogueDefinition,
            out Dictionary<string, DialogueNodeDefinition> nodeMap,
            out DomainError? error)
        {
            nodeMap = new Dictionary<string, DialogueNodeDefinition>(StringComparer.Ordinal);
            foreach (DialogueNodeDefinition node in dialogueDefinition.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    error = new DomainError(DomainErrorCode.ValidationFailed, $"Dialogue '{dialogueDefinition.Id}' has a node with empty ID.");
                    return false;
                }

                if (nodeMap.ContainsKey(node.Id))
                {
                    error = new DomainError(DomainErrorCode.ValidationFailed, $"Dialogue '{dialogueDefinition.Id}' has duplicate node ID '{node.Id}'.");
                    return false;
                }

                nodeMap[node.Id] = node;
            }

            if (!nodeMap.ContainsKey(dialogueDefinition.StartNodeId))
            {
                error = new DomainError(
                    DomainErrorCode.ValidationFailed,
                    $"Dialogue '{dialogueDefinition.Id}' start node '{dialogueDefinition.StartNodeId}' does not exist.");
                return false;
            }

            error = null;
            return true;
        }

        public bool EvaluateConditions(GameState gameState, IReadOnlyList<DialogueConditionDefinition> conditions)
        {
            foreach (DialogueConditionDefinition condition in conditions)
            {
                if (!EvaluateCondition(gameState, condition))
                {
                    return false;
                }
            }

            return true;
        }

        public DomainResult ApplyEffects(GameState gameState, IReadOnlyList<DialogueEffectDefinition> effects, CommandContext context)
        {
            foreach (DialogueEffectDefinition effect in effects)
            {
                DomainResult result = ApplyEffect(gameState, effect, context);
                if (!result.IsSuccess)
                {
                    return result;
                }
            }

            return DomainResult.Success();
        }

        private bool EvaluateCondition(GameState gameState, DialogueConditionDefinition condition)
        {
            switch (condition.ConditionType)
            {
                case DialogueConditionType.WorldBoolEquals:
                    if (!gameState.WorldState.TryGet(condition.Key, out WorldVariableValue boolValue))
                    {
                        return false;
                    }

                    return boolValue.TryGetBool(out bool actualBool) && actualBool == condition.BoolValue;

                case DialogueConditionType.WorldIntAtLeast:
                    if (!gameState.WorldState.TryGet(condition.Key, out WorldVariableValue minValue))
                    {
                        return false;
                    }

                    return minValue.TryGetInt(out int actualMin) && actualMin >= condition.IntValue;

                case DialogueConditionType.WorldIntAtMost:
                    if (!gameState.WorldState.TryGet(condition.Key, out WorldVariableValue maxValue))
                    {
                        return false;
                    }

                    return maxValue.TryGetInt(out int actualMax) && actualMax <= condition.IntValue;

                case DialogueConditionType.QuestStatusIs:
                    if (!gameState.QuestState.TryGet(condition.Key, out QuestInstanceState quest))
                    {
                        return condition.QuestStatus == QuestStatus.NotStarted;
                    }

                    return quest.Status == condition.QuestStatus;

                default:
                    return false;
            }
        }

        private DomainResult ApplyEffect(GameState gameState, DialogueEffectDefinition effect, CommandContext context)
        {
            switch (effect.EffectType)
            {
                case DialogueEffectType.SetWorldBool:
                    return _setWorldHandler.Handle(
                        gameState,
                        new SetWorldVariableCommand(effect.Key, WorldVariableValue.FromBool(effect.BoolValue)),
                        context);

                case DialogueEffectType.SetWorldInt:
                    return _setWorldHandler.Handle(
                        gameState,
                        new SetWorldVariableCommand(effect.Key, WorldVariableValue.FromInt(effect.IntValue)),
                        context);

                case DialogueEffectType.AddWorldInt:
                    int current = 0;
                    if (gameState.WorldState.TryGet(effect.Key, out WorldVariableValue variable))
                    {
                        if (!variable.TryGetInt(out current))
                        {
                            return DomainResult.Fail(new DomainError(
                                DomainErrorCode.ValidationFailed,
                                $"World variable '{effect.Key}' is not an int and cannot be incremented."));
                        }
                    }

                    return _setWorldHandler.Handle(
                        gameState,
                        new SetWorldVariableCommand(effect.Key, WorldVariableValue.FromInt(checked(current + effect.IntValue))),
                        context);

                case DialogueEffectType.EmitTalkSignal:
                    return _questSignalHandler.Handle(
                        gameState,
                        new EmitQuestSignalCommand(QuestSignalType.Talk, effect.Key, 1),
                        context);

                case DialogueEffectType.EmitVisitSignal:
                    return _questSignalHandler.Handle(
                        gameState,
                        new EmitQuestSignalCommand(QuestSignalType.Visit, effect.Key, 1),
                        context);

                default:
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.UnsupportedOperation,
                        $"Unsupported dialogue effect type '{effect.EffectType}'."));
            }
        }
    }
}
