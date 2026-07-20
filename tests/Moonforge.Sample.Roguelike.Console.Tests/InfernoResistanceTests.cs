using Moonforge.Core.Combat;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Random;
using Moonforge.Sample.Roguelike.WorldGen;

namespace Moonforge.Sample.Roguelike.Console.Tests;

public sealed class InfernoResistanceTests
{
    private static InMemoryGameDefinitionCatalog BuildCatalog()
    {
        InMemoryGameDefinitionCatalog catalog = new();
        EncounterGenerator.RegisterEncounterTables(catalog);
        return catalog;
    }

    [Fact]
    public void InfernoBoss_RegistersFireImmunityAndIceVulnerability()
    {
        // Boss is fixed by depth — at depth 9+ the Inferno boss (Infernal Mastiff) spawns.
        EncounterBlueprint boss = EncounterGenerator.GenerateBoss(
            depth: 9,
            battleSequence: 999,
            random: new Pcg32RandomSource(seed: 1234, sequence: 54),
            definitions: BuildCatalog());

        // The boss has a unique actor ID like "enemy.boss.inferno.999.1".
        BattleActorDefinition bossActor = boss.Actors.Single(a =>
            a.Faction == CombatFaction.Enemy
            && a.DisplayName.StartsWith("Boss ", StringComparison.Ordinal));

        Assert.NotNull(boss.ActorResistances);
        Assert.True(boss.ActorResistances!.TryGetValue(bossActor.ActorId, out IReadOnlyDictionary<string, int>? resistances));
        Assert.NotNull(resistances);
        Assert.Equal(100, resistances!["res.fire"]);
        Assert.Equal(-50, resistances["res.ice"]);
    }

    [Fact]
    public void InfernoEnemies_CarryFireResistance()
    {
        // Roll several deep encounters to maximize odds of seeing an Inferno enemy.
        bool sawInfernoResistance = false;
        for (int seed = 1; seed < 40 && !sawInfernoResistance; seed++)
        {
            EncounterBlueprint encounter = EncounterGenerator.Generate(
                depth: 9,
                battleSequence: seed,
                random: new Pcg32RandomSource(seed: (ulong)seed, sequence: 54),
                definitions: BuildCatalog());

            if (encounter.ThemeId != "inferno" || encounter.ActorResistances is null)
            {
                continue;
            }

            foreach (KeyValuePair<string, IReadOnlyDictionary<string, int>> entry in encounter.ActorResistances)
            {
                if (entry.Value.TryGetValue("res.fire", out int resFire) && resFire >= 50)
                {
                    sawInfernoResistance = true;
                    break;
                }
            }
        }

        Assert.True(sawInfernoResistance, "Expected at least one Inferno enemy with res.fire ≥ 50 across the rolled encounters.");
    }

    [Fact]
    public void NonInfernoEncounter_HasNoActorResistances()
    {
        // Shallow floors stick to Crypt/Warrens which haven't been wired with resistances yet.
        EncounterBlueprint encounter = EncounterGenerator.Generate(
            depth: 1,
            battleSequence: 1,
            random: new Pcg32RandomSource(seed: 42, sequence: 54),
            definitions: BuildCatalog());

        Assert.True(encounter.ActorResistances is null || encounter.ActorResistances.Count == 0);
    }
}
