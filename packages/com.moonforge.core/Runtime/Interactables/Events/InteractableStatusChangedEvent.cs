using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Interactables.Events
{

    public sealed class InteractableStatusChangedEvent : DomainEvent
    {
        public InteractableStatusChangedEvent(string instanceId, InteractableStatus previous, InteractableStatus next)
            : base(nameof(InteractableStatusChangedEvent))
        {
            InstanceId = instanceId;
            Previous = previous;
            Next = next;
        }

        public string InstanceId { get; }

        public InteractableStatus Previous { get; }

        public InteractableStatus Next { get; }
    }
}
