using System;
using System.Collections.Generic;
using Moonforge.Core.Exploration;
using Moonforge.Core.Runtime.Random;

namespace Moonforge.Sample.MonsterCatcher.WorldGen;

/// <summary>
/// Deterministic per-screen generator. Each screen is built from a PCG32 stream seeded
/// from <c>(worldSeed, screenIndex)</c> so re-entering a screen by index always produces
/// the same layout. Natural biomes carve a west→east path through walls + grass; Town
/// and Champion biomes use fixed-layout generators.
/// </summary>
internal static class WorldScreenGenerator
{
    public const int ScreenWidth = 40;
    public const int ScreenHeight = 14;

    public static WorldScreen Generate(ulong worldSeed, int screenIndex, BiomeKind biome)
    {
        IRandomSource rng = new Pcg32RandomSource(worldSeed, (ulong)(0x1000 + screenIndex));
        return biome switch
        {
            BiomeKind.Town => GenerateTown(screenIndex, rng),
            BiomeKind.Champion => GenerateChampionHall(screenIndex),
            _ => GenerateWildScreen(screenIndex, biome, rng)
        };
    }

    // ---- Wild (natural-biome) screens -----------------------------------------------------

    private static WorldScreen GenerateWildScreen(int screenIndex, BiomeKind biome, IRandomSource rng)
    {
        BiomeProfile profile = BiomeRegistry.Get(biome);
        int w = ScreenWidth;
        int h = ScreenHeight;
        OverworldTile[] tiles = new OverworldTile[w * h];

        // Pass 1: walls on the border, "noise" inside (we overwrite path later).
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = (y * w) + x;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
                {
                    tiles[idx] = OverworldTile.Wall;
                    continue;
                }

                // Interior noise: roll wall → water → grass → bare path.
                int roll = rng.NextInt(100);
                if (roll < profile.WallDensityPercent) tiles[idx] = OverworldTile.Wall;
                else if (roll < profile.WallDensityPercent + profile.WaterDensityPercent) tiles[idx] = OverworldTile.Water;
                else
                {
                    int grassRoll = rng.NextInt(100);
                    tiles[idx] = grassRoll < profile.GrassFillPercent ? OverworldTile.Grass : OverworldTile.Path;
                }
            }
        }

        // Pass 2: pin entry & exit to the middle row so the central corridor lines up across
        // adjacent screens — pure-east walking always traverses the world. (We'll add visual
        // wiggle around the corridor instead of drifting the corridor itself.)
        int corridorY = h / 2;
        GridPosition westEntry = new(0, corridorY);
        GridPosition eastExit = new(w - 1, corridorY);

        // Pass 3: punch the entry/exit holes in the border.
        tiles[(corridorY * w) + 0] = OverworldTile.Path;
        tiles[(corridorY * w) + (w - 1)] = OverworldTile.Path;

        // Pass 4: carve a 3-tile-tall straight corridor along the middle row, with some
        // grass tiles sprinkled along it so a player walking the corridor still rolls
        // encounters (rate scales with biome grass-fill).
        CarveCorridor(tiles, w, h, corridorY, profile, rng);

        // Pass 5: heal pad placement. Screen 0 always gets one (so the starter has a safety
        // pad to retreat to); other screens get one ~35% of the time. The pad is pinned to
        // the corridor row at a randomized x in the middle third, so a player walking the
        // corridor west-to-east is guaranteed to step on it.
        List<GridPosition> healPads = new();
        bool placePad = screenIndex == 0 || rng.NextInt(100) < 35;
        if (placePad)
        {
            int padX = (w / 3) + rng.NextInt(w / 3);
            int padIdx = (corridorY * w) + padX;
            tiles[padIdx] = OverworldTile.HealPad;
            healPads.Add(new GridPosition(padX, corridorY));
        }

        return new WorldScreen(
            screenIndex: screenIndex,
            biome: biome,
            width: w,
            height: h,
            tiles: tiles,
            westEntry: westEntry,
            eastExit: eastExit,
            healPads: healPads,
            shopPads: System.Array.Empty<GridPosition>(),
            gymPads: System.Array.Empty<GridPosition>());
    }

    private static void CarveCorridor(OverworldTile[] tiles, int w, int h, int corridorY, BiomeProfile profile, IRandomSource rng)
    {
        // The first/last 3 columns of the corridor are kept pure path so screen transitions
        // never start a battle. The middle stretch sprinkles in a few grass tiles at
        // roughly a quarter of the biome's grass-fill rate — enough that a straight-east
        // walker rolls 1-3 encounters per screen but won't be ambushed every step.
        int grassChance = Math.Max(8, profile.GrassFillPercent / 4);
        for (int x = 1; x < w - 1; x++)
        {
            int mainIdx = (corridorY * w) + x;
            bool canPlaceGrass = x > 2 && x < w - 4 && grassChance > 0 && rng.NextInt(100) < grassChance;
            tiles[mainIdx] = canPlaceGrass ? OverworldTile.Grass : OverworldTile.Path;

            // Buffer rows: clear walls so the corridor doesn't feel hemmed in, but keep grass
            // and water as decoration.
            for (int dy = -1; dy <= 1; dy += 2)
            {
                int py = corridorY + dy;
                if (py <= 0 || py >= h - 1) continue;
                int bufIdx = (py * w) + x;
                if (tiles[bufIdx] == OverworldTile.Wall) tiles[bufIdx] = OverworldTile.Path;
            }
        }
    }

    // ---- Town screen (gyms) --------------------------------------------------------------

    private static WorldScreen GenerateTown(int screenIndex, IRandomSource rng)
    {
        int w = ScreenWidth;
        int h = ScreenHeight;
        OverworldTile[] tiles = new OverworldTile[w * h];

        // Fill: walls on border, path everywhere inside (towns are open).
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = (y * w) + x;
                bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                tiles[idx] = border ? OverworldTile.Wall : OverworldTile.Path;
            }
        }

        // Edge holes — fixed center rows for towns so the player always lines up.
        int midY = h / 2;
        tiles[(midY * w) + 0] = OverworldTile.Path;
        tiles[(midY * w) + (w - 1)] = OverworldTile.Path;
        GridPosition westEntry = new(0, midY);
        GridPosition eastExit = new(w - 1, midY);

        // Fixed layout:
        //   - Heal pad northwest of center (5, 4)
        //   - Shop pad northeast of center (35, 4)
        //   - Gym pad east of center (32, midY) — gym leader's mat
        // Use the registered position list for whichever pads are present.
        List<GridPosition> healPads = new();
        List<GridPosition> shopPads = new();
        List<GridPosition> gymPads = new();

        Place(tiles, w, 5, 4, OverworldTile.HealPad);
        healPads.Add(new GridPosition(5, 4));

        Place(tiles, w, 34, 4, OverworldTile.ShopPad);
        shopPads.Add(new GridPosition(34, 4));

        Place(tiles, w, 32, midY, OverworldTile.GymPad);
        gymPads.Add(new GridPosition(32, midY));

        WorldScreen screen = new WorldScreen(
            screenIndex: screenIndex,
            biome: BiomeKind.Town,
            width: w,
            height: h,
            tiles: tiles,
            westEntry: westEntry,
            eastExit: eastExit,
            healPads: healPads,
            shopPads: shopPads,
            gymPads: gymPads);
        // Towns lock the east passage until the gym leader is defeated.
        screen.EastGateOpen = false;
        return screen;
    }

    // ---- Champion's Hall (final screen) --------------------------------------------------

    private static WorldScreen GenerateChampionHall(int screenIndex)
    {
        int w = ScreenWidth;
        int h = ScreenHeight;
        OverworldTile[] tiles = new OverworldTile[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = (y * w) + x;
                bool border = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                tiles[idx] = border ? OverworldTile.Wall : OverworldTile.Path;
            }
        }

        int midY = h / 2;
        tiles[(midY * w) + 0] = OverworldTile.Path;
        GridPosition westEntry = new(0, midY);

        // Goal tile at the far east — no east exit, this is the end of the world.
        int goalX = w - 4;
        Place(tiles, w, goalX, midY, OverworldTile.Goal);

        return new WorldScreen(
            screenIndex: screenIndex,
            biome: BiomeKind.Champion,
            width: w,
            height: h,
            tiles: tiles,
            westEntry: westEntry,
            eastExit: new GridPosition(goalX, midY),
            healPads: System.Array.Empty<GridPosition>(),
            shopPads: System.Array.Empty<GridPosition>(),
            gymPads: System.Array.Empty<GridPosition>());
    }

    // ---- Helpers --------------------------------------------------------------------------

    private static void Place(OverworldTile[] tiles, int width, int x, int y, OverworldTile tile)
    {
        tiles[(y * width) + x] = tile;
    }

    private static (int x, int y) FindNearestTile(OverworldTile[] tiles, int width, int height, int startX, int startY, OverworldTile target)
    {
        for (int radius = 0; radius < Math.Max(width, height); radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                    int x = startX + dx;
                    int y = startY + dy;
                    if (x < 0 || y < 0 || x >= width || y >= height) continue;
                    if (tiles[(y * width) + x] == target) return (x, y);
                }
            }
        }

        return (startX, startY);
    }
}
