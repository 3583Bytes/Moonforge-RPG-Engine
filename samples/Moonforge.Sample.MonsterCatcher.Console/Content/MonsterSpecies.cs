using System.Collections.Generic;

namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// One species' template. The sample treats <see cref="Id"/> as both the species tag (for
/// the bestiary + evolution events) and a content lookup key. Per-instance state (current
/// HP, level, PP) lives in engine modules under each actor's individual id.
/// </summary>
internal sealed class MonsterSpecies
{
    public MonsterSpecies(
        string id,
        string displayName,
        IReadOnlyList<string> typeIds,
        int baseMaxHp,
        int baseAtk,
        int baseDef,
        int baseMatk,
        int baseMdef,
        int baseInitiative,
        IReadOnlyList<string> startingMoves,
        int captureBaseRate,
        string? evolvesIntoId = null,
        int evolvesAtLevel = 0,
        IReadOnlyDictionary<int, string>? learnedMoves = null)
    {
        Id = id;
        DisplayName = displayName;
        TypeIds = typeIds;
        BaseMaxHp = baseMaxHp;
        BaseAtk = baseAtk;
        BaseDef = baseDef;
        BaseMatk = baseMatk;
        BaseMdef = baseMdef;
        BaseInitiative = baseInitiative;
        StartingMoves = startingMoves;
        CaptureBaseRate = captureBaseRate;
        EvolvesIntoId = evolvesIntoId;
        EvolvesAtLevel = evolvesAtLevel;
        LearnedMoves = learnedMoves ?? new Dictionary<int, string>();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public IReadOnlyList<string> TypeIds { get; }

    public int BaseMaxHp { get; }

    public int BaseAtk { get; }

    public int BaseDef { get; }

    public int BaseMatk { get; }

    public int BaseMdef { get; }

    public int BaseInitiative { get; }

    public IReadOnlyList<string> StartingMoves { get; }

    public int CaptureBaseRate { get; }

    public string? EvolvesIntoId { get; }

    public int EvolvesAtLevel { get; }

    /// <summary>Map of level → moveId learned on reaching that level. Sample handles this in
    /// the level-up reactor; the engine has no built-in concept of a learnset.</summary>
    public IReadOnlyDictionary<int, string> LearnedMoves { get; }
}

internal static class SpeciesIds
{
    // Starters (player choice).
    public const string Emberkin = "species.emberkin";
    public const string Splashling = "species.splashling";
    public const string Sproutling = "species.sproutling";

    // Evolved starters.
    public const string Cinderfox = "species.cinderfox";
    public const string Hydrofin = "species.hydrofin";
    public const string Leafwing = "species.leafwing";

