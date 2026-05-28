using Moonforge.Core.Exploration;
using Moonforge.Core.Runtime.Random;

using System;
using System.Collections.Generic;
using System.Linq;
namespace Moonforge.Sample.Roguelike.WorldGen
{

    internal static class DungeonGenerator
    {
        public static DungeonFloorBlueprint Generate(IRandomSource random, int depth)
        {
            const int width = 64;
            const int height = 26;
            const int maxAttempts = 8;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ExplorationTileFlags[] tiles = new ExplorationTileFlags[width * height];
                Array.Fill(tiles, ExplorationTileFlags.BlocksLineOfSight);
                List<Room> rooms = BuildRooms(random, depth, width, height, tiles);
                if (rooms.Count < 2)
                {
                    continue;
                }

                ConnectRooms(rooms, tiles, width, height);

                GridPosition spawn = rooms[0].Center;
                Room stairsRoom = rooms
                    .OrderByDescending(x => ManhattanDistance(spawn, x.Center))
                    .ThenBy(x => x.X)
                    .ThenBy(x => x.Y)
                    .First();
                GridPosition stairs = stairsRoom.Center;

                if (!IsReachable(spawn, stairs, tiles, width, height))
                {
                    continue;
                }

                List<GridPosition> pillars = DecorateLargeRoomsWithPillars(rooms, tiles, width, random, spawn, stairs);

                int stairsIndex = ToIndex(stairs.X, stairs.Y, width);
                tiles[stairsIndex] = ExplorationTileFlags.Walkable | ExplorationTileFlags.EncounterAllowed | ExplorationTileFlags.Interactable;
                return new DungeonFloorBlueprint(width, height, tiles, spawn, stairs, pillars);
            }

            return BuildFallback(width, height);
        }

        private static int ToIndex(int x, int y, int width)
        {
            return (y * width) + x;
        }

        private static List<Room> BuildRooms(
            IRandomSource random,
            int depth,
            int width,
            int height,
            ExplorationTileFlags[] tiles)
        {
            // Deeper floors have more, larger rooms — the dungeon grows along with the run.
            int desiredRooms = Math.Clamp(12 + random.NextInt(5) + (depth / 2), 12, 22);
            List<Room> rooms = new(desiredRooms);

            for (int i = 0; i < desiredRooms; i++)
            {
                bool placed = false;
                for (int tryIndex = 0; tryIndex < 48; tryIndex++)
                {
                    // Occasionally roll a "great hall" — larger room that anchors a region.
                    bool greatHall = random.NextInt(100) < 18;
                    int roomWidth = greatHall
                        ? 9 + random.NextInt(5)
                        : 4 + random.NextInt(6);
                    int roomHeight = greatHall
                        ? 6 + random.NextInt(3)
                        : 3 + random.NextInt(4);
                    int x = 1 + random.NextInt(Math.Max(1, width - roomWidth - 2));
                    int y = 1 + random.NextInt(Math.Max(1, height - roomHeight - 2));
                    Room candidate = new(x, y, roomWidth, roomHeight);

                    bool overlaps = rooms.Any(existing => existing.OverlapsWithMargin(candidate, margin: 1));
                    if (overlaps)
                    {
                        continue;
                    }

                    CarveRoom(candidate, tiles, width);
                    rooms.Add(candidate);
                    placed = true;
                    break;
                }

                if (!placed && rooms.Count >= 8)
                {
                    break;
                }
            }

            return rooms;
        }

        private static void ConnectRooms(
            IReadOnlyList<Room> rooms,
            ExplorationTileFlags[] tiles,
            int width,
            int height)
        {
            Room current = rooms[0];
            for (int i = 1; i < rooms.Count; i++)
            {
                Room next = rooms[i];
                CarveCorridor(current.Center, next.Center, tiles, width, height);
                current = next;
            }
        }

