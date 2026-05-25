using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Dialogue.Commands;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Dialogue.Queries;

public sealed class GetAvailableDialogueChoicesQueryHandler : IQueryHandler<GetAvailableDialogueChoicesQuery, IReadOnlyList<string>>
{
    private readonly IGameDefinitionCatalog _definitions;
    private readonly DialogueRuntime _runtime = new();

    public GetAvailableDialogueChoicesQueryHandler(IGameDefinitionCatalog definitions)
    {
        _definitions = definitions;
    }

    public IReadOnlyList<string> Query(GameState gameState, GetAvailableDialogueChoicesQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.DialogueId))
        {
            return Array.Empty<string>();
        }

        if (!_definitions.TryGetDialogue(query.DialogueId, out DialogueDefinition dialogue))
        {
            return Array.Empty<string>();
        }

        if (!gameState.DialogueState.TryGet(query.DialogueId, out DialogueInstanceState state)
            || state.Completed
            || string.IsNullOrWhiteSpace(state.CurrentNodeId))
        {
            return Array.Empty<string>();
        }

        DialogueNodeDefinition? currentNode = null;
        foreach (DialogueNodeDefinition node in dialogue.Nodes)
        {
            if (node.Id == state.CurrentNodeId)
            {
                currentNode = node;
                break;
            }
        }

        if (currentNode is null || currentNode.Choices.Count == 0)
        {
            return Array.Empty<string>();
        }

        List<string> visible = new(currentNode.Choices.Count);
        foreach (DialogueChoiceDefinition choice in currentNode.Choices)
        {
            if (_runtime.EvaluateConditions(gameState, choice.Conditions))
            {
                visible.Add(choice.Id);
            }
        }

        return visible;
    }
}
