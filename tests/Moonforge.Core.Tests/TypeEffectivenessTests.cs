using System.Collections.Generic;
using System.Linq;
using Moonforge.Core;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class TypeEffectivenessTests
{
    private const string Chart = "chart.elemental";
    private const string FireType = "fire";
    private const string GrassType = "grass";
    private const string WaterType = "water";
    private const string GroundType = "ground";
    private const string ElectricType = "electric";

    [Fact]
    public void Chart_Returns_Neutral_For_Empty_Defender_Types()
    {
        TypeEffectivenessChartDefinition chart = BuildElementalChart();
        Assert.Equal(100, chart.GetMultiplierPercent(FireType, System.Array.Empty<string>()));
        Assert.Equal(100, chart.GetMultiplierPercent(FireType, null));
    }

    [Fact]
    public void Chart_Returns_Neutral_For_Unknown_Matchup()
    {
        TypeEffectivenessChartDefinition chart = BuildElementalChart();
        // Ice attack type isn't in the chart; defender is Grass.
        Assert.Equal(100, chart.GetMultiplierPercent("ice", new[] { GrassType }));
    }

    [Fact]
    public void Chart_Returns_Super_Effective_For_Direct_Matchup()
    {
        TypeEffectivenessChartDefinition chart = BuildElementalChart();
        Assert.Equal(200, chart.GetMultiplierPercent(FireType, new[] { GrassType }));
    }

    [Fact]
    public void Chart_Returns_Resisted_For_Direct_Matchup()
    {
        TypeEffectivenessChartDefinition chart = BuildElementalChart();
        Assert.Equal(50, chart.GetMultiplierPercent(FireType, new[] { WaterType }));
    }

    [Fact]
    public void Chart_Returns_Zero_For_Immunity()
    {
        TypeEffectivenessChartDefinition chart = BuildElementalChart();
        Assert.Equal(0, chart.GetMultiplierPercent(ElectricType, new[] { GroundType }));
    }

    [Fact]
    public void Chart_Stacks_Multiplicatively_For_Dual_Type_Defender()
    {
        TypeEffectivenessChartDefinition chart = BuildElementalChart();
        // Water/Ground vs. Electric: 2× (Water) * 0× (Ground) = 0.
        Assert.Equal(0, chart.GetMultiplierPercent(ElectricType, new[] { WaterType, GroundType }));

        // Grass/Water vs. Fire: 2× (Grass) * 0.5× (Water) = 1× neutral.
        Assert.Equal(100, chart.GetMultiplierPercent(FireType, new[] { GrassType, WaterType }));
    }

    [Fact]
    public void Battle_Damage_Applies_Chart_Multiplier()
    {
        // Fire move (power 10) vs. Grass mon: 2× super-effective. Compare against a Water
        // mon: 0.5× resisted. Same attacker, same skill, only defender type differs.
        int grassDamage = MeasureFireSkillDamage(defenderTypes: new[] { GrassType });
        int waterDamage = MeasureFireSkillDamage(defenderTypes: new[] { WaterType });
        int neutralDamage = MeasureFireSkillDamage(defenderTypes: System.Array.Empty<string>());

        Assert.True(grassDamage > neutralDamage, $"grass={grassDamage} expected > neutral={neutralDamage}");
        Assert.True(waterDamage < neutralDamage, $"water={waterDamage} expected < neutral={neutralDamage}");
        // 2× vs. 0.5× → grass should be roughly 4× the water hit.
        Assert.True(grassDamage >= 3 * waterDamage, $"grass={grassDamage} expected >= 3× water={waterDamage}");
    }

    [Fact]
    public void Battle_Damage_With_No_Chart_Configured_Is_Unaffected_By_Defender_Types()
    {
        // Damage type without a chart id should behave exactly like the legacy path.
        int withTypes = MeasureFireSkillDamage(defenderTypes: new[] { GrassType }, configureChart: false);
        int withoutTypes = MeasureFireSkillDamage(defenderTypes: System.Array.Empty<string>(), configureChart: false);

        Assert.Equal(withTypes, withoutTypes);
    }

    [Fact]
    public void Battle_Damage_Honors_Chart_Immunity()
    {
        // Electric attacker vs. Ground defender: 0× immunity.
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        InMemoryGameDefinitionCatalog defs = BuildCatalog(includeChart: true);

        CommandDispatcher dispatcher = new();
        dispatcher.Register(new StartBattleCommandHandler());
        dispatcher.Register(new UseBattleSkillCommandHandler());

        BattleSkillDefinition shock = new(
            id: "skill.shock",
            effectType: BattleSkillEffectType.MagicalDamage,
            power: 20,
            damageTypeId: ElectricType,
            damageVariancePercent: 0);

        List<BattleActorDefinition> actors =
        [
            new BattleActorDefinition(
                actorId: "party.zap",
                displayName: "Zap",
                faction: CombatFaction.Party,
                maxHp: 40, atk: 8, def: 4, matk: 14, mdef: 5,
                initiative: 20,
                skillIds: ["skill.shock"],
                playerControlled: true,
                defenderTypeIds: new[] { ElectricType }),
            new BattleActorDefinition(
                actorId: "enemy.dirt",
                displayName: "Dirt",
                faction: CombatFaction.Enemy,
                maxHp: 40, atk: 6, def: 4, matk: 2, mdef: 5,
                initiative: 5,
                skillIds: ["skill.shock"],
                playerControlled: false,
                defenderTypeIds: new[] { GroundType })
        ];

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand("battle.imm", actors, new[] { shock }, seed: 1, sequence: 1),
            Ctx(sink, defs)).IsSuccess);

        int hpBefore = gameState.ActiveBattle!.Actors["enemy.dirt"].Hp;
        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.zap", "skill.shock", "enemy.dirt"),
            Ctx(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle!.Actors["enemy.dirt"].Hp;

        Assert.Equal(hpBefore, hpAfter);
        Assert.Contains(sink.Events.OfType<BattleActionResolvedEvent>(),
            e => e.SkillId == "skill.shock" && e.Amount == 0);
    }

    private static int MeasureFireSkillDamage(IReadOnlyList<string> defenderTypes, bool configureChart = true)
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        InMemoryGameDefinitionCatalog defs = BuildCatalog(includeChart: configureChart);

        CommandDispatcher dispatcher = new();
        dispatcher.Register(new StartBattleCommandHandler());
        dispatcher.Register(new UseBattleSkillCommandHandler());

        BattleSkillDefinition fire = new(
            id: "skill.firebolt",
            effectType: BattleSkillEffectType.MagicalDamage,
            power: 10,
            damageTypeId: FireType,
            damageVariancePercent: 0);

        List<BattleActorDefinition> actors =
        [
            new BattleActorDefinition(
                actorId: "party.attacker",
                displayName: "Attacker",
                faction: CombatFaction.Party,
                maxHp: 40, atk: 8, def: 4, matk: 12, mdef: 5,
                initiative: 20,
                skillIds: ["skill.firebolt"],
                playerControlled: true,
                defenderTypeIds: new[] { FireType }),
            new BattleActorDefinition(
                actorId: "enemy.target",
                displayName: "Target",
                faction: CombatFaction.Enemy,
                maxHp: 60, atk: 6, def: 4, matk: 6, mdef: 5,
                initiative: 5,
                skillIds: ["skill.firebolt"],
                playerControlled: false,
                defenderTypeIds: defenderTypes)
        ];

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand("battle.fire", actors, new[] { fire }, seed: 1, sequence: 1),
            Ctx(sink, defs)).IsSuccess);

        int hpBefore = gameState.ActiveBattle!.Actors["enemy.target"].Hp;
        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.attacker", "skill.firebolt", "enemy.target"),
            Ctx(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle!.Actors["enemy.target"].Hp;

        return hpBefore - hpAfter;
    }

    private static InMemoryGameDefinitionCatalog BuildCatalog(bool includeChart)
    {
        InMemoryGameDefinitionCatalog defs = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999));

        defs.AddDamageType(new DamageTypeDefinition(
            id: FireType,
            attackStatId: "matk",
            flatDefenseStatId: "mdef",
            resistanceStatId: "res.fire",
            effectivenessChartId: includeChart ? Chart : null));

        defs.AddDamageType(new DamageTypeDefinition(
            id: ElectricType,
            attackStatId: "matk",
            flatDefenseStatId: "mdef",
            resistanceStatId: "res.electric",
            effectivenessChartId: includeChart ? Chart : null));

        if (includeChart)
        {
            defs.AddTypeEffectivenessChart(BuildElementalChart());
        }

        return defs;
    }

    private static TypeEffectivenessChartDefinition BuildElementalChart()
    {
        return new TypeEffectivenessChartDefinition(
            id: Chart,
            entries: new[]
            {
                new TypeEffectivenessEntry(FireType, GrassType, 200),
                new TypeEffectivenessEntry(FireType, WaterType, 50),
                new TypeEffectivenessEntry(FireType, FireType, 50),
                new TypeEffectivenessEntry(ElectricType, WaterType, 200),
                new TypeEffectivenessEntry(ElectricType, GroundType, 0)
            });
    }

    private static CommandContext Ctx(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 99, sequence: 1),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }
}