        private static void CarveRoom(Room room, ExplorationTileFlags[] tiles, int width)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    int index = ToIndex(x, y, width);
                    tiles[index] = ExplorationTileFlags.Walkable | ExplorationTileFlags.EncounterAllowed;
                }
            }
        }

        private static void CarveCorridor(
            GridPosition from,
            GridPosition to,
            ExplorationTileFlags[] tiles,
            int width,
            int height)
        {
            int x = from.X;
            int y = from.Y;
            while (x != to.X)
            {
                SetWalkable(x, y, tiles, width, height);
                x += x < to.X ? 1 : -1;
            }

            while (y != to.Y)
            {
                SetWalkable(x, y, tiles, width, height);
                y += y < to.Y ? 1 : -1;
            }

            SetWalkable(x, y, tiles, width, height);
        }

        private static void SetWalkable(int x, int y, ExplorationTileFlags[] tiles, int width, int height)
        {
            if (x <= 0 || y <= 0 || x >= width - 1 || y >= height - 1)
            {
                return;
            }

            int index = ToIndex(x, y, width);
            tiles[index] = ExplorationTileFlags.Walkable | ExplorationTileFlags.EncounterAllowed;
        }

        private static bool IsReachable(
            GridPosition start,
            GridPosition target,
            ExplorationTileFlags[] tiles,
            int width,
            int height)
        {
            bool[] visited = new bool[tiles.Length];
            Queue<GridPosition> queue = new();
            queue.Enqueue(start);
            visited[ToIndex(start.X, start.Y, width)] = true;

            while (queue.Count > 0)
            {
                GridPosition current = queue.Dequeue();
                if (current.X == target.X && current.Y == target.Y)
                {
                    return true;
                }

                TryEnqueue(current.X + 1, current.Y);
                TryEnqueue(current.X - 1, current.Y);
                TryEnqueue(current.X, current.Y + 1);
                TryEnqueue(current.X, current.Y - 1);
            }

            return false;

            void TryEnqueue(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                int index = ToIndex(x, y, width);
                if (visited[index])
                {
                    return;
                }

                bool walkable = (tiles[index] & ExplorationTileFlags.Walkable) == ExplorationTileFlags.Walkable;
                if (!walkable)
                {
                    return;
                }

                visited[index] = true;
                queue.Enqueue(new GridPosition(x, y));
            }
        }

        /// <summary>
        /// Drops a few non-walkable "pillar" tiles into the interior of rooms big enough to
        /// hold them. Pillars never touch room edges (so wall geometry stays clean) and never
        /// cover the spawn or stairs tile (so the player isn't trapped).
        /// </summary>
        private static List<GridPosition> DecorateLargeRoomsWithPillars(
            IReadOnlyList<Room> rooms,
            ExplorationTileFlags[] tiles,
            int width,
            IRandomSource random,
            GridPosition spawn,
            GridPosition stairs)
        {
            List<GridPosition> pillars = new();
            for (int r = 0; r < rooms.Count; r++)
            {
                Room room = rooms[r];

                // Only large enough rooms get pillars — interior must be at least 5×4.
                if (room.Width < 7 || room.Height < 6)
                {
                    continue;
                }

                int pillarCount = 1 + random.NextInt(2);
                for (int i = 0; i < pillarCount; i++)
                {
                    int px = room.X + 2 + random.NextInt(room.Width - 4);
                    int py = room.Y + 2 + random.NextInt(room.Height - 4);

                    if ((px == spawn.X && py == spawn.Y) || (px == stairs.X && py == stairs.Y))
                    {
                        continue;
                    }

                    int index = ToIndex(px, py, width);
                    if ((tiles[index] & ExplorationTileFlags.Walkable) != ExplorationTileFlags.Walkable)
                    {
                        continue;
                    }

                    tiles[index] = ExplorationTileFlags.BlocksLineOfSight;
                    pillars.Add(new GridPosition(px, py));
                }
            }

            return pillars;
        }

        private static DungeonFloorBlueprint BuildFallback(int width, int height)
        {
            ExplorationTileFlags[] tiles = new ExplorationTileFlags[width * height];
            Array.Fill(tiles, ExplorationTileFlags.BlocksLineOfSight);

            GridPosition spawn = new(3, 3);
            GridPosition stairs = new(width - 4, height - 4);
            for (int y = 2; y < height - 2; y++)
            {
                for (int x = 2; x < width - 2; x++)
                {
                    int index = ToIndex(x, y, width);
                    tiles[index] = ExplorationTileFlags.Walkable | ExplorationTileFlags.EncounterAllowed;
                }
            }

            tiles[ToIndex(stairs.X, stairs.Y, width)] = ExplorationTileFlags.Walkable | ExplorationTileFlags.EncounterAllowed | ExplorationTileFlags.Interactable;
            return new DungeonFloorBlueprint(width, height, tiles, spawn, stairs, Array.Empty<GridPosition>());
        }

        private static int ManhattanDistance(GridPosition a, GridPosition b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }

        private readonly struct Room
        {
            public Room(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Center = new GridPosition(x + (width / 2), y + (height / 2));
            }

            public int X { get; }

            public int Y { get; }

            public int Width { get; }

            public int Height { get; }

            public GridPosition Center { get; }

            public bool OverlapsWithMargin(Room other, int margin)
            {
                int left = X - margin;
                int right = X + Width + margin;
                int top = Y - margin;
                int bottom = Y + Height + margin;

                int otherLeft = other.X;
                int otherRight = other.X + other.Width;
                int otherTop = other.Y;
                int otherBottom = other.Y + other.Height;

                return left < otherRight
                    && right > otherLeft
                    && top < otherBottom
                    && bottom > otherTop;
            }
        }
    }
}
