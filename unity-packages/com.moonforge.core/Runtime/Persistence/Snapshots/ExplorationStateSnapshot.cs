using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class ExplorationStateSnapshot
    {
        /// <summary>Legacy single-map shape (schema ≤ 8) — still read on load, no longer written.</summary>
        public ExplorationMapSnapshot Map { get; set; } = new();

        /// <summary>Legacy single-map actor list (schema ≤ 8) — still read on load, no longer written.</summary>
        public List<ExplorationActorSnapshot> Actors { get; set; } = new();

        /// <summary>Active map id (schema 9+).</summary>
        public string ActiveMapId { get; set; } = string.Empty;

        /// <summary>Every configured map with its own actor set (schema 9+), sorted by map id.</summary>
        public List<ExplorationMapEntrySnapshot> Maps { get; set; } = new();
    }

    /// <summary>One named exploration map plus the actors that live on it.</summary>
    public sealed class ExplorationMapEntrySnapshot
    {
        public string MapId { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public List<int> Tiles { get; set; } = new();

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
