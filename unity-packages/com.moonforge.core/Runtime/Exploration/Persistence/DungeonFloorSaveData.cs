using System.Collections.Generic;

namespace Moonforge.Core.Exploration.Persistence
{

    /// <summary>
    /// JSON-friendly projection of a <see cref="DungeonFloorBlueprint"/>. Tiles are flattened
    /// to a list of integers (cast from <see cref="ExplorationTileFlags"/>) and pillar positions
    /// to an interleaved X/Y list so the payload survives standard JSON serializers without any
    /// custom converters. Use <see cref="DungeonFloorSaveMapper"/> to convert in either direction.
    /// </summary>
    public sealed class DungeonFloorSaveData
    {
        public DungeonFloorSaveData()
        {
        }

        public DungeonFloorSaveData(
            int width,
            int height,
            List<int> tiles,
            int spawnX,
            int spawnY,
            int stairsX,
            int stairsY,
            List<int>? pillarsXY = null)
        {
            Width = width;
            Height = height;
            Tiles = tiles;
            SpawnX = spawnX;
            SpawnY = spawnY;
            StairsX = stairsX;
            StairsY = stairsY;
            PillarsXY = pillarsXY;
        }

        public int Width { get; set; }

        public int Height { get; set; }

        public List<int> Tiles { get; set; } = new();

        public int SpawnX { get; set; }

        public int SpawnY { get; set; }

        public int StairsX { get; set; }

        public int StairsY { get; set; }

        public List<int>? PillarsXY { get; set; }
    }
}
