using Moonforge.Core.Exploration;
using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Interactables.Events
{

    public sealed class InteractablePlacedEvent : DomainEvent
    {
        public InteractablePlacedEvent(string instanceId, string definitionId, GridPosition position)
            : base(nameof(InteractablePlacedEvent))
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Position = position;
        }

        public string InstanceId { get; }

        public string DefinitionId { get; }

        public GridPosition Position { get; }
    }
}
