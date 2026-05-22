using System.Collections.Generic;
using System.Linq;
using Moonforge.Core;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Party;
using Moonforge.Core.Party.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class CaptureTests
{
    private const string Starter = "mon.starter";
    private const string Bench = "mon.bench";
    private const string Wild = "mon.wild";

    [Fact]
    public void Capture_Succeeds_When_Chance_Caps_At_100()
    {
        // baseRate=100 × hpFactor (≈1 at 1/100 HP) × bonus 200% → clamped to 100%; always succeeds.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 100, wildStartingHp: 1, wildMaxHp: 100);

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new AttemptCaptureCommand(Starter, Wild, bonusPercent: 200),
            Ctx(sink));

        Assert.True(result.IsSuccess);
        Assert.Single(sink.Events.OfType<BattleActorCapturedEvent>(),
            e => e.CapturedActorId == Wild && e.HpAtCapture == 1 && e.MaxHpAtCapture == 100);
        Assert.True(gameState.PartyState.Contains(Wild));
        Assert.True(gameState.PartyState.TryGet(Wild, out PartyMember added) && !added.IsActive);
    }

    [Fact]
    public void Capture_Fails_When_Chance_Floors_At_Zero()
    {
        // baseRate=1 × hpFactor (1/3 at full HP) × 100% → 0.33% → rounds to 0; always misses.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 1, wildStartingHp: 100, wildMaxHp: 100);

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new AttemptCaptureCommand(Starter, Wild, bonusPercent: 100),
            Ctx(sink));

        Assert.True(result.IsSuccess);
        Assert.Empty(sink.Events.OfType<BattleActorCapturedEvent>());
        Assert.Single(sink.Events.OfType<CaptureAttemptFailedEvent>(),
            e => e.RolledChancePercent == 0);
        Assert.True(gameState.ActiveBattle!.Actors.ContainsKey(Wild));
        Assert.False(gameState.PartyState.Contains(Wild));
    }

    [Fact]
    public void Capture_Successfully_Removes_Target_From_Battle()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 100, wildStartingHp: 1, wildMaxHp: 100);

        Assert.True(dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Starter, Wild, bonusPercent: 200), Ctx(sink)).IsSuccess);

        // Last enemy gone → battle ends Victory and ActiveBattle is cleared by the handler.
        Assert.Null(gameState.ActiveBattle);
        Assert.Single(sink.Events.OfType<BattleEndedEvent>(),
            e => e.Status == BattleStatus.Victory);
    }

    [Fact]
    public void BattleEndedEvent_Includes_Final_Actor_Hp_Snapshot()
    {
        // After the battle ends ActiveBattle is null — consumers needing post-battle HP
        // (party-wipe detection, persistent health bars) must read it from the event.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 100, wildStartingHp: 1, wildMaxHp: 100);
        gameState.ActiveBattle!.Actors[Starter].Hp = 23;

        Assert.True(dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Starter, Wild, bonusPercent: 200), Ctx(sink)).IsSuccess);

        Assert.Null(gameState.ActiveBattle);
        BattleEndedEvent ended = sink.Events.OfType<BattleEndedEvent>().Single();
        // Captured actors are removed from the battle before it ends, so the snapshot
        // contains only actors still in the battle at the moment of close. Party
        // members always remain so HP carries over post-battle.
        Assert.Equal(23, ended.FinalActorHp[Starter]);
        Assert.Equal(30, ended.FinalActorMaxHp[Starter]);
    }

    [Fact]
    public void Capture_Of_Uncapturable_Target_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 0, wildStartingHp: 50, wildMaxHp: 100);

        DomainResult result = dispatcher.Dispatch(
            gameState,
            new AttemptCaptureCommand(Starter, Wild, bonusPercent: 200),
            Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.UnsupportedOperation, result.Error!.Code);
        Assert.True(gameState.ActiveBattle!.Actors.ContainsKey(Wild));
    }

    [Fact]
    public void Capture_Of_Downed_Target_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 100, wildStartingHp: 50, wildMaxHp: 100);
        gameState.ActiveBattle!.Actors[Wild].Hp = 0;

        DomainResult result = dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Starter, Wild, bonusPercent: 200), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, result.Error!.Code);
    }

    [Fact]
    public void Capture_Of_Ally_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 100, wildStartingHp: 50, wildMaxHp: 100);

        DomainResult result = dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Starter, Starter, bonusPercent: 200), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.UnsupportedOperation, result.Error!.Code);
    }

    [Fact]
    public void Capture_When_Party_Full_Rolls_Back_Whole_Transaction()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 100, wildStartingHp: 1, wildMaxHp: 100, maxRoster: 2);
        // Roster already has Starter + Bench → max 2.

        DomainResult result = dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Starter, Wild, bonusPercent: 200), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.ValidationFailed, result.Error!.Code);
        // Roll-back: wild still in battle, party unchanged, capture event never visible.
        Assert.True(gameState.ActiveBattle!.Actors.ContainsKey(Wild));
        Assert.Equal(2, gameState.PartyState.Members.Count);
        Assert.Empty(sink.Events.OfType<BattleActorCapturedEvent>());
    }

    [Fact]
    public void Failed_Capture_Consumes_Turn()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 1, wildStartingHp: 100, wildMaxHp: 100);

        Assert.Equal(Starter, gameState.ActiveBattle!.TurnOrder[gameState.ActiveBattle.TurnIndex]);

        Assert.True(dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Starter, Wild, bonusPercent: 100), Ctx(sink)).IsSuccess);

        // Turn should have advanced to the wild.
        Assert.Equal(Wild, gameState.ActiveBattle!.TurnOrder[gameState.ActiveBattle.TurnIndex]);
    }

    [Fact]
    public void Bonus_Percent_Influences_Computed_Chance()
    {
        // baseRate=10 × hpFactor (1/3 at full) × bonus → ~3.3% at 100%, ~6.7% at 200%.
        // Force failure (low bonus) so we can read the RolledChancePercent from the event.
        (GameState a, CommandDispatcher d1, InMemoryDomainEventSink s1) = StartCaptureBattle(captureBaseRate: 10, wildStartingHp: 100, wildMaxHp: 100);
        (GameState b, CommandDispatcher d2, InMemoryDomainEventSink s2) = StartCaptureBattle(captureBaseRate: 10, wildStartingHp: 100, wildMaxHp: 100);

        Assert.True(d1.Dispatch(a, new AttemptCaptureCommand(Starter, Wild, bonusPercent: 100), Ctx(s1)).IsSuccess);
        Assert.True(d2.Dispatch(b, new AttemptCaptureCommand(Starter, Wild, bonusPercent: 200), Ctx(s2)).IsSuccess);

        CaptureAttemptFailedEvent at100 = s1.Events.OfType<CaptureAttemptFailedEvent>().Single();
        CaptureAttemptFailedEvent at200 = s2.Events.OfType<CaptureAttemptFailedEvent>().Single();
        Assert.True(at200.RolledChancePercent > at100.RolledChancePercent,
            $"200% bonus chance ({at200.RolledChancePercent}) should exceed 100% bonus chance ({at100.RolledChancePercent})");
    }

    [Fact]
    public void Capture_Requires_Current_Turn_Actor()
    {
        // Wild has higher initiative — its turn is first; starter can't attempt yet.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartCaptureBattle(
            captureBaseRate: 100, wildStartingHp: 1, wildMaxHp: 100, starterFirst: false);

        DomainResult result = dispatcher.Dispatch(gameState, new AttemptCaptureCommand(Starter, Wild, bonusPercent: 200), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, result.Error!.Code);
    }

    private static (GameState, CommandDispatcher, InMemoryDomainEventSink) StartCaptureBattle(
        int captureBaseRate,
        int wildStartingHp,
        int wildMaxHp,
        int maxRoster = 6,
        bool starterFirst = true)
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();

        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 1, maxRoster: maxRoster), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand(Starter, active: true), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand(Bench, active: false), Ctx(sink)).IsSuccess);

        List<BattleSkillDefinition> skills = [new BattleSkillDefinition("skill.tackle", BattleSkillEffectType.PhysicalDamage, power: 5)];

        int starterInit = starterFirst ? 20 : 5;
        int wildInit = starterFirst ? 10 : 20;

        BattleActorDefinition wild = new(
            actorId: Wild,
            displayName: "Wild",
            faction: CombatFaction.Enemy,
            maxHp: wildMaxHp,
            atk: 5, def: 3, matk: 3, mdef: 2,
            initiative: wildInit,
            skillIds: ["skill.tackle"],
            playerControlled: false,
            captureBaseRate: captureBaseRate);

        List<BattleActorDefinition> actors =
        [
            new BattleActorDefinition(
                actorId: Starter,
                displayName: "Starter",
                faction: CombatFaction.Party,
                maxHp: 30, atk: 7, def: 4, matk: 4, mdef: 3,
                initiative: starterInit,
                skillIds: ["skill.tackle"],
                playerControlled: true),
            wild
        ];

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand("battle.cap", actors, skills, seed: 7, sequence: 1),
            Ctx(sink)).IsSuccess);

        if (wildStartingHp < wildMaxHp)
        {
            gameState.ActiveBattle!.Actors[Wild].Hp = wildStartingHp;
        }

        return (gameState, dispatcher, sink);
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
