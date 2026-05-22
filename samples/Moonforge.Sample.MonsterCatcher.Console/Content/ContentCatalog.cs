using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Evolution;

namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// Builds the in-memory <see cref="IGameDefinitionCatalog"/> the game runs against. Every
/// engine-visible piece of content the sample needs — damage types, type effectiveness chart,
/// experience curves, evolutions, currency — is registered here.
/// </summary>
internal static class ContentCatalog
{
    public const string ExperienceCurveId = "curve.monster";
    public const string CurrencyGold = "currency.gold";

    public static InMemoryGameDefinitionCatalog Build()
    {
        InMemoryGameDefinitionCatalog defs = new();

        defs.AddCurrency(new CurrencyDefinition(CurrencyGold, 99999));

        // ---- Items + town shop ----
        foreach (Item item in Items.All)
        {
            defs.AddItem(item.EngineDefinition);
        }

        defs.AddShop(TownShop.EngineDefinition);

        // ---- Main quest ----
        defs.AddQuest(MainQuest.EngineDefinition);

        // ---- Damage types (one per move type) ----
        foreach (string typeId in AllTypeIds)
        {
            // Both physical and magical moves can route through the same defender resistance
            // stat — the sample doesn't make the distinction.
            string atkStat = typeId == TypeIds.Normal || typeId == TypeIds.Rock ? "atk" : "matk";
            string defStat = typeId == TypeIds.Normal || typeId == TypeIds.Rock ? "def" : "mdef";
            defs.AddDamageType(new DamageTypeDefinition(
                id: typeId,
                attackStatId: atkStat,
                flatDefenseStatId: defStat,
                resistanceStatId: $"res.{typeId}",
                effectivenessChartId: TypeIds.EffectivenessChart));
        }

        defs.AddTypeEffectivenessChart(BuildTypeChart());

        // ---- Experience curve — single shared curve for every monster ----
        // Threshold N is the XP needed to ENTER level N+1. Roughly accelerating.
        long[] thresholds = new long[24];
        long total = 0;
        for (int i = 0; i < thresholds.Length; i++)
        {
            long levelCost = 10 + (long)(i * i * 1.5);
            total += levelCost;
            thresholds[i] = total;
        }

        defs.AddExperienceCurve(new ExperienceCurveDefinition(
            id: ExperienceCurveId,
            xpThresholds: thresholds,
            displayName: "Standard"));

        // ---- Evolutions ----
        defs.AddEvolution(new EvolutionDefinition(
            id: EvolutionIds.EmberkinToCinderfox,
            trigger: EvolutionTrigger.LevelUp,
            requiredLevel: 8,
            displayName: "Emberkin → Cinderfox",
            evolvedSpeciesId: SpeciesIds.Cinderfox));

        defs.AddEvolution(new EvolutionDefinition(
            id: EvolutionIds.SplashlingToHydrofin,
            trigger: EvolutionTrigger.LevelUp,
            requiredLevel: 8,
            displayName: "Splashling → Hydrofin",
            evolvedSpeciesId: SpeciesIds.Hydrofin));

        defs.AddEvolution(new EvolutionDefinition(
            id: EvolutionIds.SproutlingToLeafwing,
            trigger: EvolutionTrigger.LevelUp,
            requiredLevel: 8,
            displayName: "Sproutling → Leafwing",
            evolvedSpeciesId: SpeciesIds.Leafwing));

        return defs;
    }

    public static IReadOnlyList<string> AllTypeIds { get; } = new[]
    {
        TypeIds.Normal, TypeIds.Fire, TypeIds.Water, TypeIds.Grass, TypeIds.Electric,
        TypeIds.Ground, TypeIds.Rock, TypeIds.Flying, TypeIds.Ghost, TypeIds.Dark
    };

    private static TypeEffectivenessChartDefinition BuildTypeChart()
    {
        // Compact Pokemon-style chart. Values are whole-percent multipliers (200 = 2× super,
        // 50 = 0.5× resisted, 0 = immune). Unlisted matchups default to 100 (neutral).
        List<TypeEffectivenessEntry> entries = new()
        {
            // Fire
            new(TypeIds.Fire, TypeIds.Grass, 200),
            new(TypeIds.Fire, TypeIds.Water, 50),
            new(TypeIds.Fire, TypeIds.Fire, 50),
            new(TypeIds.Fire, TypeIds.Rock, 50),

            // Water
            new(TypeIds.Water, TypeIds.Fire, 200),
            new(TypeIds.Water, TypeIds.Ground, 200),
            new(TypeIds.Water, TypeIds.Rock, 200),
            new(TypeIds.Water, TypeIds.Water, 50),
            new(TypeIds.Water, TypeIds.Grass, 50),

            // Grass
            new(TypeIds.Grass, TypeIds.Water, 200),
            new(TypeIds.Grass, TypeIds.Ground, 200),
            new(TypeIds.Grass, TypeIds.Rock, 200),
            new(TypeIds.Grass, TypeIds.Fire, 50),
            new(TypeIds.Grass, TypeIds.Flying, 50),
            new(TypeIds.Grass, TypeIds.Grass, 50),

            // Electric
            new(TypeIds.Electric, TypeIds.Water, 200),
            new(TypeIds.Electric, TypeIds.Flying, 200),
            new(TypeIds.Electric, TypeIds.Ground, 0),
            new(TypeIds.Electric, TypeIds.Electric, 50),
            new(TypeIds.Electric, TypeIds.Grass, 50),

            // Ground
            new(TypeIds.Ground, TypeIds.Electric, 200),
            new(TypeIds.Ground, TypeIds.Fire, 200),
            new(TypeIds.Ground, TypeIds.Rock, 200),
            new(TypeIds.Ground, TypeIds.Flying, 0),
            new(TypeIds.Ground, TypeIds.Grass, 50),

            // Rock
            new(TypeIds.Rock, TypeIds.Fire, 200),
            new(TypeIds.Rock, TypeIds.Flying, 200),
            new(TypeIds.Rock, TypeIds.Water, 50),
            new(TypeIds.Rock, TypeIds.Grass, 50),
            new(TypeIds.Rock, TypeIds.Ground, 50),

            // Flying
            new(TypeIds.Flying, TypeIds.Grass, 200),
            new(TypeIds.Flying, TypeIds.Electric, 50),
            new(TypeIds.Flying, TypeIds.Rock, 50),

            // Ghost (immune to Normal; super against Ghost)
            new(TypeIds.Ghost, TypeIds.Ghost, 200),
            new(TypeIds.Ghost, TypeIds.Dark, 50),
            new(TypeIds.Ghost, TypeIds.Normal, 0),

            // Dark (super against Ghost)
            new(TypeIds.Dark, TypeIds.Ghost, 200),
            new(TypeIds.Dark, TypeIds.Dark, 50),

            // Normal (resisted by Rock, no effect on Ghost)
            new(TypeIds.Normal, TypeIds.Rock, 50),
            new(TypeIds.Normal, TypeIds.Ghost, 0)
        };

        return new TypeEffectivenessChartDefinition(TypeIds.EffectivenessChart, entries, "Elemental");
    }
}
