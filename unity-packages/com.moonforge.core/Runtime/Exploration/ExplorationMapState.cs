using System;
using System.Collections.Generic;

namespace Moonforge.Core.Exploration
{

    public sealed class ExplorationMapState
    {
        private readonly List<ExplorationTileFlags> _tiles = new();

        public string MapId { get; private set; } = string.Empty;

        public int Width { get; private set; }

        public int Height { get; private set; }

        public bool IsConfigured => Width > 0 && Height > 0 && _tiles.Count == Width * Height;

        public bool TryConfigure(
            string mapId,
            int width,
            int height,
            IReadOnlyList<ExplorationTileFlags> tiles,
            out string? error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(mapId))
            {
                error = "Map ID is required.";
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                error = "Map dimensions must be positive.";
                return false;
            }

            if (tiles.Count != width * height)
            {
                error = $"Tile count mismatch. Expected={width * height}, actual={tiles.Count}.";
                return false;
            }

            MapId = mapId;
            Width = width;
            Height = height;
            _tiles.Clear();
            _tiles.AddRange(tiles);
            return true;
        }

        public bool IsInBounds(GridPosition position)
        {
            return position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
        }

        public bool TryGetTileFlags(GridPosition position, out ExplorationTileFlags flags)
        {
            if (!IsInBounds(position))
            {
                flags = ExplorationTileFlags.None;
                return false;
            }

            flags = _tiles[GetIndex(position)];
            return true;
        }

        public bool IsWalkable(GridPosition position)
        {
            return TryGetTileFlags(position, out ExplorationTileFlags flags)
                && (flags & ExplorationTileFlags.Walkable) == ExplorationTileFlags.Walkable;
        }

        public void CopyFrom(ExplorationMapState source)
        {
            MapId = source.MapId;
            Width = source.Width;
            Height = source.Height;
            _tiles.Clear();
            _tiles.AddRange(source._tiles);
        }

        private int GetIndex(GridPosition position)
        {
            return position.Y * Width + position.X;
        }
    }
}
