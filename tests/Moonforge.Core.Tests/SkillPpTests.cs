using System.Collections.Generic;
using System.Linq;
using Moonforge.Core;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Party.Commands;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class SkillPpTests
{
    private const string Hero = "party.hero";
    private const string Wild = "enemy.wild";
    private const string Tackle = "skill.tackle";
    private const string Ember = "skill.ember";

    [Fact]
    public void Skill_With_MaxPp_Decrements_On_Use_And_Blocks_When_Empty()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(emberPp: 2);

        // Two uses of Ember succeed, the third fails for "out of PP".
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Ember, Wild), Ctx(sink)).IsSuccess);
        // Enemy turn between hero turns.
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Ember, Wild), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), Ctx(sink)).IsSuccess);

        Assert.Equal(0, gameState.ActiveBattle!.Actors[Hero].SkillPp[Ember]);

        DomainResult third = dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Ember, Wild), Ctx(sink));
        Assert.False(third.IsSuccess);
        Assert.Equal(DomainErrorCode.InsufficientResources, third.Error!.Code);
        Assert.Single(sink.Events.OfType<SkillPpDepletedEvent>(), e => e.SkillId == Ember);
    }

    [Fact]
    public void Skill_With_Zero_MaxPp_Is_Unlimited()
    {
        // Tackle defaults to MaxPp=0; PP dict shouldn't even contain it.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(emberPp: 5);

        Assert.False(gameState.ActiveBattle!.Actors[Hero].SkillPp.ContainsKey(Tackle));

        for (int i = 0; i < 20; i++)
        {
            Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Tackle, Wild), Ctx(sink)).IsSuccess);
            if (gameState.ActiveBattle is null || gameState.ActiveBattle.Status != BattleStatus.Active) break;
            Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), Ctx(sink)).IsSuccess);
            if (gameState.ActiveBattle is null || gameState.ActiveBattle.Status != BattleStatus.Active) break;
        }

        // Reaching here without an out-of-PP error confirms Tackle is unlimited.
    }

    [Fact]
    public void Untracked_Actor_Starts_Battle_At_Max_Pp_And_Does_Not_Pollute_State()
    {
        // Wild enemies aren't tracked — they should start with full PP each battle and not
        // leave behind ActorSkillPpState entries.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(emberPp: 3);
        Assert.False(gameState.ActorSkillPpState.IsTracked(Wild));

        gameState.ActiveBattle!.Status = BattleStatus.Victory;
        gameState.ActiveBattle = null;

        Assert.False(gameState.ActorSkillPpState.IsTracked(Wild));
    }

    [Fact]
    public void Tracked_Actor_Persists_Pp_Across_Battles()
    {
        // Add hero to party (auto-tracks PP), use Ember twice, end battle, start a fresh
        // battle — hero should resume with reduced PP, not full.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(emberPp: 5);

        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Ember, Wild), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Ember, Wild), Ctx(sink)).IsSuccess);

        // Drop wild to 1 HP then take the killing blow with Tackle so the battle end runs
        // through the normal path (UpdateBattleEndState → PersistAllTrackedSkillPp).
        gameState.ActiveBattle!.Actors[Wild].Hp = 1;
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Tackle, Wild), Ctx(sink)).IsSuccess);

        Assert.Null(gameState.ActiveBattle);
        Assert.True(gameState.ActorSkillPpState.IsTracked(Hero));
        Assert.True(gameState.ActorSkillPpState.TryGetSkillPp(Hero, Ember, out int storedPp));
        Assert.Equal(3, storedPp);

        // Second battle — Ember should start at 3, not 5.
        Assert.True(dispatcher.Dispatch(gameState, BuildStartBattle("battle.2", emberPp: 5), Ctx(sink)).IsSuccess);
        Assert.Equal(3, gameState.ActiveBattle!.Actors[Hero].SkillPp[Ember]);
    }

    [Fact]
    public void RestoreSkillPp_Refills_Tracked_Skill()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(emberPp: 5);

        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Ember, Wild), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), Ctx(sink)).IsSuccess);
        Assert.Equal(4, gameState.ActiveBattle!.Actors[Hero].SkillPp[Ember]);

        Assert.True(dispatcher.Dispatch(gameState, new RestoreSkillPpCommand(Hero, Ember, amount: 10), Ctx(sink)).IsSuccess);

        // Capped at MaxPp = 5.
        Assert.Equal(5, gameState.ActiveBattle!.Actors[Hero].SkillPp[Ember]);
        Assert.True(gameState.ActorSkillPpState.TryGetSkillPp(Hero, Ember, out int storedPp) && storedPp == 5);
    }

    [Fact]
    public void RestoreSkillPp_For_Untracked_Actor_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(emberPp: 5);

        DomainResult result = dispatcher.Dispatch(gameState, new RestoreSkillPpCommand(Wild, Ember, amount: 1), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.NotFound, result.Error!.Code);
    }

    [Fact]
    public void Capture_Persists_Captured_Actor_Pp_And_Tracks_Them()
    {
        // Wild starts untracked. After capture it should be tracked AND have its captured
        // PP value persisted (matching the in-battle current PP).
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(emberPp: 5);

        // Give the wild a PP-tracked skill so we have something to persist.
        gameState.ActiveBattle!.Actors[Wild].SkillPp[Ember] = 3;
        gameState.ActiveBattle!.Actors[Wild].CaptureBaseRate = 100;
        gameState.ActiveBattle!.Actors[Wild].Hp = 1;

        Assert.True(dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Hero, Wild, bonusPercent: 200), Ctx(sink)).IsSuccess);

        Assert.True(gameState.ActorSkillPpState.IsTracked(Wild));
        Assert.True(gameState.ActorSkillPpState.TryGetSkillPp(Wild, Ember, out int storedPp));
        Assert.Equal(3, storedPp);
    }

    [Fact]
    public void Save_RoundTrip_Preserves_Pp_State()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartBattle(emberPp: 5);
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Ember, Wild), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), Ctx(sink)).IsSuccess);
        gameState.ActiveBattle!.Actors[Wild].Hp = 1;
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Tackle, Wild), Ctx(sink)).IsSuccess);
        Assert.Null(gameState.ActiveBattle);
        Assert.True(gameState.ActorSkillPpState.TryGetSkillPp(Hero, Ember, out int beforeSave) && beforeSave == 4);

        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(gameState));
        GameStateSnapshot decoded = serializer.Deserialize(json);
        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.True(rebuilt.ActorSkillPpState.IsTracked(Hero));
        Assert.True(rebuilt.ActorSkillPpState.TryGetSkillPp(Hero, Ember, out int rebuiltPp));
        Assert.Equal(4, rebuiltPp);
    }

    private static (GameState, CommandDispatcher, InMemoryDomainEventSink) StartBattle(int emberPp)
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();

        // Add hero to party so PP auto-tracks for them.
        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 1, maxRoster: 6), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand(Hero, active: true), Ctx(sink)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, BuildStartBattle("battle.1", emberPp), Ctx(sink)).IsSuccess);
        return (gameState, dispatcher, sink);
    }

    private static StartBattleCommand BuildStartBattle(string battleId, int emberPp)
    {
        List<BattleSkillDefinition> skills =
        [
            new BattleSkillDefinition(Tackle, BattleSkillEffectType.PhysicalDamage, power: 2),
            new BattleSkillDefinition(Ember, BattleSkillEffectType.MagicalDamage, power: 2, maxPp: emberPp)
        ];

        // Wild is intentionally tanky relative to skill damage so PP scenarios can run multiple
        // turns without the wild dying mid-test.
        List<BattleActorDefinition> actors =
        [
            new BattleActorDefinition(
                actorId: Hero,
                displayName: "Hero",
                faction: CombatFaction.Party,
                maxHp: 200, atk: 4, def: 5, matk: 4, mdef: 5,
                initiative: 20,
                skillIds: [Tackle, Ember],
                playerControlled: true),
            new BattleActorDefinition(
                actorId: Wild,
                displayName: "Wild",
                faction: CombatFaction.Enemy,
                maxHp: 200, atk: 4, def: 3, matk: 3, mdef: 2,
                initiative: 10,
                skillIds: [Tackle],
                playerControlled: false)
        ];

        return new StartBattleCommand(battleId, actors, skills, seed: 7, sequence: 1);
    }

    private static CommandContext Ctx(InMemoryDomainEventSink sink)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1, sequence: 1),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            new InMemoryGameDefinitionCatalog().AddCurrency(new CurrencyDefinition("currency.gold", 999)));
    }
}
