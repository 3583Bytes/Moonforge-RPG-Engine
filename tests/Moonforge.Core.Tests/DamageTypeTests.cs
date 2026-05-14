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
using Moonforge.Core.Stats;

namespace Moonforge.Core.Tests;

public sealed class DamageTypeTests
{
    private const string Hero = "party.hero";
    private const string Dummy = "enemy.dummy";
    private const string Strike = "skill.strike";
    private const string Firebolt = "skill.firebolt";

    [Fact]
    public void Without_Registered_Type_Physical_Uses_Legacy_Atk_Minus_Def()
    {
        // No damage types registered → legacy math: max(1, atk + power - def) = max(1, 10 + 5 - 4) = 11
        InMemoryGameDefinitionCatalog defs = new();
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartScratchBattle(defs, heroAtk: 10, dummyDef: 4, skillPower: 5);

        int hpBefore = gameState.ActiveBattle!.Actors[Dummy].Hp;
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Strike, Dummy), CreateContext(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle.Actors[Dummy].Hp;

        Assert.Equal(11, hpBefore - hpAfter);
    }

    [Fact]
    public void Registered_Physical_Applies_Percent_Resistance_On_Top_Of_Flat_Defense()
    {
        // atk=10, def=4, power=5 → afterFlat = 11. res.physical=25% → 11 * 0.75 = 8.25 → 8.
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddDamageType(new DamageTypeDefinition(
            StandardDamageTypes.Physical,
            attackStatId: "atk",
            flatDefenseStatId: "def"));
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartScratchBattle(defs, heroAtk: 10, dummyDef: 4, skillPower: 5);
        gameState.ActorStatsState.GetOrCreate(Dummy).SetBase(StandardStats.ResistancePhysical, 25);

        int hpBefore = gameState.ActiveBattle!.Actors[Dummy].Hp;
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Strike, Dummy), CreateContext(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle.Actors[Dummy].Hp;

        Assert.Equal(8, hpBefore - hpAfter);
    }

    [Fact]
    public void Resistance_At_100_Grants_Immunity_Zero_Damage()
    {
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddDamageType(new DamageTypeDefinition(StandardDamageTypes.Magical, attackStatId: "matk"));
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartScratchBattle(defs, heroMatk: 20, skillPower: 30, skillEffect: BattleSkillEffectType.MagicalDamage, skillId: Firebolt);
        gameState.ActorStatsState.GetOrCreate(Dummy).SetBase(StandardStats.ResistanceMagical, 100);

        int hpBefore = gameState.ActiveBattle!.Actors[Dummy].Hp;
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Firebolt, Dummy), CreateContext(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle.Actors[Dummy].Hp;

        Assert.Equal(0, hpBefore - hpAfter);
    }

    [Fact]
    public void Resistance_Above_100_Still_Capped_At_Immunity()
    {
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddDamageType(new DamageTypeDefinition(StandardDamageTypes.Magical, attackStatId: "matk"));
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartScratchBattle(defs, heroMatk: 20, skillPower: 30, skillEffect: BattleSkillEffectType.MagicalDamage, skillId: Firebolt);
        gameState.ActorStatsState.GetOrCreate(Dummy).SetBase(StandardStats.ResistanceMagical, 150);

        int hpBefore = gameState.ActiveBattle!.Actors[Dummy].Hp;
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Firebolt, Dummy), CreateContext(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle.Actors[Dummy].Hp;

        Assert.Equal(0, hpBefore - hpAfter);
    }

    [Fact]
    public void Negative_Resistance_Multiplies_Damage_For_Vulnerability()
    {
        // matk=10, power=10, no flat defense → afterFlat = 20. res=-50% → 20 * 1.5 = 30.
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddDamageType(new DamageTypeDefinition(StandardDamageTypes.Magical, attackStatId: "matk"));
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartScratchBattle(
            defs, heroMatk: 10, skillPower: 10, skillEffect: BattleSkillEffectType.MagicalDamage, skillId: Firebolt, dummyMaxHp: 200);
        gameState.ActorStatsState.GetOrCreate(Dummy).SetBase(StandardStats.ResistanceMagical, -50);

        int hpBefore = gameState.ActiveBattle!.Actors[Dummy].Hp;
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Firebolt, Dummy), CreateContext(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle.Actors[Dummy].Hp;

        Assert.Equal(30, hpBefore - hpAfter);
    }

