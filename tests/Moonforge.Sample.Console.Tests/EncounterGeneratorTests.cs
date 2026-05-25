using Moonforge.Core.Combat;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Random;
using Moonforge.Sample.Roguelike.WorldGen;

namespace Moonforge.Sample.Console.Tests;

public sealed class EncounterGeneratorTests
{
    private static InMemoryGameDefinitionCatalog BuildCatalog()
    {
        InMemoryGameDefinitionCatalog catalog = new();
        EncounterGenerator.RegisterEncounterTables(catalog);
        return catalog;
    }

    [Fact]
    public void Generate_AlwaysIncludesHeroAndAtLeastOneEnemy()
    {
        EncounterBlueprint encounter = EncounterGenerator.Generate(
            depth: 3,
            battleSequence: 1,
            random: new Pcg32RandomSource(seed: 77, sequence: 54),
            definitions: BuildCatalog());

        Assert.Contains(encounter.Actors, x => x.ActorId == "party.hero" && x.PlayerControlled);
        Assert.Contains(encounter.Actors, x => x.Faction == CombatFaction.Enemy);
        Assert.NotEmpty(encounter.Skills);
        Assert.NotEmpty(encounter.RewardCurrency);
    }

    [Fact]
    public void Generate_HigherDepthScalesEnemyThreat()
    {
        EncounterBlueprint shallow = EncounterGenerator.Generate(
            depth: 1,
            battleSequence: 1,
            random: new Pcg32RandomSource(seed: 2026, sequence: 54),
            definitions: BuildCatalog());
        EncounterBlueprint deep = EncounterGenerator.Generate(
            depth: 8,
            battleSequence: 1,
            random: new Pcg32RandomSource(seed: 2026, sequence: 54),
            definitions: BuildCatalog());

        int shallowEnemyHp = shallow.Actors.Where(x => x.Faction == CombatFaction.Enemy).Sum(x => x.MaxHp);
        int deepEnemyHp = deep.Actors.Where(x => x.Faction == CombatFaction.Enemy).Sum(x => x.MaxHp);

        Assert.True(deepEnemyHp > shallowEnemyHp);
    }

    [Fact]
    public void Generate_DeepFloorsUseLateThemes()
    {
        for (int i = 0; i < 8; i++)
        {
            EncounterBlueprint deep = EncounterGenerator.Generate(
                depth: 9,
                battleSequence: 100 + i,
                random: new Pcg32RandomSource(seed: (ulong)(4100 + i), sequence: 54),
                definitions: BuildCatalog());

            Assert.Contains(deep.ThemeId, new[] { "inferno", "catacombs" });
        }
    }

    [Fact]
    public void Generate_DeepFloors_CanRollChampionOrElitePacks()
    {
        bool foundSpecial = false;
        for (int i = 0; i < 40; i++)
        {
            EncounterBlueprint deep = EncounterGenerator.Generate(
                depth: 9,
                battleSequence: 300 + i,
                random: new Pcg32RandomSource(seed: (ulong)(9000 + i), sequence: 54),
                definitions: BuildCatalog());

            if (deep.ChampionCount > 0 || deep.EliteCount > 0)
            {
                foundSpecial = true;
                break;
            }
        }

        Assert.True(foundSpecial);
    }

    [Fact]
    public void Generate_ElitePack_HasElitePrefixInEnemyDisplayName()
    {
        bool foundEliteNamedEnemy = false;
        for (int i = 0; i < 100; i++)
        {
            EncounterBlueprint deep = EncounterGenerator.Generate(
                depth: 10,
                battleSequence: 500 + i,
                random: new Pcg32RandomSource(seed: (ulong)(15000 + i), sequence: 54),
                definitions: BuildCatalog());

            if (deep.EliteCount <= 0)
            {
                continue;
            }

            foundEliteNamedEnemy = deep.Actors.Any(x => x.Faction == CombatFaction.Enemy && x.DisplayName.StartsWith("Elite ", StringComparison.Ordinal));
            if (foundEliteNamedEnemy)
            {
                break;
            }
        }

        Assert.True(foundEliteNamedEnemy);
    }

    [Fact]
    public void GenerateBoss_IncludesBossEnemyAndTokenReward()
    {
        EncounterBlueprint boss = EncounterGenerator.GenerateBoss(
            depth: 6,
            battleSequence: 900,
            random: new Pcg32RandomSource(seed: 4242, sequence: 54),
            definitions: BuildCatalog());

        Assert.Contains(boss.Actors, x => x.Faction == CombatFaction.Enemy && x.DisplayName.StartsWith("Boss ", StringComparison.Ordinal));
        Assert.Contains(boss.RewardCurrency, x => x.CurrencyId == "currency.token" && x.Amount > 0);
    }

    [Fact]
    public void GenerateBoss_HigherDepthScalesBossHp()
    {
        EncounterBlueprint shallowBoss = EncounterGenerator.GenerateBoss(
            depth: 3,
            battleSequence: 901,
            random: new Pcg32RandomSource(seed: 5151, sequence: 54),
            definitions: BuildCatalog());
        EncounterBlueprint deepBoss = EncounterGenerator.GenerateBoss(
            depth: 9,
            battleSequence: 901,
            random: new Pcg32RandomSource(seed: 5151, sequence: 54),
            definitions: BuildCatalog());

        int shallowHp = shallowBoss.Actors.Where(x => x.Faction == CombatFaction.Enemy).Sum(x => x.MaxHp);
        int deepHp = deepBoss.Actors.Where(x => x.Faction == CombatFaction.Enemy).Sum(x => x.MaxHp);

        Assert.True(deepHp > shallowHp);
    }

    [Fact]
    public void RegisterEncounterTables_RegistersOneTablePerTheme()
    {
        InMemoryGameDefinitionCatalog catalog = BuildCatalog();
        Assert.True(catalog.TryGetEncounterTable(EncounterGenerator.CryptTableId, out _));
        Assert.True(catalog.TryGetEncounterTable(EncounterGenerator.WarrensTableId, out _));
        Assert.True(catalog.TryGetEncounterTable(EncounterGenerator.CatacombsTableId, out _));
        Assert.True(catalog.TryGetEncounterTable(EncounterGenerator.InfernoTableId, out _));
    }
}
