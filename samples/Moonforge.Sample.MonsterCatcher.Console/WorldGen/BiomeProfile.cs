using System.Collections.Generic;
using Moonforge.Sample.MonsterCatcher.Content;

namespace Moonforge.Sample.MonsterCatcher.WorldGen;

/// <summary>
/// Static description of a biome — what tiles it spawns, how dense, what wild monsters
/// live there, and what encounter chance grass tiles roll at. One instance per
/// <see cref="BiomeKind"/>; looked up via <see cref="BiomeRegistry.Get"/>.
/// </summary>
internal sealed class BiomeProfile
{
    public BiomeProfile(
        BiomeKind kind,
        string displayName,
        string tagline,
        int wallDensityPercent,
        int waterDensityPercent,
        int grassFillPercent,
        int encounterChancePercent,
        IReadOnlyList<string> wildPool)
    {
        Kind = kind;
        DisplayName = displayName;
        Tagline = tagline;
        WallDensityPercent = wallDensityPercent;
        WaterDensityPercent = waterDensityPercent;
        GrassFillPercent = grassFillPercent;
        EncounterChancePercent = encounterChancePercent;
        WildPool = wildPool;
    }

    public BiomeKind Kind { get; }

    public string DisplayName { get; }

    /// <summary>One-line description shown the first time the player enters this biome.</summary>
    public string Tagline { get; }

    /// <summary>% chance an interior non-path tile becomes a wall (tree/rock/etc).</summary>
    public int WallDensityPercent { get; }

    /// <summary>% chance an interior non-path tile becomes water.</summary>
    public int WaterDensityPercent { get; }

    /// <summary>% chance the remaining interior tiles are grass (vs bare path). Higher = more encounters.</summary>
    public int GrassFillPercent { get; }

    /// <summary>Per-grass-tile encounter chance when the player steps on it.</summary>
    public int EncounterChancePercent { get; }

    public IReadOnlyList<string> WildPool { get; }
}

internal static class BiomeRegistry
{
    public static readonly IReadOnlyDictionary<BiomeKind, BiomeProfile> All = new Dictionary<BiomeKind, BiomeProfile>
    {
        [BiomeKind.Plains] = new(
            kind: BiomeKind.Plains,
            displayName: "Plains",
            tagline: "Open rolling fields. Easy walking, common monsters in the grass.",
            wallDensityPercent: 4,
            waterDensityPercent: 0,
            grassFillPercent: 60,
            encounterChancePercent: 14,
            wildPool: new[] { SpeciesIds.Featherling, SpeciesIds.Charpup, SpeciesIds.Sparkmite }),

        [BiomeKind.Forest] = new(
            kind: BiomeKind.Forest,
            displayName: "Forest",
            tagline: "Dense canopy. Trees close in around you and rustles abound.",
            wallDensityPercent: 22,
            waterDensityPercent: 0,
            grassFillPercent: 85,
            encounterChancePercent: 18,
            wildPool: new[] { SpeciesIds.Featherling, SpeciesIds.Charpup, SpeciesIds.Wisplet, SpeciesIds.Sparkmite }),

        [BiomeKind.Cave] = new(
            kind: BiomeKind.Cave,
            displayName: "Cave",
            tagline: "Damp stone and narrow corridors. Eyes glint from the dark.",
            wallDensityPercent: 30,
            waterDensityPercent: 4,
            grassFillPercent: 25,
            encounterChancePercent: 22,
            wildPool: new[] { SpeciesIds.Pebblet, SpeciesIds.Wisplet, SpeciesIds.Sparkmite }),

        [BiomeKind.Highlands] = new(
            kind: BiomeKind.Highlands,
            displayName: "Highlands",
            tagline: "Wind-bitten ridges and tumbled rocks.",
            wallDensityPercent: 18,
            waterDensityPercent: 0,
            grassFillPercent: 45,
            encounterChancePercent: 16,
            wildPool: new[] { SpeciesIds.Pebblet, SpeciesIds.Featherling, SpeciesIds.Charpup }),

        [BiomeKind.Beach] = new(
            kind: BiomeKind.Beach,
            displayName: "Beach",
            tagline: "Soft sand bordered by tidewater.",
            wallDensityPercent: 4,
            waterDensityPercent: 28,
            grassFillPercent: 35,
            encounterChancePercent: 16,
            wildPool: new[] { SpeciesIds.Bubbleling, SpeciesIds.Featherling, SpeciesIds.Sparkmite }),

        [BiomeKind.Marsh] = new(
            kind: BiomeKind.Marsh,
            displayName: "Marsh",
            tagline: "Soggy ground and standing pools. Something splashes nearby.",
            wallDensityPercent: 8,
            waterDensityPercent: 18,
            grassFillPercent: 70,
            encounterChancePercent: 20,
            wildPool: new[] { SpeciesIds.Mudling, SpeciesIds.Bubbleling, SpeciesIds.Wisplet }),

        [BiomeKind.Town] = new(
            kind: BiomeKind.Town,
            displayName: "Town",
            tagline: "A waystation between routes — a healer, a shopkeep, and a gym.",
            wallDensityPercent: 0,
            waterDensityPercent: 0,
            grassFillPercent: 0,
            encounterChancePercent: 0,
            wildPool: System.Array.Empty<string>()),

        [BiomeKind.Champion] = new(
            kind: BiomeKind.Champion,
            displayName: "Champion's Hall",
            tagline: "The road ends here. So does the test.",
            wallDensityPercent: 0,
            waterDensityPercent: 0,
            grassFillPercent: 0,
            encounterChancePercent: 0,
            wildPool: System.Array.Empty<string>())
    };

    public static BiomeProfile Get(BiomeKind kind) => All[kind];
}
