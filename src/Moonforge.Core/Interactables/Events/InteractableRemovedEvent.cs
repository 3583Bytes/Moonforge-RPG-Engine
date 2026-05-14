using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Interactables.Events;

public sealed class InteractableRemovedEvent : DomainEvent
{
    public InteractableRemovedEvent(string instanceId)
        : base(nameof(InteractableRemovedEvent))
    {
        InstanceId = instanceId;
    }

    public string InstanceId { get; }
}
