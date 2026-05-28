using Moonforge.Core.Interactables;

namespace Moonforge.Core.Data.Definitions
{

    /// <summary>
    /// One side-effect that fires when an interactable is successfully used. Effects are run in
    /// declaration order; any failure aborts the interaction and rolls back state changes.
    /// </summary>
    public sealed class InteractableEffectDefinition
    {
        public InteractableEffectDefinition(
            InteractableEffectKind kind,
            string targetId,
            bool boolValue = false,
            int intValue = 0)
        {
            Kind = kind;
            TargetId = targetId;
            BoolValue = boolValue;
            IntValue = intValue;
        }

        public InteractableEffectKind Kind { get; }

        /// <summary>
        /// Interpretation depends on <see cref="Kind"/>: loot table id, world variable key,
        /// target interactable id, or signal key.
        /// </summary>
        public string TargetId { get; }

        public bool BoolValue { get; }

        public int IntValue { get; }
    }
}
