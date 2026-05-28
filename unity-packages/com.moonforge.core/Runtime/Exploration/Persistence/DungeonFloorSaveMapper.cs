using System.Collections.Generic;
using System.Linq;

namespace Moonforge.Core.Exploration.Persistence
{

    /// <summary>
    /// Round-trips a <see cref="DungeonFloorBlueprint"/> through <see cref="DungeonFloorSaveData"/>.
    /// Kept separate from the blueprint so persistence concerns don't leak into the generator
    /// output type.
    /// </summary>
    public static class DungeonFloorSaveMapper
    {
        public static DungeonFloorSaveData ToSaveData(DungeonFloorBlueprint floor)
        {
            List<int> pillarsXY = new(floor.Pillars.Count * 2);
            for (int i = 0; i < floor.Pillars.Count; i++)
            {
                pillarsXY.Add(floor.Pillars[i].X);
                pillarsXY.Add(floor.Pillars[i].Y);
            }

            return new DungeonFloorSaveData(
                floor.Width,
                floor.Height,
                floor.Tiles.Select(x => (int)x).ToList(),
                floor.Spawn.X,
                floor.Spawn.Y,
                floor.Stairs.X,
                floor.Stairs.Y,
                pillarsXY);
        }

        public static DungeonFloorBlueprint ToBlueprint(DungeonFloorSaveData saveData)
        {
            List<ExplorationTileFlags> tiles = saveData.Tiles.Select(x => (ExplorationTileFlags)x).ToList();
            List<GridPosition> pillars = new();
            if (saveData.PillarsXY is not null)
            {
                for (int i = 0; i + 1 < saveData.PillarsXY.Count; i += 2)
                {
                    pillars.Add(new GridPosition(saveData.PillarsXY[i], saveData.PillarsXY[i + 1]));
                }
            }

            return new DungeonFloorBlueprint(
                saveData.Width,
                saveData.Height,
                tiles,
                new GridPosition(saveData.SpawnX, saveData.SpawnY),
                new GridPosition(saveData.StairsX, saveData.StairsY),
                pillars);
        }
    }
}
