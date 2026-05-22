using System.Collections.Generic;
using Moonforge.Core.Exploration;

namespace Moonforge.Sample.MonsterCatcher.WorldGen;

/// <summary>
/// Tile kinds that can appear on a screen. The engine's <see cref="ExplorationTileFlags"/>
/// tracks walkability and encounter eligibility; the sample carries this parallel enum
/// to drive rendering and tile-effect dispatch.
/// </summary>
internal enum OverworldTile
{
    Wall = 0,          // tree / rock / impassable border
    Path = 1,          // ordinary walkable ground
    Grass = 2,         // tall grass — random encounter chance per step
    Water = 3,         // decorative impassable
    HealPad = 4,       // heals party + restores PP (free, infinite uses)
    ShopPad = 5,       // opens the town shop (towns only)
    GymPad = 6,        // initiates the gym leader trainer battle (towns only)
    Goal = 7           // final tile — Champion's Hall ending tile
}

/// <summary>
/// One screen of the world. Each screen is a self-contained <see cref="Width"/>×<see cref="Height"/>
/// grid generated deterministically from (worldSeed, screenIndex). Screens are cached on
/// first visit so backtracking shows the same layout and remembers any per-screen mutable
/// state (which heal pads have already been rested on, whether the gym has been beaten).
/// </summary>
internal sealed class WorldScreen
{
    public WorldScreen(
        int screenIndex,
        BiomeKind biome,
        int width,
        int height,
        OverworldTile[] tiles,
        GridPosition westEntry,
        GridPosition eastExit,
        IReadOnlyList<GridPosition> healPads,
        IReadOnlyList<GridPosition> shopPads,
        IReadOnlyList<GridPosition> gymPads)
    {
        ScreenIndex = screenIndex;
        Biome = biome;
        Width = width;
        Height = height;
        Tiles = tiles;
        WestEntry = westEntry;
        EastExit = eastExit;
        HealPads = healPads;
        ShopPads = shopPads;
        GymPads = gymPads;
    }

    public int ScreenIndex { get; }

    public BiomeKind Biome { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Row-major tile grid, length = <see cref="Width"/> × <see cref="Height"/>.</summary>
    public OverworldTile[] Tiles { get; }

    /// <summary>Path tile on the west border (player spawns here when entering from the west).</summary>
    public GridPosition WestEntry { get; }

    /// <summary>Path tile on the east border (player exits this screen here, enters next one's west entry).</summary>
    public GridPosition EastExit { get; }

    public IReadOnlyList<GridPosition> HealPads { get; }

    public IReadOnlyList<GridPosition> ShopPads { get; }

    public IReadOnlyList<GridPosition> GymPads { get; }

    // ---- Per-screen mutable state -----------------------------------------------------

    /// <summary>Tile indices of heal pads the player has already used this screen-instance.</summary>
    public HashSet<int> RestedAtPads { get; } = new();

    /// <summary>
    /// True when the east edge is locked (gym screens lock the east passage until the
    /// gym leader is defeated). Phase 2 wires the unlock; phase 1 leaves this true for
    /// every screen.
    /// </summary>
    public bool EastGateOpen { get; set; } = true;

    /// <summary>True if the gym on this screen has been cleared (only meaningful for Town screens).</summary>
    public bool GymCleared { get; set; }

    // ---- Queries ----------------------------------------------------------------------

    public OverworldTile TileAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return OverworldTile.Wall;
        return Tiles[(y * Width) + x];
    }

    public bool IsWalkable(int x, int y)
    {
        OverworldTile tile = TileAt(x, y);
        return tile != OverworldTile.Wall && tile != OverworldTile.Water;
    }

    public int IndexOf(int x, int y) => (y * Width) + x;

    /// <summary>
    /// Translates the screen's tile palette into engine <see cref="ExplorationTileFlags"/>
    /// for <see cref="Moonforge.Core.Exploration.Commands.ConfigureExplorationMapCommand"/>.
    /// Reconfigured on every screen transition.
    /// </summary>
    public ExplorationTileFlags[] ToEngineFlags()
    {
        ExplorationTileFlags[] flags = new ExplorationTileFlags[Tiles.Length];
        for (int i = 0; i < Tiles.Length; i++)
        {
            flags[i] = Tiles[i] switch
            {
                OverworldTile.Wall => ExplorationTileFlags.BlocksLineOfSight,
                OverworldTile.Water => ExplorationTileFlags.BlocksLineOfSight,
                OverworldTile.Grass => ExplorationTileFlags.Walkable | ExplorationTileFlags.EncounterAllowed,
                OverworldTile.Path => ExplorationTileFlags.Walkable,
                OverworldTile.HealPad => ExplorationTileFlags.Walkable | ExplorationTileFlags.Interactable,
                OverworldTile.ShopPad => ExplorationTileFlags.Walkable | ExplorationTileFlags.Interactable,
                OverworldTile.GymPad => ExplorationTileFlags.Walkable | ExplorationTileFlags.Interactable,
                OverworldTile.Goal => ExplorationTileFlags.Walkable | ExplorationTileFlags.Interactable,
                _ => ExplorationTileFlags.BlocksLineOfSight
            };
        }

        return flags;
    }
}