    // Wilds.
    public const string Sparkmite = "species.sparkmite";
    public const string Pebblet = "species.pebblet";
    public const string Mudling = "species.mudling";
    public const string Wisplet = "species.wisplet";
    public const string Featherling = "species.featherling";
    public const string Bubbleling = "species.bubbleling";
    public const string Charpup = "species.charpup";
}

internal static class MonsterRoster
{
    public static IReadOnlyDictionary<string, MonsterSpecies> All { get; } = new Dictionary<string, MonsterSpecies>
    {
        // ---- Starters (level 5 picks for the player) ----
        [SpeciesIds.Emberkin] = new(
            id: SpeciesIds.Emberkin, displayName: "Emberkin",
            typeIds: new[] { TypeIds.Fire },
            baseMaxHp: 32, baseAtk: 11, baseDef: 9, baseMatk: 14, baseMdef: 10, baseInitiative: 14,
            // Ember first so the default "first move" pick (used by the headless smoke test
            // and as the natural first-listed action) is the type-flavored move, not a weak
            // Normal attack.
            startingMoves: new[] { Moves.Ember, Moves.Scratch },
            captureBaseRate: 0, // can't capture your own starter mid-fight in this sample
            evolvesIntoId: SpeciesIds.Cinderfox, evolvesAtLevel: 8,
            learnedMoves: new Dictionary<int, string> { [6] = Moves.Bite }),

        [SpeciesIds.Splashling] = new(
            id: SpeciesIds.Splashling, displayName: "Splashling",
            typeIds: new[] { TypeIds.Water },
            baseMaxHp: 36, baseAtk: 9, baseDef: 11, baseMatk: 13, baseMdef: 12, baseInitiative: 11,
            startingMoves: new[] { Moves.WaterGun, Moves.Tackle },
            captureBaseRate: 0,
            evolvesIntoId: SpeciesIds.Hydrofin, evolvesAtLevel: 8,
            learnedMoves: new Dictionary<int, string> { [6] = Moves.MudSlap }),

        [SpeciesIds.Sproutling] = new(
            id: SpeciesIds.Sproutling, displayName: "Sproutling",
            typeIds: new[] { TypeIds.Grass },
            baseMaxHp: 34, baseAtk: 10, baseDef: 12, baseMatk: 13, baseMdef: 11, baseInitiative: 10,
            startingMoves: new[] { Moves.VineWhip, Moves.Tackle },
            captureBaseRate: 0,
            evolvesIntoId: SpeciesIds.Leafwing, evolvesAtLevel: 8,
            learnedMoves: new Dictionary<int, string> { [6] = Moves.Gust }),

        // ---- Evolved starters ----
        [SpeciesIds.Cinderfox] = new(
            id: SpeciesIds.Cinderfox, displayName: "Cinderfox",
            typeIds: new[] { TypeIds.Fire, TypeIds.Dark },
            baseMaxHp: 52, baseAtk: 18, baseDef: 13, baseMatk: 22, baseMdef: 15, baseInitiative: 20,
            startingMoves: new[] { Moves.Scratch, Moves.Ember, Moves.Bite },
            captureBaseRate: 0),

        [SpeciesIds.Hydrofin] = new(
            id: SpeciesIds.Hydrofin, displayName: "Hydrofin",
            typeIds: new[] { TypeIds.Water },
            baseMaxHp: 58, baseAtk: 15, baseDef: 17, baseMatk: 21, baseMdef: 18, baseInitiative: 16,
            startingMoves: new[] { Moves.Tackle, Moves.WaterGun, Moves.MudSlap },
            captureBaseRate: 0),

        [SpeciesIds.Leafwing] = new(
            id: SpeciesIds.Leafwing, displayName: "Leafwing",
            typeIds: new[] { TypeIds.Grass, TypeIds.Flying },
            baseMaxHp: 54, baseAtk: 16, baseDef: 16, baseMatk: 21, baseMdef: 17, baseInitiative: 19,
            startingMoves: new[] { Moves.Tackle, Moves.VineWhip, Moves.Gust },
            captureBaseRate: 0),

        // ---- Wild monsters ----
        [SpeciesIds.Sparkmite] = new(
            id: SpeciesIds.Sparkmite, displayName: "Sparkmite",
            typeIds: new[] { TypeIds.Electric },
            baseMaxHp: 28, baseAtk: 9, baseDef: 8, baseMatk: 13, baseMdef: 8, baseInitiative: 16,
            startingMoves: new[] { Moves.Tackle, Moves.Spark },
            captureBaseRate: 50),

        [SpeciesIds.Pebblet] = new(
            id: SpeciesIds.Pebblet, displayName: "Pebblet",
            typeIds: new[] { TypeIds.Rock },
            baseMaxHp: 36, baseAtk: 12, baseDef: 14, baseMatk: 6, baseMdef: 9, baseInitiative: 7,
            startingMoves: new[] { Moves.Tackle, Moves.RockThrow },
            captureBaseRate: 45),

        [SpeciesIds.Mudling] = new(
            id: SpeciesIds.Mudling, displayName: "Mudling",
            typeIds: new[] { TypeIds.Ground },
            baseMaxHp: 34, baseAtk: 11, baseDef: 11, baseMatk: 10, baseMdef: 9, baseInitiative: 9,
            startingMoves: new[] { Moves.Tackle, Moves.MudSlap },
            captureBaseRate: 50),

        [SpeciesIds.Wisplet] = new(
            id: SpeciesIds.Wisplet, displayName: "Wisplet",
            typeIds: new[] { TypeIds.Ghost },
            baseMaxHp: 26, baseAtk: 8, baseDef: 8, baseMatk: 15, baseMdef: 12, baseInitiative: 15,
            startingMoves: new[] { Moves.Lick, Moves.Bite },
            captureBaseRate: 30),

        [SpeciesIds.Featherling] = new(
            id: SpeciesIds.Featherling, displayName: "Featherling",
            typeIds: new[] { TypeIds.Flying },
            baseMaxHp: 28, baseAtk: 10, baseDef: 9, baseMatk: 11, baseMdef: 9, baseInitiative: 18,
            startingMoves: new[] { Moves.Tackle, Moves.Gust },
            captureBaseRate: 45),

        [SpeciesIds.Bubbleling] = new(
            id: SpeciesIds.Bubbleling, displayName: "Bubbleling",
            typeIds: new[] { TypeIds.Water },
            baseMaxHp: 30, baseAtk: 9, baseDef: 10, baseMatk: 12, baseMdef: 11, baseInitiative: 12,
            startingMoves: new[] { Moves.Tackle, Moves.WaterGun },
            captureBaseRate: 50),

        [SpeciesIds.Charpup] = new(
            id: SpeciesIds.Charpup, displayName: "Charpup",
            typeIds: new[] { TypeIds.Fire },
            baseMaxHp: 30, baseAtk: 11, baseDef: 9, baseMatk: 13, baseMdef: 9, baseInitiative: 13,
            startingMoves: new[] { Moves.Scratch, Moves.Ember },
            captureBaseRate: 45)
    };

    public static MonsterSpecies Get(string id) => All[id];
}
