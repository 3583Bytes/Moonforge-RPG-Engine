using Moonforge.Core.Exploration;

using System.Collections.Generic;
namespace Moonforge.Sample.Roguelike.WorldGen
{

    /// <summary>
    /// Builds the town map programmatically: walks an outer wall ring, drops named building
    /// rectangles for each landmark, and adds floor/wall decorations so the result reads as a
    /// settlement rather than a featureless box. The returned blueprint is consumed by the
    /// game loop to seed <see cref="ConfigureExplorationMapCommand"/> + marker overlays.
    /// </summary>
    internal static class TownLayout
    {
        private const int Width = 60;
        private const int Height = 22;

        public static TownBlueprint Build()
        {
            ExplorationTileFlags[] tiles = new ExplorationTileFlags[Width * Height];
            Dictionary<GridPosition, char> wallDecos = new();
            Dictionary<GridPosition, char> floorDecos = new();

            // 1. Carve open courtyard inside an outer wall ring.
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    bool isBorder = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                    tiles[Index(x, y)] = isBorder
                        ? ExplorationTileFlags.BlocksLineOfSight
                        : ExplorationTileFlags.Walkable;
                }
            }

            DecorateOuterWall(wallDecos);

            // 2. Carve buildings. Each is a wall ring with one floor doorway; the interactable
            //    landmark sits on a floor tile in front of (or inside) the door so the player can
            //    walk up and press E.
            Dictionary<char, GridPosition> landmarks = new();

            CarveBuilding(tiles, wallDecos, landmarks, new BuildingSpec(
                X: 4, Y: 2, BuildingWidth: 10, BuildingHeight: 5,
                DoorX: 8, DoorOnSouth: true,
                Landmark: 'A', LandmarkInsideBuilding: false));

            CarveBuilding(tiles, wallDecos, landmarks, new BuildingSpec(
                X: 19, Y: 2, BuildingWidth: 10, BuildingHeight: 5,
                DoorX: 23, DoorOnSouth: true,
                Landmark: 'S', LandmarkInsideBuilding: false));

            CarveBuilding(tiles, wallDecos, landmarks, new BuildingSpec(
                X: 34, Y: 2, BuildingWidth: 10, BuildingHeight: 5,
                DoorX: 38, DoorOnSouth: true,
                Landmark: 'H', LandmarkInsideBuilding: false));

            CarveBuilding(tiles, wallDecos, landmarks, new BuildingSpec(
                X: 4, Y: 12, BuildingWidth: 11, BuildingHeight: 5,
                DoorX: 8, DoorOnSouth: true,
                Landmark: 'Q', LandmarkInsideBuilding: false));

            CarveBuilding(tiles, wallDecos, landmarks, new BuildingSpec(
                X: 21, Y: 13, BuildingWidth: 8, BuildingHeight: 4,
                DoorX: 24, DoorOnSouth: true,
                Landmark: 'G', LandmarkInsideBuilding: false));

            // Cache: small kiosk in the south-east.
            CarveBuilding(tiles, wallDecos, landmarks, new BuildingSpec(
                X: 45, Y: 9, BuildingWidth: 6, BuildingHeight: 4,
                DoorX: 47, DoorOnSouth: true,
                Landmark: 'C', LandmarkInsideBuilding: false));

            // Dungeon gate: distinct double-line border so it reads as the way out.
            CarveBuilding(tiles, wallDecos, landmarks, new BuildingSpec(
                X: 45, Y: 14, BuildingWidth: 7, BuildingHeight: 5,
                DoorX: 48, DoorOnSouth: true,
                Landmark: '>', LandmarkInsideBuilding: true,
                UseDoubleWalls: true));

            // Fountain: free-standing decoration with water tiles around it.
            PlaceFountain(tiles, wallDecos, floorDecos, landmarks, centerX: 36, centerY: 13);

            // Player spawn: open plaza in the middle, no marker.
            GridPosition heroSpawn = new(30, 10);

            // Sprinkle a few cosmetic floor decorations so the courtyard isn't a uniform dot field.
            SprinkleFloorAccents(tiles, floorDecos);

