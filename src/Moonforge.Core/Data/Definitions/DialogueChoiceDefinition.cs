using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

public sealed class DialogueChoiceDefinition
{
    public DialogueChoiceDefinition(
        string id,
        string textKey,
        string? nextNodeId = null,
        IReadOnlyList<DialogueConditionDefinition>? conditions = null,
        IReadOnlyList<DialogueEffectDefinition>? effects = null)
    {
        Id = id;
        TextKey = textKey;
        NextNodeId = nextNodeId;
        Conditions = conditions ?? System.Array.Empty<DialogueConditionDefinition>();
        Effects = effects ?? System.Array.Empty<DialogueEffectDefinition>();
    }

    public string Id { get; }

    public string TextKey { get; }

    public string? NextNodeId { get; }

    public IReadOnlyList<DialogueConditionDefinition> Conditions { get; }

    public IReadOnlyList<DialogueEffectDefinition> Effects { get; }
}
