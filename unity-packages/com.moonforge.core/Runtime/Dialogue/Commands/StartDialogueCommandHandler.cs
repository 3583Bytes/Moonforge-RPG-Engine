using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Dialogue.Events;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.World.Commands;

namespace Moonforge.Core.Dialogue.Commands
{

    public sealed class StartDialogueCommandHandler : ICommandHandler<StartDialogueCommand>
    {
        private readonly DialogueRuntime _runtime;

        /// <summary>
        /// Pass the handlers you registered for <see cref="SetWorldVariableCommand"/> and
        /// <see cref="EmitQuestSignalCommand"/> so dialogue effects behave identically to
        /// directly dispatched commands. Both default to the built-in handlers.
        /// </summary>
        public StartDialogueCommandHandler(
            ICommandHandler<SetWorldVariableCommand>? setWorldVariableHandler = null,
            ICommandHandler<EmitQuestSignalCommand>? questSignalHandler = null)
        {
            _runtime = new DialogueRuntime(setWorldVariableHandler, questSignalHandler);
        }

        public DomainResult Handle(GameState gameState, StartDialogueCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.DialogueId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Dialogue ID is required."));
            }

            if (!context.Definitions.TryGetDialogue(command.DialogueId, out DialogueDefinition dialogue))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown dialogue definition '{command.DialogueId}'."));
            }

            if (!_runtime.TryBuildNodeMap(dialogue, out Dictionary<string, DialogueNodeDefinition> nodeMap, out DomainError? error))
            {
                return DomainResult.Fail(error!);
            }

            DialogueInstanceState state = gameState.DialogueState.GetOrCreate(command.DialogueId);
            state.Completed = false;
            state.CurrentNodeId = dialogue.StartNodeId;
            context.EventSink.Publish(new DialogueStartedEvent(command.DialogueId));

            return EnterNode(gameState, state, command.DialogueId, dialogue.StartNodeId, nodeMap, context, maxHops: 32);
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
