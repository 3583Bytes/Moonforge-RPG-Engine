namespace Moonforge.Core.Interactables
{

    /// <summary>
    /// Lifecycle states an <c>InteractableInstance</c> can be in. Designers can also flip an
    /// interactable between these states via the <c>ChangeInteractableStatus</c> effect.
    /// </summary>
    public enum InteractableStatus
    {
        /// <summary>Initial state: ready to be interacted with.</summary>
        Default = 0,

        /// <summary>Chest opened, door open. Effects fired at least once.</summary>
        Opened = 1,

        /// <summary>Uses depleted; further interactions do nothing.</summary>
        Consumed = 2,

        /// <summary>Breakable container destroyed.</summary>
        Broken = 3,

        /// <summary>Was locked, key has been used; door is open or chest is accessible.</summary>
        Unlocked = 4
    }
}
