using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class ExplorationStateSnapshot
    {
        public ExplorationMapSnapshot Map { get; set; } = new();

        public List<ExplorationActorSnapshot> Actors { get; set; } = new();
    }

    public sealed class ExplorationMapSnapshot
    {
        public string MapId { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public List<int> Tiles { get; set; } = new();
    }

    public sealed class ExplorationActorSnapshot
    {
        public string ActorId { get; set; } = string.Empty;

        public int X { get; set; }

        public int Y { get; set; }

        public bool BlocksMovement { get; set; }
    }
}
