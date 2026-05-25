using System;
using System.Collections.Generic;
using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Exploration.Commands;

public sealed class ConfigureExplorationMapCommand : ICommand
{
    public ConfigureExplorationMapCommand(
        string mapId,
        int width,
        int height,
        IReadOnlyList<ExplorationTileFlags> tiles)
    {
        MapId = mapId;
        Width = width;
        Height = height;
        Tiles = tiles is ExplorationTileFlags[] typedArray
            ? typedArray
            : (tiles is null ? [] : new List<ExplorationTileFlags>(tiles).ToArray());
    }

    public string MapId { get; }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<ExplorationTileFlags> Tiles { get; }
}
