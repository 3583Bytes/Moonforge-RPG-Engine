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

public sealed class SwapBattleActorTests
{
    private const string Starter = "mon.starter";
    private const string Bench = "mon.bench";
    private const string Wild = "mon.wild";

    [Fact]
    public void Swap_Replaces_Actor_In_TurnOrder_And_Advances_Turn()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartMonsterBattle(starterFirst: true);

        Assert.Equal(Starter, gameState.ActiveBattle!.TurnOrder[gameState.ActiveBattle.TurnIndex]);

        BattleActorDefinition incoming = BenchMonster();
        DomainResult result = dispatcher.Dispatch(gameState, new SwapBattleActorCommand(Starter, incoming), Ctx(sink));

        Assert.True(result.IsSuccess);
        Assert.False(gameState.ActiveBattle!.Actors.ContainsKey(Starter));
        Assert.True(gameState.ActiveBattle!.Actors.ContainsKey(Bench));
        Assert.Contains(Bench, gameState.ActiveBattle!.TurnOrder);
        Assert.DoesNotContain(Starter, gameState.ActiveBattle!.TurnOrder);
        // Swap consumed the starter's turn; current actor is now the wild enemy.
        Assert.Equal(Wild, gameState.ActiveBattle!.TurnOrder[gameState.ActiveBattle.TurnIndex]);
        Assert.Single(sink.Events.OfType<BattleActorSwappedEvent>(),
            e => e.OutActorId == Starter && e.InActorId == Bench);
    }

    [Fact]
    public void Reactor_Syncs_Party_Active_Flags()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartMonsterBattle(starterFirst: true);

        Assert.True(gameState.PartyState.TryGet(Starter, out PartyMember s) && s.IsActive);
        Assert.True(gameState.PartyState.TryGet(Bench, out PartyMember b) && !b.IsActive);

        Assert.True(dispatcher.Dispatch(gameState, new SwapBattleActorCommand(Starter, BenchMonster()), Ctx(sink)).IsSuccess);

        Assert.True(gameState.PartyState.TryGet(Starter, out s) && !s.IsActive);
        Assert.True(gameState.PartyState.TryGet(Bench, out b) && b.IsActive);
    }

    [Fact]
    public void Swap_When_Not_Current_Turn_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartMonsterBattle(starterFirst: false);
        // Starter is slower than the wild — wild's slot is current.
        Assert.Equal(Wild, gameState.ActiveBattle!.TurnOrder[gameState.ActiveBattle.TurnIndex]);

        DomainResult result = dispatcher.Dispatch(gameState, new SwapBattleActorCommand(Starter, BenchMonster()), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, result.Error!.Code);
        Assert.True(gameState.ActiveBattle!.Actors.ContainsKey(Starter));
    }

    [Fact]
    public void Swap_Enemy_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartMonsterBattle(starterFirst: false);
        // Wild's turn — but swapping the wild itself must be rejected (party-only).
        DomainResult result = dispatcher.Dispatch(gameState, new SwapBattleActorCommand(Wild, BenchMonster()), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.UnsupportedOperation, result.Error!.Code);
    }

    [Fact]
    public void Swap_To_Same_Actor_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartMonsterBattle(starterFirst: true);

        BattleActorDefinition sameId = StarterMonster();
        DomainResult result = dispatcher.Dispatch(gameState, new SwapBattleActorCommand(Starter, sameId), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.ValidationFailed, result.Error!.Code);
    }

    [Fact]
    public void Swap_To_Actor_Already_In_Battle_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartMonsterBattle(starterFirst: true);

        BattleActorDefinition dupe = new(
            actorId: Wild,
            displayName: "Wild",
            faction: CombatFaction.Party,  // even forced to party, duplicate id is rejected
            maxHp: 10,
            atk: 1,
            def: 1,
            matk: 1,
            mdef: 1,
            initiative: 5,
            skillIds: ["skill.tackle"],
            playerControlled: true);

        DomainResult result = dispatcher.Dispatch(gameState, new SwapBattleActorCommand(Starter, dupe), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, result.Error!.Code);
    }

    [Fact]
    public void Swap_To_Enemy_Faction_Definition_Fails()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartMonsterBattle(starterFirst: true);

        BattleActorDefinition badFaction = new(
            actorId: Bench,
            displayName: "Bench",
            faction: CombatFaction.Enemy,
            maxHp: 10,
            atk: 1,
            def: 1,
            matk: 1,
            mdef: 1,
            initiative: 5,
            skillIds: ["skill.tackle"],
            playerControlled: true);

        DomainResult result = dispatcher.Dispatch(gameState, new SwapBattleActorCommand(Starter, badFaction), Ctx(sink));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.ValidationFailed, result.Error!.Code);
    }

    [Fact]
    public void Reactor_Ignores_Actors_Not_In_Party()
    {
        // A scripted "wild swaps to a new wild" scenario — neither actor is in the player
        // party. The reactor should silently leave the party untouched.
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartMonsterBattle(starterFirst: true);

        // First: empty the party so neither actor matches.
        Assert.True(dispatcher.Dispatch(gameState, new RemovePartyMemberCommand(Starter), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new RemovePartyMemberCommand(Bench), Ctx(sink)).IsSuccess);

        // Swap should still succeed at the combat layer (party validation is the game's job).
        DomainResult result = dispatcher.Dispatch(gameState, new SwapBattleActorCommand(Starter, BenchMonster()), Ctx(sink));

        Assert.True(result.IsSuccess);
        Assert.Empty(gameState.PartyState.Members);
    }

    private static (GameState, CommandDispatcher, InMemoryDomainEventSink) StartMonsterBattle(bool starterFirst)
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();

        Assert.True(dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 1, maxRoster: 6), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand(Starter, active: true), Ctx(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new AddPartyMemberCommand(Bench, active: false), Ctx(sink)).IsSuccess);

        List<BattleSkillDefinition> skills =
        [
            new BattleSkillDefinition("skill.tackle", BattleSkillEffectType.PhysicalDamage, power: 5),
            new BattleSkillDefinition("skill.bite", BattleSkillEffectType.PhysicalDamage, power: 4)
        ];

        int starterInit = starterFirst ? 20 : 5;
        int wildInit = starterFirst ? 10 : 20;

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
            new BattleActorDefinition(
                actorId: Wild,
                displayName: "Wild",
                faction: CombatFaction.Enemy,
                maxHp: 25, atk: 6, def: 3, matk: 3, mdef: 2,
                initiative: wildInit,
                skillIds: ["skill.bite"],
                playerControlled: false)
        ];

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand("battle.swap", actors, skills, seed: 7, sequence: 1),
            Ctx(sink)).IsSuccess);

        return (gameState, dispatcher, sink);
    }

    private static BattleActorDefinition StarterMonster() => new(
        actorId: Starter, displayName: "Starter", faction: CombatFaction.Party,
        maxHp: 30, atk: 7, def: 4, matk: 4, mdef: 3, initiative: 20,
        skillIds: ["skill.tackle"], playerControlled: true);

    private static BattleActorDefinition BenchMonster() => new(
        actorId: Bench, displayName: "Bench", faction: CombatFaction.Party,
        maxHp: 28, atk: 8, def: 3, matk: 5, mdef: 4, initiative: 18,
        skillIds: ["skill.tackle"], playerControlled: true);

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
