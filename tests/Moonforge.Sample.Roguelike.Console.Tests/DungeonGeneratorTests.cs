using Moonforge.Core.Exploration;
using Moonforge.Core.Runtime.Random;
using Moonforge.Sample.Roguelike.WorldGen;

namespace Moonforge.Sample.Roguelike.Console.Tests;

public sealed class DungeonGeneratorTests
{
    [Fact]
    public void Generate_SameSeedAndDepth_ProducesEquivalentFloor()
    {
        Pcg32RandomSource randomA = new(seed: 4444, sequence: 54);
        Pcg32RandomSource randomB = new(seed: 4444, sequence: 54);

        DungeonFloorBlueprint first = DungeonGenerator.Generate(randomA, depth: 4);
        DungeonFloorBlueprint second = DungeonGenerator.Generate(randomB, depth: 4);

        Assert.Equal(first.Width, second.Width);
        Assert.Equal(first.Height, second.Height);
        Assert.Equal(first.Spawn, second.Spawn);
        Assert.Equal(first.Stairs, second.Stairs);
        Assert.Equal(first.Tiles.Count, second.Tiles.Count);
        for (int i = 0; i < first.Tiles.Count; i++)
        {
            Assert.Equal(first.Tiles[i], second.Tiles[i]);
        }
    }

    [Fact]
    public void Generate_DifferentSeeds_ProducesDifferentFloorLayout()
    {
        Pcg32RandomSource randomA = new(seed: 4444, sequence: 54);
        Pcg32RandomSource randomB = new(seed: 987654321, sequence: 54);

        DungeonFloorBlueprint first = DungeonGenerator.Generate(randomA, depth: 4);
        DungeonFloorBlueprint second = DungeonGenerator.Generate(randomB, depth: 4);

        bool sameSpawn = first.Spawn.Equals(second.Spawn);
        bool sameStairs = first.Stairs.Equals(second.Stairs);
        bool sameTiles = first.Tiles.SequenceEqual(second.Tiles);
        Assert.False(sameSpawn && sameStairs && sameTiles);
    }

    [Fact]
    public void Generate_AlwaysProducesReachableSpawnToStairs()
    {
        for (int depth = 1; depth <= 8; depth++)
        {
            Pcg32RandomSource random = new(seed: (ulong)(1400 + depth), sequence: 54);
            DungeonFloorBlueprint floor = DungeonGenerator.Generate(random, depth);

            Assert.True(IsWalkable(floor, floor.Spawn));
            Assert.True(IsWalkable(floor, floor.Stairs));
            Assert.True(IsReachable(floor, floor.Spawn, floor.Stairs));
        }
    }

    private static bool IsWalkable(DungeonFloorBlueprint floor, GridPosition position)
    {
        int index = (position.Y * floor.Width) + position.X;
        return (floor.Tiles[index] & ExplorationTileFlags.Walkable) == ExplorationTileFlags.Walkable;
    }

    private static bool IsReachable(DungeonFloorBlueprint floor, GridPosition start, GridPosition target)
    {
        bool[] visited = new bool[floor.Tiles.Count];
        Queue<GridPosition> queue = new();
        queue.Enqueue(start);
        visited[(start.Y * floor.Width) + start.X] = true;

        while (queue.Count > 0)
        {
            GridPosition current = queue.Dequeue();
            if (current.Equals(target))
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
            if (x < 0 || y < 0 || x >= floor.Width || y >= floor.Height)
            {
                return;
            }

            int index = (y * floor.Width) + x;
            if (visited[index])
            {
                return;
            }

            if ((floor.Tiles[index] & ExplorationTileFlags.Walkable) != ExplorationTileFlags.Walkable)
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(new GridPosition(x, y));
        }
    }
}
