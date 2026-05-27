using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Exploration.Events
{

    public sealed class ExplorationMapConfiguredEvent : DomainEvent
    {
        public ExplorationMapConfiguredEvent(string mapId, int width, int height)
            : base("exploration.map.configured")
        {
            MapId = mapId;
            Width = width;
            Height = height;
        }

        public string MapId { get; }

        public int Width { get; }

        public int Height { get; }
    }
}
