using Moonforge.Core.Exploration;
using Moonforge.Sample.Roguelike.WorldGen;

namespace Moonforge.Sample.Console.Tests;

public sealed class TownLayoutTests
{
    [Fact]
    public void Build_PlacesAllRequiredLandmarks()
    {
        TownBlueprint blueprint = TownLayout.Build();

        // Every interactable landmark must exist so dialog/shop wiring in the game loop
        // can resolve them by character key.
        foreach (char key in new[] { 'A', 'S', 'H', 'Q', 'G', 'C', 'F', '>' })
        {
            Assert.True(blueprint.Landmarks.ContainsKey(key), $"Missing landmark '{key}'.");
        }
    }

    [Fact]
    public void Build_LandmarkTilesAreWalkableAndInteractable()
    {
        TownBlueprint blueprint = TownLayout.Build();

        foreach (KeyValuePair<char, GridPosition> entry in blueprint.Landmarks)
        {
            int index = (entry.Value.Y * blueprint.Width) + entry.Value.X;
            ExplorationTileFlags flags = blueprint.Tiles[index];
            Assert.True(
                (flags & ExplorationTileFlags.Walkable) == ExplorationTileFlags.Walkable,
                $"Landmark '{entry.Key}' at {entry.Value} is not walkable.");
            Assert.True(
                (flags & ExplorationTileFlags.Interactable) == ExplorationTileFlags.Interactable,
                $"Landmark '{entry.Key}' at {entry.Value} is not interactable.");
        }
    }

    [Fact]
    public void Build_HeroSpawnIsOnOpenFloor()
    {
        TownBlueprint blueprint = TownLayout.Build();
        int index = (blueprint.HeroSpawn.Y * blueprint.Width) + blueprint.HeroSpawn.X;
        ExplorationTileFlags flags = blueprint.Tiles[index];

        Assert.True((flags & ExplorationTileFlags.Walkable) == ExplorationTileFlags.Walkable);
        Assert.False(blueprint.WallDecorations.ContainsKey(blueprint.HeroSpawn));
    }

    [Fact]
    public void Build_TownIsLargerThanLegacyLayout()
    {
        // Sanity check that the visual upgrade actually happened — the legacy town was 30x9.
        TownBlueprint blueprint = TownLayout.Build();
        Assert.True(blueprint.Width >= 40);
        Assert.True(blueprint.Height >= 15);
    }
}
