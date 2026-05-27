using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Interactables.Events
{

    public sealed class InteractableInteractedEvent : DomainEvent
    {
        public InteractableInteractedEvent(string instanceId, string definitionId, string actorId)
            : base(nameof(InteractableInteractedEvent))
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            ActorId = actorId;
        }

        public string InstanceId { get; }

        public string DefinitionId { get; }

        public string ActorId { get; }
    }
}
