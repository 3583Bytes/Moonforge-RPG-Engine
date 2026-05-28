using System.Collections.Generic;

namespace Moonforge.Core.Exploration
{

    /// <summary>
    /// Output of a dungeon/floor generator: a rectangular grid of <see cref="ExplorationTileFlags"/>
    /// plus the spawn and stairs positions and any decorative-but-load-bearing extras (e.g. pillar
    /// positions inside large rooms). This is the data form a generator hands off to whatever
    /// consumes the floor — rendering, persistence, and the engine's exploration commands that
    /// drive movement and interaction.
    /// </summary>
    public sealed class DungeonFloorBlueprint
    {
        public DungeonFloorBlueprint(
            int width,
            int height,
            IReadOnlyList<ExplorationTileFlags> tiles,
            GridPosition spawn,
            GridPosition stairs,
            IReadOnlyList<GridPosition> pillars)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
            Spawn = spawn;
            Stairs = stairs;
            Pillars = pillars;
        }

        public int Width { get; }

        public int Height { get; }

        public IReadOnlyList<ExplorationTileFlags> Tiles { get; }

        public GridPosition Spawn { get; }

        public GridPosition Stairs { get; }

        public IReadOnlyList<GridPosition> Pillars { get; }
    }
}
