using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Interactables.Events;

/// <summary>
/// Fired when an actor tries to interact with a locked interactable and does not have the
/// required key. Carries the required key's item id so UI can prompt with what's missing.
/// </summary>
public sealed class InteractableLockedEvent : DomainEvent
{
    public InteractableLockedEvent(string instanceId, string actorId, string? requiredKeyItemId)
        : base(nameof(InteractableLockedEvent))
    {
        InstanceId = instanceId;
        ActorId = actorId;
        RequiredKeyItemId = requiredKeyItemId;
    }

    public string InstanceId { get; }

    public string ActorId { get; }

    public string? RequiredKeyItemId { get; }
}