            return new TownBlueprint(
                Width,
                Height,
                tiles,
                heroSpawn,
                landmarks,
                wallDecos,
                floorDecos);
        }

        private static int Index(int x, int y) => (y * Width) + x;

        private static void DecorateOuterWall(Dictionary<GridPosition, char> wallDecos)
        {
            for (int x = 0; x < Width; x++)
            {
                wallDecos[new GridPosition(x, 0)] = '═';
                wallDecos[new GridPosition(x, Height - 1)] = '═';
            }

            for (int y = 0; y < Height; y++)
            {
                wallDecos[new GridPosition(0, y)] = '║';
                wallDecos[new GridPosition(Width - 1, y)] = '║';
            }

            wallDecos[new GridPosition(0, 0)] = '╔';
            wallDecos[new GridPosition(Width - 1, 0)] = '╗';
            wallDecos[new GridPosition(0, Height - 1)] = '╚';
            wallDecos[new GridPosition(Width - 1, Height - 1)] = '╝';
        }

        private static void CarveBuilding(
            ExplorationTileFlags[] tiles,
            Dictionary<GridPosition, char> wallDecos,
            Dictionary<char, GridPosition> landmarks,
            BuildingSpec spec)
        {
            char topLeft = spec.UseDoubleWalls ? '╔' : '┌';
            char topRight = spec.UseDoubleWalls ? '╗' : '┐';
            char bottomLeft = spec.UseDoubleWalls ? '╚' : '└';
            char bottomRight = spec.UseDoubleWalls ? '╝' : '┘';
            char horizontal = spec.UseDoubleWalls ? '═' : '─';
            char vertical = spec.UseDoubleWalls ? '║' : '│';

            int x0 = spec.X;
            int y0 = spec.Y;
            int x1 = spec.X + spec.BuildingWidth - 1;
            int y1 = spec.Y + spec.BuildingHeight - 1;

            // Wall the perimeter.
            for (int x = x0; x <= x1; x++)
            {
                SetWall(tiles, wallDecos, x, y0, horizontal);
                SetWall(tiles, wallDecos, x, y1, horizontal);
            }

            for (int y = y0; y <= y1; y++)
            {
                SetWall(tiles, wallDecos, x0, y, vertical);
                SetWall(tiles, wallDecos, x1, y, vertical);
            }

            SetWall(tiles, wallDecos, x0, y0, topLeft);
            SetWall(tiles, wallDecos, x1, y0, topRight);
            SetWall(tiles, wallDecos, x0, y1, bottomLeft);
            SetWall(tiles, wallDecos, x1, y1, bottomRight);

            // Carve interior as walkable (no encounters in town).
            for (int y = y0 + 1; y <= y1 - 1; y++)
            {
                for (int x = x0 + 1; x <= x1 - 1; x++)
                {
                    tiles[Index(x, y)] = ExplorationTileFlags.Walkable;
                }
            }

            // Punch out the doorway: bottom wall tile becomes walkable.
            int doorY = spec.DoorOnSouth ? y1 : y0;
            tiles[Index(spec.DoorX, doorY)] = ExplorationTileFlags.Walkable;
            wallDecos.Remove(new GridPosition(spec.DoorX, doorY));

            // Landmark tile: either on the doorway itself (interior landmarks like the dungeon
            // gate) or one step outside the door (NPC stalls).
            int landmarkY = spec.LandmarkInsideBuilding
                ? doorY
                : (spec.DoorOnSouth ? doorY + 1 : doorY - 1);
            GridPosition landmarkPos = new(spec.DoorX, landmarkY);
            tiles[Index(landmarkPos.X, landmarkPos.Y)] = ExplorationTileFlags.Walkable | ExplorationTileFlags.Interactable;
            landmarks[spec.Landmark] = landmarkPos;
        }

        private static void PlaceFountain(
            ExplorationTileFlags[] tiles,
            Dictionary<GridPosition, char> wallDecos,
            Dictionary<GridPosition, char> floorDecos,
            Dictionary<char, GridPosition> landmarks,
            int centerX,
            int centerY)
        {
            // Single solid block in the middle acts as the fountain plinth.
            SetWall(tiles, wallDecos, centerX, centerY, '◉');

            // Ring of "water" tiles (still walkable, but with a wave decoration).
            (int dx, int dy)[] waterOffsets =
            new[]
            {
                (-1, -1), (0, -1), (1, -1),
                (-1, 0),           (1, 0),
                (-1, 1),  (0, 1),  (1, 1)
            };

            foreach ((int dx, int dy) in waterOffsets)
            {
                int x = centerX + dx;
                int y = centerY + dy;
                if (x <= 0 || y <= 0 || x >= Width - 1 || y >= Height - 1)
                {
                    continue;
                }

                tiles[Index(x, y)] = ExplorationTileFlags.Walkable;
                floorDecos[new GridPosition(x, y)] = '~';
            }

            // The landmark itself sits one tile south of the plinth so the player has a clear
            // doorstep to interact from.
            GridPosition landmarkPos = new(centerX, centerY + 2);
            if (landmarkPos.Y < Height - 1)
            {
                tiles[Index(landmarkPos.X, landmarkPos.Y)] = ExplorationTileFlags.Walkable | ExplorationTileFlags.Interactable;
                landmarks['F'] = landmarkPos;
            }
        }

        private static void SprinkleFloorAccents(
            ExplorationTileFlags[] tiles,
            Dictionary<GridPosition, char> floorDecos)
        {
            // Cosmetic-only — these positions are hand-picked to avoid the doorway tiles and the
            // hero spawn, and they don't change tile flags.
            (int x, int y, char glyph)[] accents =
            new[]
            {
                (2, 9,  '♣'),
                (16, 9,  '♣'),
                (32, 8,  '♣'),
                (56, 8,  '♣'),
                (2, 19, '♣'),
                (16, 19, '♣'),
                (50, 5,  '♣')
            };

            foreach ((int x, int y, char glyph) in accents)
            {
                GridPosition pos = new(x, y);
                if (pos.X <= 0 || pos.Y <= 0 || pos.X >= Width - 1 || pos.Y >= Height - 1)
                {
                    continue;
                }

                if ((tiles[Index(pos.X, pos.Y)] & ExplorationTileFlags.Walkable) != ExplorationTileFlags.Walkable)
                {
                    continue;
                }

                floorDecos[pos] = glyph;
            }
        }

        private static void SetWall(
            ExplorationTileFlags[] tiles,
            Dictionary<GridPosition, char> wallDecos,
            int x,
            int y,
            char glyph)
        {
            tiles[Index(x, y)] = ExplorationTileFlags.BlocksLineOfSight;
            wallDecos[new GridPosition(x, y)] = glyph;
        }

        private sealed record BuildingSpec(
            int X,
            int Y,
            int BuildingWidth,
            int BuildingHeight,
            int DoorX,
            bool DoorOnSouth,
            char Landmark,
            bool LandmarkInsideBuilding,
            bool UseDoubleWalls = false);
    }

    internal sealed record TownBlueprint(
        int Width,
        int Height,
        IReadOnlyList<ExplorationTileFlags> Tiles,
        GridPosition HeroSpawn,
        IReadOnlyDictionary<char, GridPosition> Landmarks,
        IReadOnlyDictionary<GridPosition, char> WallDecorations,
        IReadOnlyDictionary<GridPosition, char> FloorDecorations);
}
