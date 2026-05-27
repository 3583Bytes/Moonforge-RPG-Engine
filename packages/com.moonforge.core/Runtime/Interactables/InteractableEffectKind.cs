namespace Moonforge.Core.Interactables
{

    public enum InteractableEffectKind
    {
        /// <summary>Rolls and atomically grants the loot table named by <c>TargetId</c>.</summary>
        GrantLootTable = 0,

        /// <summary>Sets a world variable: key=<c>TargetId</c>, value=<c>BoolValue</c>.</summary>
        SetWorldBool = 1,

        /// <summary>Sets a world variable: key=<c>TargetId</c>, value=<c>IntValue</c>.</summary>
        SetWorldInt = 2,

        /// <summary>Changes <c>TargetId</c> interactable's status to the value encoded in <c>IntValue</c> (cast from <c>InteractableStatus</c>).</summary>
        ChangeInteractableStatus = 3,

        /// <summary>Marks <c>TargetId</c> interactable as unlocked.</summary>
        UnlockInteractable = 4,

        /// <summary>Marks <c>TargetId</c> interactable as locked.</summary>
        LockInteractable = 5,

        /// <summary>Publishes an <c>InteractionSignalEvent</c> with key <c>TargetId</c>; for quest reactors and host scripts.</summary>
        EmitInteractionSignal = 6
    }
}
