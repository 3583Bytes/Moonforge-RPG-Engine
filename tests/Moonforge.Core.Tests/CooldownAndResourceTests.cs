using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class CooldownAndResourceTests
{
    private const string Hero = "party.hero";
    private const string Slime = "enemy.slime";
    private const string Heavy = "skill.heavy";
    private const string Attack = "skill.attack";
    private const string Focus = "focus";

    [Fact]
    public void Skill_With_Cooldown_Blocks_Reuse_Until_Elapsed()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = CreateWorld();

        Assert.True(Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Heavy, Slime), sink).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new ExecuteAiTurnCommand(), sink).IsSuccess);

        DomainResult retry = Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Heavy, Slime), sink);
        Assert.False(retry.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, retry.Error!.Code);
        Assert.Contains("cooldown", retry.Error!.Message);
    }

    [Fact]
    public void Cooldown_Decrements_On_Turn_Advance_And_Allows_Reuse()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = CreateWorld();

        Assert.True(Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Heavy, Slime), sink).IsSuccess);
        Assert.Equal(2, gameState.ActiveBattle!.Actors[Hero].Cooldowns[Heavy]);
        Assert.True(Dispatch(dispatcher, gameState, new ExecuteAiTurnCommand(), sink).IsSuccess);
        Assert.Equal(1, gameState.ActiveBattle!.Actors[Hero].Cooldowns[Heavy]);

        DomainResult stillBlocked = Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Heavy, Slime), sink);
        Assert.False(stillBlocked.IsSuccess);

        Assert.True(Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Attack, Slime), sink).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new ExecuteAiTurnCommand(), sink).IsSuccess);

        Assert.False(gameState.ActiveBattle!.Actors[Hero].Cooldowns.ContainsKey(Heavy));
        Assert.True(Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Heavy, Slime), sink).IsSuccess);
    }

    [Fact]
    public void Skill_Use_Deducts_Resource_Cost()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = CreateWorld();

        Assert.Equal(3, gameState.ActiveBattle!.Actors[Hero].Resources[Focus]);
        Assert.True(Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Heavy, Slime), sink).IsSuccess);
        Assert.Equal(1, gameState.ActiveBattle!.Actors[Hero].Resources[Focus]);
    }

    [Fact]
    public void Insufficient_Resource_Blocks_Skill_Use()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = CreateWorld();
        gameState.ActiveBattle!.Actors[Hero].Resources[Focus] = 1;

        DomainResult result = Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Heavy, Slime), sink);

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InsufficientResources, result.Error!.Code);
        Assert.Equal(1, gameState.ActiveBattle!.Actors[Hero].Resources[Focus]);
    }

    [Fact]
    public void Per_Turn_Refresh_Adds_Resource_Up_To_Max()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = CreateWorld();
        gameState.ActiveBattle!.Actors[Hero].Resources[Focus] = 0;

        Assert.True(Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Attack, Slime), sink).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new ExecuteAiTurnCommand(), sink).IsSuccess);

        Assert.Equal(1, gameState.ActiveBattle!.Actors[Hero].Resources[Focus]);
    }

    [Fact]
    public void Per_Turn_Refresh_Clamps_To_Maximum()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = CreateWorld();

        Assert.Equal(3, gameState.ActiveBattle!.Actors[Hero].Resources[Focus]);
        Assert.True(Dispatch(dispatcher, gameState, new UseBattleSkillCommand(Hero, Attack, Slime), sink).IsSuccess);
        Assert.True(Dispatch(dispatcher, gameState, new ExecuteAiTurnCommand(), sink).IsSuccess);

        Assert.Equal(3, gameState.ActiveBattle!.Actors[Hero].Resources[Focus]);
    }

    private static (GameState, CommandDispatcher, InMemoryDomainEventSink) CreateWorld()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new StartBattleCommandHandler());
        dispatcher.Register(new UseBattleSkillCommandHandler());
        dispatcher.Register(new ExecuteAiTurnCommandHandler());

        BattleActorDefinition hero = new(
            actorId: Hero,
            displayName: "Hero",
            faction: CombatFaction.Party,
            maxHp: 60,
            atk: 10,
            def: 3,
            matk: 3,
            mdef: 3,
            initiative: 20,
            skillIds: [Attack, Heavy],
            playerControlled: true,
            resourceMaxes: new Dictionary<string, int> { [Focus] = 3 },
            startingResources: new Dictionary<string, int> { [Focus] = 3 },
            resourceRefreshPerTurn: new Dictionary<string, int> { [Focus] = 1 });

        BattleActorDefinition slime = new(
            actorId: Slime,
            displayName: "Slime",
            faction: CombatFaction.Enemy,
            maxHp: 80,
            atk: 1,
            def: 0,
            matk: 0,
            mdef: 0,
            initiative: 5,
            skillIds: [Attack],
            playerControlled: false);

        List<BattleSkillDefinition> skills =
        [
            new BattleSkillDefinition(Attack, BattleSkillEffectType.PhysicalDamage, power: 3),
            new BattleSkillDefinition(
                Heavy,
                BattleSkillEffectType.PhysicalDamage,
                power: 8,
                cooldownTurns: 2,
                resourceCosts: new Dictionary<string, int> { [Focus] = 2 })
        ];

        Assert.True(Dispatch(
            dispatcher,
            gameState,
            new StartBattleCommand("battle.test", [hero, slime], skills, seed: 1),
            sink).IsSuccess);

        return (gameState, dispatcher, sink);
    }

    private static DomainResult Dispatch<TCommand>(CommandDispatcher dispatcher, GameState gameState, TCommand command, InMemoryDomainEventSink sink) where TCommand : ICommand
    {
        return dispatcher.Dispatch(gameState, command, new CommandContext(
            new Pcg32RandomSource(seed: 777, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            new InMemoryGameDefinitionCatalog()
                .AddCurrency(new CurrencyDefinition("currency.gold", 999))));
    }
}
