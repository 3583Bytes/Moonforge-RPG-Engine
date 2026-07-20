using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Exploration.Events
{

    public sealed class ExplorationMapSwitchedEvent : DomainEvent
    {
        public ExplorationMapSwitchedEvent(string fromMapId, string toMapId)
            : base("exploration.map.switched")
        {
            FromMapId = fromMapId;
            ToMapId = toMapId;
        }

        /// <summary>Previously active map id — empty when no map was configured yet.</summary>
        public string FromMapId { get; }

        public string ToMapId { get; }
    }
}