    [Fact]
    public void Per_Skill_Damage_Type_Override_Routes_To_Custom_Type()
    {
        // Firebolt is a magical-effect skill but declares damageTypeId="fire". The runtime
        // should resolve via the "fire" damage type definition, not "magical".
        // matk=10, power=10, no flat def → 20 * 0.70 = 14 against 30% fire resistance.
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddDamageType(new DamageTypeDefinition(StandardDamageTypes.Fire, attackStatId: "matk"));
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartScratchBattle(
            defs, heroMatk: 10, skillPower: 10, skillEffect: BattleSkillEffectType.MagicalDamage, skillId: Firebolt, dummyMaxHp: 200,
            skillDamageTypeId: StandardDamageTypes.Fire);

        // Real game would route equipment through EquipItemCommand which pushes this Flat
        // modifier automatically. Inline here to keep the test focused on damage resolution.
        gameState.ActorStatsState.GetOrCreate(Dummy).AddModifier(new StatModifier(
            StandardStats.ResistanceFire,
            StatModifierBucket.Flat,
            30,
            "equipment",
            "slot.armor:item.flame_cloak"));

        int hpBefore = gameState.ActiveBattle!.Actors[Dummy].Hp;
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Firebolt, Dummy), CreateContext(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle.Actors[Dummy].Hp;

        Assert.Equal(14, hpBefore - hpAfter);
    }

    [Fact]
    public void Flat_Defense_That_Out_Tanks_Still_Yields_Minimum_One_Damage()
    {
        // atk=2, power=1, def=99 → afterFlat = -96, no resistance. Min damage 1.
        InMemoryGameDefinitionCatalog defs = new();
        defs.AddDamageType(new DamageTypeDefinition(
            StandardDamageTypes.Physical,
            attackStatId: "atk",
            flatDefenseStatId: "def"));
        (GameState gameState, CommandDispatcher dispatcher, InMemoryDomainEventSink sink) = StartScratchBattle(defs, heroAtk: 2, dummyDef: 99, skillPower: 1);

        int hpBefore = gameState.ActiveBattle!.Actors[Dummy].Hp;
        Assert.True(dispatcher.Dispatch(gameState, new UseBattleSkillCommand(Hero, Strike, Dummy), CreateContext(sink, defs)).IsSuccess);
        int hpAfter = gameState.ActiveBattle.Actors[Dummy].Hp;

        Assert.Equal(1, hpBefore - hpAfter);
    }

    [Fact]
    public void Resistance_StatId_Defaults_To_Res_Prefix_Plus_Id()
    {
        DamageTypeDefinition fire = new(StandardDamageTypes.Fire, attackStatId: "matk");
        Assert.Equal("res.fire", fire.ResistanceStatId);
        Assert.Equal(StandardStats.Resistance(StandardDamageTypes.Fire), fire.ResistanceStatId);
    }

    private static (GameState, CommandDispatcher, InMemoryDomainEventSink) StartScratchBattle(
        InMemoryGameDefinitionCatalog definitions,
        int heroAtk = 0,
        int heroMatk = 0,
        int dummyDef = 0,
        int dummyMaxHp = 100,
        int skillPower = 1,
        BattleSkillEffectType skillEffect = BattleSkillEffectType.PhysicalDamage,
        string skillId = Strike,
        string? skillDamageTypeId = null)
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new StartBattleCommandHandler());
        dispatcher.Register(new UseBattleSkillCommandHandler());
        dispatcher.Register(new ExecuteAiTurnCommandHandler());

        BattleSkillDefinition skill = new(skillId, skillEffect, skillPower, damageTypeId: skillDamageTypeId);
        StartBattleCommand command = new(
            battleId: "battle.test",
            actors:
            [
                new BattleActorDefinition(
                    actorId: Hero,
                    displayName: "Hero",
                    faction: CombatFaction.Party,
                    maxHp: 100,
                    atk: heroAtk,
                    def: 0,
                    matk: heroMatk,
                    mdef: 0,
                    initiative: 20,
                    skillIds: [skillId],
                    playerControlled: true),
                new BattleActorDefinition(
                    actorId: Dummy,
                    displayName: "Dummy",
                    faction: CombatFaction.Enemy,
                    maxHp: dummyMaxHp,
                    atk: 0,
                    def: dummyDef,
                    matk: 0,
                    mdef: 0,
                    initiative: 1,
                    skillIds: ["skill.idle"],
                    playerControlled: false)
            ],
            skills: [skill, new BattleSkillDefinition("skill.idle", BattleSkillEffectType.PhysicalDamage, 0)],
            seed: 1);

        Assert.True(dispatcher.Dispatch(gameState, command, CreateContext(sink, definitions)).IsSuccess);
        return (gameState, dispatcher, sink);
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog definitions)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 42, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
