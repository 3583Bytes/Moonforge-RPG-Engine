using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions
{

    public sealed class DialogueNodeDefinition
    {
        public DialogueNodeDefinition(
            string id,
            string textKey,
            IReadOnlyList<DialogueChoiceDefinition>? choices = null,
            IReadOnlyList<DialogueConditionDefinition>? conditions = null,
            IReadOnlyList<DialogueEffectDefinition>? onEnterEffects = null,
            string? autoNextNodeId = null)
        {
            Id = id;
            TextKey = textKey;
            Choices = choices ?? System.Array.Empty<DialogueChoiceDefinition>();
            Conditions = conditions ?? System.Array.Empty<DialogueConditionDefinition>();
            OnEnterEffects = onEnterEffects ?? System.Array.Empty<DialogueEffectDefinition>();
            AutoNextNodeId = autoNextNodeId;
        }

        public string Id { get; }

        public string TextKey { get; }

        public IReadOnlyList<DialogueChoiceDefinition> Choices { get; }

        public IReadOnlyList<DialogueConditionDefinition> Conditions { get; }

        public IReadOnlyList<DialogueEffectDefinition> OnEnterEffects { get; }

        public string? AutoNextNodeId { get; }
    }
}
