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

namespace Moonforge.Sample.Console.Tests;

public sealed class BossWreathOfFlameTests
{
    [Fact]
    public void Boss_Casts_Wreath_Of_Flame_On_Its_First_Available_Turn()
    {
        InMemoryGameDefinitionCatalog catalog = new();
        EncounterGenerator.RegisterEncounterTables(catalog);
        catalog.AddStatusEffect(new StatusEffectDefinition(
            id: "status.wreath_of_flame",
            durationTurns: 4,
            statModifiers: new Dictionary<string, int> { ["matk"] = 5 },
            displayName: "Wreath of Flame"));

        EncounterBlueprint boss = EncounterGenerator.GenerateBoss(
            depth: 3,
            battleSequence: 1,
            random: new Pcg32RandomSource(seed: 42, sequence: 7),
            definitions: catalog);

        BattleActorDefinition bossActor = boss.Actors.Single(a =>
            a.Faction == CombatFaction.Enemy &&
            a.DisplayName.StartsWith("Boss ", StringComparison.Ordinal));

        Assert.Contains("skill.boss.wreath", bossActor.SkillIds);

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
                battleId: "battle.boss.wreath.test",
                actors: boss.Actors,
                skills: boss.Skills,
                seed: 1,
                sequence: 1),
            context).IsSuccess);

        // Hero outpaces the boss on initiative (hero 20 vs CryptWarden 12 at depth 3),
        // so we burn the hero's first turn before the boss acts.
        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.hero", "skill.attack", bossActor.ActorId),
            context).IsSuccess);

        // Boss's first action — should pick Wreath of Flame.
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), context).IsSuccess);

        StatusAppliedEvent wreathEvent = sink.Events
            .OfType<StatusAppliedEvent>()
            .Single(e => e.StatusId == "status.wreath_of_flame");

        Assert.Equal(bossActor.ActorId, wreathEvent.ActorId);
        Assert.Equal(bossActor.ActorId, wreathEvent.SourceActorId);
        Assert.True(gameState.ActiveBattle!.Actors[bossActor.ActorId].ActiveStatusEffects.ContainsKey("status.wreath_of_flame"));
        Assert.DoesNotContain(sink.Events.OfType<BattleActionResolvedEvent>(),
            e => e.ActorId == bossActor.ActorId && e.SkillId == "skill.boss.wreath");
    }
}
