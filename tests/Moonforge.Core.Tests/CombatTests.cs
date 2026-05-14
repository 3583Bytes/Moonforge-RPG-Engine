using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class CombatTests
{
    [Fact]
    public void StartBattle_Uses_Static_Initiative_Order()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();

        DomainResult result = dispatcher.Dispatch(gameState, CreateBattleCommand(seed: 1), CreateContext(sink));

        Assert.True(result.IsSuccess);
        Assert.NotNull(gameState.ActiveBattle);
        Assert.Equal("party.hero", gameState.ActiveBattle!.TurnOrder[0]);
        Assert.Equal("enemy.goblin", gameState.ActiveBattle!.TurnOrder[1]);
    }

    [Fact]
    public void Ai_Uses_Priority_Heal_When_Low_Hp()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        Assert.True(dispatcher.Dispatch(gameState, CreateBattleCommand(seed: 2), CreateContext(sink)).IsSuccess);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.hero", "skill.attack", "enemy.goblin"),
            CreateContext(sink)).IsSuccess);

        int hpAfterHit = gameState.ActiveBattle!.Actors["enemy.goblin"].Hp;
        Assert.True(hpAfterHit <= 10);

        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), CreateContext(sink)).IsSuccess);

        int hpAfterHeal = gameState.ActiveBattle!.Actors["enemy.goblin"].Hp;
        Assert.True(hpAfterHeal > hpAfterHit);
        Assert.Contains(sink.Events, e => e is BattleActionResolvedEvent action && action.SkillId == "skill.heal");
    }

    [Fact]
    public void Downed_Actor_Can_Be_Revived_By_Heal()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        Assert.True(dispatcher.Dispatch(gameState, CreateBattleCommand(seed: 3), CreateContext(sink)).IsSuccess);

        // Turn 1: hero attacks enemy to 0 HP (downed).
        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.hero", "skill.attack", "enemy.goblin"),
            CreateContext(sink)).IsSuccess);
        gameState.ActiveBattle!.Actors["enemy.goblin"].Hp = 0;

        // Turn 2: goblin is downed and should be skipped; cleric acts.
        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.cleric", "skill.attack", "enemy.shaman"),
            CreateContext(sink)).IsSuccess);

        // Turn 3: enemy shaman heals downed goblin.
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), CreateContext(sink)).IsSuccess);

        Assert.True(gameState.ActiveBattle!.Actors["enemy.goblin"].Hp > 0);
    }

    [Fact]
    public void Same_Seed_And_Inputs_Produce_Equivalent_Battle_Result()
    {
        GameState a = RunDeterministicScenario(seed: 444);
        GameState b = RunDeterministicScenario(seed: 444);

        Assert.Equal(a.ActiveBattle!.Round, b.ActiveBattle!.Round);
        Assert.Equal(a.ActiveBattle!.TurnIndex, b.ActiveBattle!.TurnIndex);
        Assert.Equal(a.ActiveBattle!.RngState.RollsUsed, b.ActiveBattle!.RngState.RollsUsed);

        foreach ((string actorId, BattleActorState actorA) in a.ActiveBattle!.Actors)
        {
            Assert.True(b.ActiveBattle!.TryGetActor(actorId, out BattleActorState actorB));
            Assert.Equal(actorA.Hp, actorB.Hp);
        }
    }

    [Fact]
    public void Victory_Applies_Reward_Transactions_Atomically()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddItem(new ItemDefinition("item.herb", 20));

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand(
                battleId: "battle.reward.test",
                actors:
                [
                    new BattleActorDefinition(
                        actorId: "party.hero",
                        displayName: "Hero",
                        faction: CombatFaction.Party,
                        maxHp: 30,
                        atk: 10,
                        def: 3,
                        matk: 2,
                        mdef: 2,
                        initiative: 20,
                        skillIds: ["skill.attack"],
                        playerControlled: true),
                    new BattleActorDefinition(
                        actorId: "enemy.slime",
                        displayName: "Slime",
                        faction: CombatFaction.Enemy,
                        maxHp: 8,
                        atk: 1,
                        def: 0,
                        matk: 0,
                        mdef: 0,
                        initiative: 10,
                        skillIds: ["skill.attack"],
                        playerControlled: false)
                ],
                skills:
                [
                    new BattleSkillDefinition("skill.attack", BattleSkillEffectType.PhysicalDamage, 5)
                ],
                seed: 7,
                rewardCurrency: [new CurrencyDelta("currency.gold", 12)],
                rewardInventory: [new InventoryDelta("item.herb", 2)]),
            CreateContext(sink, definitions)).IsSuccess);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.hero", "skill.attack", "enemy.slime"),
            CreateContext(sink, definitions)).IsSuccess);

        Assert.Null(gameState.ActiveBattle);
        Assert.Equal(12, gameState.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Equal(2, gameState.InventoryBag.GetTotalQuantity("item.herb"));
    }

    [Fact]
    public void Combat_Kill_Emits_Quest_Kill_Signal_For_AutoTracking()
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();
        dispatcher.RegisterReactor(new QuestObjectiveTrackingReactor());
        dispatcher.Register(new StartQuestCommandHandler());

        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddCurrency(new CurrencyDefinition("currency.gold", 999))
            .AddQuest(new QuestDefinition(
                id: "quest.kill.slime",
                objectives:
                [
                    new QuestObjectiveDefinition("obj.kill.slime", QuestObjectiveType.Kill, targetId: "enemy.slime", requiredCount: 1)
                ],
                rootObjectiveIds: ["obj.kill.slime"]));

        Assert.True(dispatcher.Dispatch(gameState, new StartQuestCommand("quest.kill.slime"), CreateContext(sink, definitions)).IsSuccess);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand(
                battleId: "battle.kill.quest",
                actors:
                [
                    new BattleActorDefinition(
                        actorId: "party.hero",
                        displayName: "Hero",
                        faction: CombatFaction.Party,
                        maxHp: 30,
                        atk: 10,
                        def: 3,
                        matk: 2,
                        mdef: 2,
                        initiative: 20,
                        skillIds: ["skill.attack"],
                        playerControlled: true),
                    new BattleActorDefinition(
                        actorId: "enemy.slime",
                        displayName: "Slime",
                        faction: CombatFaction.Enemy,
                        maxHp: 8,
                        atk: 1,
                        def: 0,
                        matk: 0,
                        mdef: 0,
                        initiative: 10,
                        skillIds: ["skill.attack"],
                        playerControlled: false)
                ],
                skills:
                [
                    new BattleSkillDefinition("skill.attack", BattleSkillEffectType.PhysicalDamage, 5)
                ],
                seed: 9),
            CreateContext(sink, definitions)).IsSuccess);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.hero", "skill.attack", "enemy.slime"),
            CreateContext(sink, definitions)).IsSuccess);

        Assert.True(gameState.QuestState.TryGet("quest.kill.slime", out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Completed, quest.Status);
        Assert.Equal(1, quest.GetObjectiveProgress("obj.kill.slime"));
    }

    private static GameState RunDeterministicScenario(ulong seed)
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = CreateDispatcher();

        Assert.True(dispatcher.Dispatch(gameState, CreateBattleCommand(seed), CreateContext(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.hero", "skill.attack", "enemy.goblin"),
            CreateContext(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), CreateContext(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.cleric", "skill.attack", "enemy.shaman"),
            CreateContext(sink)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(), CreateContext(sink)).IsSuccess);

        return gameState;
    }

    private static StartBattleCommand CreateBattleCommand(ulong seed)
    {
        List<BattleSkillDefinition> skills =
        [
            new BattleSkillDefinition("skill.attack", BattleSkillEffectType.PhysicalDamage, power: 8),
            new BattleSkillDefinition("skill.heal", BattleSkillEffectType.Heal, power: 6),
            new BattleSkillDefinition("skill.fire", BattleSkillEffectType.MagicalDamage, power: 7)
        ];

        BattleAiPolicyDefinition goblinAi = new(
            rules:
            [
                new BattleAiRuleDefinition(
                    skillId: "skill.heal",
                    priorityWeight: 100,
                    targetPolicy: BattleAiTargetPolicy.LowestHpAlly,
                    conditions: [new BattleAiConditionDefinition(BattleAiConditionType.SelfHpBelowPercent, 60)])
            ],
            fallbackSkillId: "skill.attack",
            fallbackTargetPolicy: BattleAiTargetPolicy.LowestHpEnemy);

        BattleAiPolicyDefinition shamanAi = new(
            rules:
            [
                new BattleAiRuleDefinition(
                    skillId: "skill.heal",
                    priorityWeight: 90,
                    targetPolicy: BattleAiTargetPolicy.LowestHpAlly,
                    conditions: [new BattleAiConditionDefinition(BattleAiConditionType.AnyAllyHpBelowPercent, 100)])
            ],
            fallbackSkillId: "skill.fire",
            fallbackTargetPolicy: BattleAiTargetPolicy.HighestThreatEnemy);

        List<BattleActorDefinition> actors =
        [
            new BattleActorDefinition(
                actorId: "party.hero",
                displayName: "Hero",
                faction: CombatFaction.Party,
                maxHp: 35,
                atk: 12,
                def: 5,
                matk: 4,
                mdef: 3,
                initiative: 20,
                skillIds: ["skill.attack"],
                playerControlled: true),
            new BattleActorDefinition(
                actorId: "enemy.goblin",
                displayName: "Goblin",
                faction: CombatFaction.Enemy,
                maxHp: 20,
                atk: 7,
                def: 2,
                matk: 3,
                mdef: 1,
                initiative: 15,
                skillIds: ["skill.attack", "skill.heal"],
                playerControlled: false,
                aiPolicy: goblinAi),
            new BattleActorDefinition(
                actorId: "party.cleric",
                displayName: "Cleric",
                faction: CombatFaction.Party,
                maxHp: 28,
                atk: 4,
                def: 4,
                matk: 10,
                mdef: 6,
                initiative: 13,
                skillIds: ["skill.heal", "skill.attack"],
                playerControlled: true),
            new BattleActorDefinition(
                actorId: "enemy.shaman",
                displayName: "Shaman",
                faction: CombatFaction.Enemy,
                maxHp: 24,
                atk: 5,
                def: 3,
                matk: 9,
                mdef: 4,
                initiative: 11,
                skillIds: ["skill.fire", "skill.heal"],
                playerControlled: false,
                aiPolicy: shamanAi)
        ];

        return new StartBattleCommand("battle.test", actors, skills, seed: seed, sequence: 777);
    }

    private static CommandDispatcher CreateDispatcher()
    {
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new StartBattleCommandHandler());
        dispatcher.Register(new UseBattleSkillCommandHandler());
        dispatcher.Register(new ExecuteAiTurnCommandHandler());
        return dispatcher;
    }

    private static CommandContext CreateContext(
        InMemoryDomainEventSink sink,
        IGameDefinitionCatalog? definitions = null)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 9999, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions ?? new InMemoryGameDefinitionCatalog().AddCurrency(new CurrencyDefinition("currency.gold", 999)));
    }
}
