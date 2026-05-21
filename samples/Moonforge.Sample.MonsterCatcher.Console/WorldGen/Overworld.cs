using System.Collections.Generic;
using Moonforge.Core.Exploration;

namespace Moonforge.Sample.MonsterCatcher.WorldGen;

/// <summary>
/// Tile kinds the overworld uses. The engine's <see cref="ExplorationTileFlags"/> tracks
/// walkability and encounter eligibility; the sample carries this parallel enum to drive
/// rendering and tile-effect dispatch (PokeCenter heal, goal tile, etc.).
/// </summary>
internal enum OverworldTile
{
    Wall = 0,         // tree / cliff / impassable border
    Path = 1,         // ordinary walkable ground
    Grass = 2,        // tall grass — random encounter chance per step
    Water = 3,        // decorative impassable
    PokeCenter = 4,   // stepping on heals party + restores PP
    Goal = 5          // stepping on ends the game (victory)
}

/// <summary>
/// Static, immutable description of the world's grid. Built once by
/// <see cref="OverworldGenerator"/> and read every frame. Per-instance player state lives
/// in the engine's <see cref="ExplorationActorState"/>; this object holds only the layout.
/// </summary>
internal sealed class Overworld
{
    public Overworld(
        int width,
        int height,
        OverworldTile[] tiles,
        int[] zoneIds,
        GridPosition spawn,
        GridPosition goal,
        IReadOnlyList<GridPosition> pokeCenters,
        int zoneCount)
    {
        Width = width;
        Height = height;
        Tiles = tiles;
        ZoneIds = zoneIds;
        Spawn = spawn;
        Goal = goal;
        PokeCenters = pokeCenters;
        ZoneCount = zoneCount;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Row-major tile grid, length = Width * Height.</summary>
    public OverworldTile[] Tiles { get; }

    /// <summary>Parallel array — which zone (1..<see cref="ZoneCount"/>) each tile belongs to. 0 = no zone (borders).</summary>
    public int[] ZoneIds { get; }

    public GridPosition Spawn { get; }

    public GridPosition Goal { get; }

    public IReadOnlyList<GridPosition> PokeCenters { get; }

    public int ZoneCount { get; }

    public OverworldTile TileAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return OverworldTile.Wall;
        return Tiles[(y * Width) + x];
    }

    public int ZoneAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        return ZoneIds[(y * Width) + x];
    }

    public bool IsWalkable(int x, int y)
    {
        OverworldTile tile = TileAt(x, y);
        return tile != OverworldTile.Wall && tile != OverworldTile.Water;
    }
}
