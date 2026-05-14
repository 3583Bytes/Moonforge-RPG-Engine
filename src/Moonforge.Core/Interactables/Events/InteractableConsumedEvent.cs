using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Interactables.Events;

public sealed class InteractableConsumedEvent : DomainEvent
{
    public InteractableConsumedEvent(string instanceId)
        : base(nameof(InteractableConsumedEvent))
    {
        InstanceId = instanceId;
    }

    public string InstanceId { get; }
}
