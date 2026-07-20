using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Exploration.Events
{

    public sealed class ExplorationMapRemovedEvent : DomainEvent
    {
        public ExplorationMapRemovedEvent(string mapId)
            : base("exploration.map.removed")
        {
            MapId = mapId;
        }

        public string MapId { get; }
    }
}
