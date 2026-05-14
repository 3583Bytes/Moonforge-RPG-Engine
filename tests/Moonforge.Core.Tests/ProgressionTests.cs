using System.Collections.Generic;
using System.Linq;
using Moonforge.Core;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Progression;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Progression.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class ProgressionTests
{
    private const string Hero = "party.hero";
    private const string Slime = "enemy.slime";
    private const string Curve = "curve.linear";

    [Fact]
    public void Grant_Xp_Below_Threshold_Does_Not_Level()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve), CreateContext(sink, defs)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Hero, 5), CreateContext(sink, defs)).IsSuccess);

        Assert.True(gameState.ProgressionState.TryGet(Hero, out ActorProgression progression));
        Assert.Equal(1, progression.Level);
        Assert.Equal(5, progression.Xp);
        Assert.DoesNotContain(sink.Events, e => e is LevelUpEvent);
    }

    [Fact]
    public void Grant_Xp_Crossing_One_Threshold_Fires_LevelUp()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve), CreateContext(sink, defs)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Hero, 15), CreateContext(sink, defs)).IsSuccess);

        Assert.True(gameState.ProgressionState.TryGet(Hero, out ActorProgression progression));
        Assert.Equal(2, progression.Level);
        Assert.Single(sink.Events.OfType<LevelUpEvent>(), e => e.ActorId == Hero && e.ToLevel == 2);
    }

    [Fact]
    public void Grant_Xp_Crossing_Many_Thresholds_Fires_Multiple_LevelUps()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve), CreateContext(sink, defs)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new GrantExperienceCommand(Hero, 100), CreateContext(sink, defs)).IsSuccess);

        Assert.True(gameState.ProgressionState.TryGet(Hero, out ActorProgression progression));
        // Thresholds [10, 30, 60, 100]; 100 XP exactly hits the level-5 boundary
        Assert.Equal(5, progression.Level);
        int levelUpEvents = sink.Events.OfType<LevelUpEvent>().Count();
        Assert.Equal(4, levelUpEvents);
    }

    [Fact]
    public void Unknown_Curve_Fails_Validation()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        DomainResult result = dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, "curve.unknown"), CreateContext(sink, defs));
        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public void Battle_Victory_Grants_Party_Xp_From_Enemy_Reward()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve), CreateContext(sink, defs)).IsSuccess);

        BattleActorDefinition hero = new(
            actorId: Hero,
            displayName: "Hero",
            faction: CombatFaction.Party,
            maxHp: 30,
            atk: 20,
            def: 0,
            matk: 0,
            mdef: 0,
            initiative: 99,
            skillIds: ["skill.attack"],
            playerControlled: true);
        BattleActorDefinition slime = new(
            actorId: Slime,
            displayName: "Slime",
            faction: CombatFaction.Enemy,
            maxHp: 5,
            atk: 1,
            def: 0,
            matk: 0,
            mdef: 0,
            initiative: 1,
            skillIds: ["skill.attack"],
            xpReward: 18);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand("battle.xp", [hero, slime], [new BattleSkillDefinition("skill.attack", BattleSkillEffectType.PhysicalDamage, power: 10)], seed: 1),
            CreateContext(sink, defs)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, "skill.attack", Slime), CreateContext(sink, defs)).IsSuccess);

        Assert.True(gameState.ProgressionState.TryGet(Hero, out ActorProgression progression));
        Assert.Equal(18, progression.Xp);
        Assert.Equal(2, progression.Level); // 18 XP crosses the 10 threshold
    }

    [Fact]
    public void Progression_Round_Trips_Through_Persistence_Snapshot()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = CreateWorld();
        Assert.True(dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(Hero, Curve, level: 3, xp: 42), CreateContext(sink, defs)).IsSuccess);

        GameStateSnapshot snapshot = GameStateSnapshotMapper.Capture(gameState);
        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(snapshot);
        GameStateSnapshot decoded = serializer.Deserialize(json);

        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.True(rebuilt.ProgressionState.TryGet(Hero, out ActorProgression progression));
        Assert.Equal(Curve, progression.CurveId);
        Assert.Equal(3, progression.Level);
        Assert.Equal(42, progression.Xp);
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) CreateWorld()
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog defs = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddExperienceCurve(new ExperienceCurveDefinition(
                id: Curve,
                xpThresholds: new long[] { 10, 30, 60, 100 },
                displayName: "Linear Curve"));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
        return (gameState, dispatcher, defs, sink);
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }
}
