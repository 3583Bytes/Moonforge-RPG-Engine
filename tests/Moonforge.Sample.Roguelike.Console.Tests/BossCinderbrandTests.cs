using Moonforge.Core;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;
using Moonforge.Sample.Roguelike.WorldGen;

namespace Moonforge.Sample.Roguelike.Console.Tests;

public sealed class BossCinderbrandTests
{
    [Fact]
    public void Boss_Casts_Cinderbrand_On_Its_Second_Available_Turn_After_Wreath()
    {
        InMemoryGameDefinitionCatalog catalog = new();
        EncounterGenerator.RegisterEncounterTables(catalog);
        catalog.AddStatusEffect(new StatusEffectDefinition(
            id: "status.wreath_of_flame",
            durationTurns: 4,
            statModifiers: new Dictionary<string, int> { ["matk"] = 5 },
            displayName: "Wreath of Flame"));
        catalog.AddStatusEffect(new StatusEffectDefinition(
            id: "status.cinderbrand",
            durationTurns: 3,
            statModifiers: new Dictionary<string, int> { ["def"] = -3 },
            displayName: "Cinderbrand"));

        EncounterBlueprint boss = EncounterGenerator.GenerateBoss(
            depth: 3,
            battleSequence: 1,
            random: new Pcg32RandomSource(seed: 42, sequence: 7),
            definitions: catalog);

        BattleActorDefinition bossActor = boss.Actors.Single(a =>
            a.Faction == CombatFaction.Enemy &&
            a.DisplayName.StartsWith("Boss ", StringComparison.Ordinal));

        Assert.Contains("skill.boss.cinderbrand", bossActor.SkillIds);

        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new StartBattleCommandHandler());
        dispatcher.Register(new UseBattleSkillCommandHandler());
        dispatcher.Register(new ExecuteAiTurnCommandHandler());

        CommandContext context = new(
            new Pcg32RandomSource(seed: 11, sequence: 5),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            catalog);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand(
                battleId: "battle.boss.cinder.test",
                actors: boss.Actors,
                skills: boss.Skills,
                seed: 1,
                sequence: 1),
            context).IsSuccess);

        // Hero acts first (higher initiative), boss casts Wreath, hero acts again, boss
        // casts Cinderbrand (Wreath is on cooldown, Cinder is the next-highest priority).
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand("party.hero", "skill.attack", bossActor.ActorId), context).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), context).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand("party.hero", "skill.attack", bossActor.ActorId), context).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), context).IsSuccess);

        StatusAppliedEvent cinderEvent = sink.Events
            .OfType<StatusAppliedEvent>()
            .Single(e => e.StatusId == "status.cinderbrand");

        Assert.Equal("party.hero", cinderEvent.ActorId);
        Assert.Equal(bossActor.ActorId, cinderEvent.SourceActorId);
        Assert.True(gameState.ActiveBattle!.Actors["party.hero"].ActiveStatusEffects.ContainsKey("status.cinderbrand"));
        Assert.DoesNotContain(sink.Events.OfType<BattleActionResolvedEvent>(),
            e => e.ActorId == bossActor.ActorId && e.SkillId == "skill.boss.cinderbrand");
    }
}
