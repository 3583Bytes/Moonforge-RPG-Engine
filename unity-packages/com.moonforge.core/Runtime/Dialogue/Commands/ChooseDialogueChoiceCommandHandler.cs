using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Dialogue.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Dialogue.Commands
{

    public sealed class ChooseDialogueChoiceCommandHandler : ICommandHandler<ChooseDialogueChoiceCommand>
    {
        private readonly DialogueRuntime _runtime = new();

        public DomainResult Handle(GameState gameState, ChooseDialogueChoiceCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.DialogueId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Dialogue ID is required."));
            }

            if (string.IsNullOrWhiteSpace(command.ChoiceId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Dialogue choice ID is required."));
            }

            if (!context.Definitions.TryGetDialogue(command.DialogueId, out DialogueDefinition dialogue))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown dialogue definition '{command.DialogueId}'."));
            }

            if (!_runtime.TryBuildNodeMap(dialogue, out Dictionary<string, DialogueNodeDefinition> nodeMap, out DomainError? error))
            {
                return DomainResult.Fail(error!);
            }

            if (!gameState.DialogueState.TryGet(command.DialogueId, out DialogueInstanceState state))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, $"Dialogue '{command.DialogueId}' has not been started."));
            }

            if (state.Completed || string.IsNullOrWhiteSpace(state.CurrentNodeId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, $"Dialogue '{command.DialogueId}' is not awaiting a choice."));
            }

            if (!nodeMap.TryGetValue(state.CurrentNodeId, out DialogueNodeDefinition currentNode))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    $"Dialogue '{command.DialogueId}' current node '{state.CurrentNodeId}' is invalid."));
            }

            DialogueChoiceDefinition? selected = null;
            foreach (DialogueChoiceDefinition choice in currentNode.Choices)
            {
                if (choice.Id == command.ChoiceId)
                {
                    selected = choice;
                    break;
                }
            }

            if (selected is null)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.NotFound,
                    $"Choice '{command.ChoiceId}' does not exist on node '{currentNode.Id}'."));
            }

            if (!_runtime.EvaluateConditions(gameState, selected.Conditions))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    $"Choice '{command.ChoiceId}' conditions are not met."));
            }

            DomainResult effectsResult = _runtime.ApplyEffects(gameState, selected.Effects, context);
            if (!effectsResult.IsSuccess)
            {
                return effectsResult;
            }

            state.MarkChosenChoice(command.ChoiceId);
            context.EventSink.Publish(new DialogueChoiceSelectedEvent(command.DialogueId, currentNode.Id, command.ChoiceId));

            if (string.IsNullOrWhiteSpace(selected.NextNodeId))
            {
                state.CurrentNodeId = null;
                state.Completed = true;
                context.EventSink.Publish(new DialogueCompletedEvent(command.DialogueId));
                return DomainResult.Success();
            }

            return EnterNode(gameState, state, command.DialogueId, selected.NextNodeId!, nodeMap, context, maxHops: 32);
        }

        private DomainResult EnterNode(
            GameState gameState,
            DialogueInstanceState state,
            string dialogueId,
            string nodeId,
            IReadOnlyDictionary<string, DialogueNodeDefinition> nodeMap,
            CommandContext context,
            int maxHops)
        {
            string currentNodeId = nodeId;
            int hops = 0;

            while (true)
            {
                if (hops++ > maxHops)
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.ValidationFailed,
                        $"Dialogue '{dialogueId}' exceeded max auto-next hops. Check for loops."));
                }

                if (!nodeMap.TryGetValue(currentNodeId, out DialogueNodeDefinition node))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.ValidationFailed,
                        $"Dialogue '{dialogueId}' references unknown node '{currentNodeId}'."));
                }

                if (!_runtime.EvaluateConditions(gameState, node.Conditions))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.ValidationFailed,
                        $"Dialogue node '{currentNodeId}' conditions are not met."));
                }

                state.CurrentNodeId = currentNodeId;
                state.MarkVisitedNode(currentNodeId);
                context.EventSink.Publish(new DialogueNodeEnteredEvent(dialogueId, currentNodeId));

                DomainResult effectResult = _runtime.ApplyEffects(gameState, node.OnEnterEffects, context);
                if (!effectResult.IsSuccess)
                {
                    return effectResult;
                }

                if (!string.IsNullOrWhiteSpace(node.AutoNextNodeId))
                {
                    currentNodeId = node.AutoNextNodeId!;
                    continue;
                }

                if (node.Choices.Count == 0)
                {
                    state.CurrentNodeId = null;
                    state.Completed = true;
                    context.EventSink.Publish(new DialogueCompletedEvent(dialogueId));
                }

                return DomainResult.Success();
            }
        }
    }
}
