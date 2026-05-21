using System;
using System.Collections.Generic;
using Moonforge.Core.Exploration;
using Moonforge.Core.Runtime.Random;

namespace Moonforge.Sample.MonsterCatcher.WorldGen;

/// <summary>
/// Builds a deterministic west-to-east overworld map sliced into <see cref="ZoneCount"/>
/// vertical bands. Grass density (encounter chance) increases by zone so the player
/// progressively walks into harder territory. PokeCenters sit on zone boundaries 1, 4, 7
/// so the player has 3 natural rest stops between spawn and the goal at the east edge.
/// </summary>
internal static class OverworldGenerator
{
    public const int Width = 64;
    public const int Height = 24;
    public const int ZoneCount = 10;

    public static Overworld Generate(IRandomSource random)
    {
        OverworldTile[] tiles = new OverworldTile[Width * Height];
        int[] zoneIds = new int[Width * Height];

        // Pass 1: fill with grass everywhere inside the border, walls on the border.
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int idx = (y * Width) + x;
                bool border = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                tiles[idx] = border ? OverworldTile.Wall : OverworldTile.Grass;
                zoneIds[idx] = border ? 0 : ResolveZoneId(x);
            }
        }

        // Pass 2: carve an east-west walking path through the middle so the player can always
        // make progress without stepping on grass. The path wiggles a little so each zone
        // looks different. Two tiles tall — wide enough to give the player some breathing room.
        int pathY = Height / 2;
        for (int x = 1; x < Width - 1; x++)
        {
            // Wiggle: shift the path up or down by 1 row occasionally based on x.
            if (x % 8 == 0)
            {
                int shift = random.NextInt(3) - 1;   // -1, 0, or +1
                pathY = Math.Clamp(pathY + shift, 3, Height - 4);
            }

            for (int yOff = -1; yOff <= 1; yOff++)
            {
                int py = pathY + yOff;
                if (py <= 0 || py >= Height - 1) continue;
                int idx = (py * Width) + x;
                if (tiles[idx] == OverworldTile.Wall) continue;
                tiles[idx] = OverworldTile.Path;
            }
        }

        // Pass 3: punch in some interior walls (trees) — density rises with zone so the
        // later zones feel claustrophobic.
        for (int x = 2; x < Width - 2; x++)
        {
            int zone = ResolveZoneId(x);
            int density = 6 + zone;   // ~6% in zone 1, ~16% in zone 10
            for (int y = 2; y < Height - 2; y++)
            {
                int idx = (y * Width) + x;
                if (tiles[idx] != OverworldTile.Grass) continue;   // leave path tiles alone
                if (random.NextInt(100) < density)
                {
                    tiles[idx] = OverworldTile.Wall;
                }
            }
        }

        // Pass 4: water decoration — a small pond around zone 3 just for visual flavor.
        DrawPond(tiles, centerX: ColumnForZone(3) + 2, centerY: 4, radius: 2);
        DrawPond(tiles, centerX: ColumnForZone(7) + 3, centerY: Height - 5, radius: 2);

        // Pass 5: PokeCenters. Place at zones 1, 4, 7 on the path so the player can't miss them.
        List<GridPosition> pokeCenters = new();
        foreach (int zone in new[] { 1, 4, 7 })
        {
            int cx = ColumnForZone(zone) + 1;
            int cy = pathY;   // approximation — the player walks the path, so anchor there
            // Find the nearest path tile to (cx, cy).
            (int x, int y) = FindNearestTile(tiles, cx, cy, OverworldTile.Path);
            int idx = (y * Width) + x;
            tiles[idx] = OverworldTile.PokeCenter;
            pokeCenters.Add(new GridPosition(x, y));
        }

        // Pass 6: spawn (west edge of path) and goal (east edge of last zone).
        (int spawnX, int spawnY) = FindNearestTile(tiles, 1, Height / 2, OverworldTile.Path);
        (int goalX, int goalY) = FindNearestTile(tiles, Width - 2, Height / 2, OverworldTile.Path);
        tiles[(goalY * Width) + goalX] = OverworldTile.Goal;

        return new Overworld(
            width: Width,
            height: Height,
            tiles: tiles,
            zoneIds: zoneIds,
            spawn: new GridPosition(spawnX, spawnY),
            goal: new GridPosition(goalX, goalY),
            pokeCenters: pokeCenters,
            zoneCount: ZoneCount);
    }

    /// <summary>
    /// Translates the engine's tile flags so <see cref="Moonforge.Core.Exploration.Commands.ConfigureExplorationMapCommand"/>
    /// validates walkability the same way we do. Grass and path are walkable; PokeCenter and
    /// goal are walkable + Interactable (so a future query can find them). Wall and water block.
    /// </summary>
    public static ExplorationTileFlags[] ToEngineFlags(Overworld map)
    {
        ExplorationTileFlags[] flags = new ExplorationTileFlags[map.Tiles.Length];
        for (int i = 0; i < map.Tiles.Length; i++)
        {
            flags[i] = map.Tiles[i] switch
            {
                OverworldTile.Wall => ExplorationTileFlags.BlocksLineOfSight,
                OverworldTile.Water => ExplorationTileFlags.BlocksLineOfSight,
                OverworldTile.Grass => ExplorationTileFlags.Walkable | ExplorationTileFlags.EncounterAllowed,
                OverworldTile.Path => ExplorationTileFlags.Walkable,
                OverworldTile.PokeCenter => ExplorationTileFlags.Walkable | ExplorationTileFlags.Interactable,
                OverworldTile.Goal => ExplorationTileFlags.Walkable | ExplorationTileFlags.Interactable,
                _ => ExplorationTileFlags.BlocksLineOfSight
            };
        }

        return flags;
    }

    private static int ResolveZoneId(int x)
    {
        // 10 zones spread across the playable interior (x = 1 .. Width-2 inclusive).
        int interior = Width - 2;
        int zoneWidth = interior / ZoneCount;
        int zone = 1 + ((x - 1) / Math.Max(1, zoneWidth));
        return Math.Clamp(zone, 1, ZoneCount);
    }

    private static int ColumnForZone(int zone)
    {
        int interior = Width - 2;
        int zoneWidth = interior / ZoneCount;
        return 1 + (zone - 1) * zoneWidth;
    }

    private static (int x, int y) FindNearestTile(OverworldTile[] tiles, int startX, int startY, OverworldTile target)
    {
        // Spiral outward looking for a matching tile.
        for (int radius = 0; radius < Math.Max(Width, Height); radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                    int x = startX + dx;
                    int y = startY + dy;
                    if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                    if (tiles[(y * Width) + x] == target)
                    {
                        return (x, y);
                    }
                }
            }
        }

        return (startX, startY);
    }

    private static void DrawPond(OverworldTile[] tiles, int centerX, int centerY, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy > radius * radius) continue;
                int x = centerX + dx;
                int y = centerY + dy;
                if (x <= 0 || y <= 0 || x >= Width - 1 || y >= Height - 1) continue;
                int idx = (y * Width) + x;
                if (tiles[idx] == OverworldTile.Path) continue;   // keep path connected
                tiles[idx] = OverworldTile.Water;
            }
        }
    }
}
