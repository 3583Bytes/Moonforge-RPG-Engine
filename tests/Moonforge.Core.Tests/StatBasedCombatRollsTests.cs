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
using Moonforge.Core.Stats;

namespace Moonforge.Core.Tests;

public sealed class StatBasedCombatRollsTests
{
    [Fact]
    public void Actor_Crit_Stat_Lifts_A_Zero_Base_Crit_Skill_Into_A_Guaranteed_Crit()
    {
        // Base skill has 0% crit chance. With actor.crit = 100, the effective chance
        // should reach 100% and the hit lands as a critical.
        BattleActionResolvedEvent ev = RunOneAttack(skillCrit: 0, attackerCritStat: 100);
        Assert.True(ev.WasCritical);
    }

    [Fact]
    public void Actor_Crit_Stat_Adds_To_The_Per_Skill_Chance()
    {
        // Same seed, two scenarios — one with actor crit stat = 0, one with crit = 30.
        // Skill base crit = 80% → effective is 80 vs 100 (clamped). At 100% crit, the
        // hit is guaranteed; at 80% the same seed may or may not crit. Make this robust
        // by running once at each end and asserting WasCritical lines up with the
        // effective chance reaching 100%.
        BattleActionResolvedEvent ev = RunOneAttack(skillCrit: 80, attackerCritStat: 30);
        Assert.True(ev.WasCritical);
    }

    [Fact]
    public void Effective_Crit_Chance_Clamped_To_Zero_When_Negative()
    {
        // Cursed item: actor has -100 crit. Skill base 50% crit. Effective = -50 → 0,
        // never crits.
        BattleActionResolvedEvent ev = RunOneAttack(skillCrit: 50, attackerCritStat: -100);
        Assert.False(ev.WasCritical);
    }

    [Fact]
    public void Critdmg_Stat_Shifts_The_Crit_Damage_Multiplier()
    {
        int baseCritAmount = RunOneAttack(skillCrit: 100, attackerCritStat: 0, critDmgStat: 0).Amount;
        int boostedCritAmount = RunOneAttack(skillCrit: 100, attackerCritStat: 0, critDmgStat: 100).Amount;

        // Base: 200% multiplier (default). Boosted: 200 + 100 = 300%. So boosted should
        // be 1.5× the base crit (with identical seed and no variance).
        Assert.Equal(baseCritAmount * 3 / 2, boostedCritAmount);
    }

    [Fact]
    public void Actor_Accuracy_Stat_Recovers_Skill_Accuracy()
    {
        // Skill has 0% accuracy → always misses by default. Actor with +100 acc clamps
        // back to 100, so the hit lands.
        InMemoryDomainEventSink sink = RunBattleAndDrain(skillAccuracy: 0, attackerAccStat: 100);
        Assert.DoesNotContain(sink.Events, e => e is BattleActionMissedEvent);
        Assert.Contains(sink.Events, e => e is BattleActionResolvedEvent r && r.SkillId == "skill.test" && !r.WasHeal);
    }

    [Fact]
    public void Target_Evasion_Reduces_Effective_Accuracy()
    {
        // Skill 100% accuracy, target +100 evasion → effective 0, always misses.
        InMemoryDomainEventSink sink = RunBattleAndDrain(skillAccuracy: 100, targetEvaStat: 100);
        Assert.Contains(sink.Events, e => e is BattleActionMissedEvent);
        Assert.DoesNotContain(sink.Events, e => e is BattleActionResolvedEvent r && r.SkillId == "skill.test" && !r.WasHeal);
    }

    private static BattleActionResolvedEvent RunOneAttack(int skillCrit, int attackerCritStat, int critDmgStat = 0)
    {
        InMemoryDomainEventSink sink = RunBattleAndDrain(
            skillAccuracy: 100,
            skillCrit: skillCrit,
            attackerCritStat: attackerCritStat,
            critDmgStat: critDmgStat);

        return sink.Events.OfType<BattleActionResolvedEvent>().Single(e => e.SkillId == "skill.test" && !e.WasHeal);
    }

    private static InMemoryDomainEventSink RunBattleAndDrain(
        int skillAccuracy = 100,
        int skillCrit = 0,
        int attackerAccStat = 0,
        int attackerCritStat = 0,
        int critDmgStat = 0,
        int targetEvaStat = 0)
    {
        GameState gameState = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new StartBattleCommandHandler());
        dispatcher.Register(new UseBattleSkillCommandHandler());

        BattleSkillDefinition skill = new(
            "skill.test",
            BattleSkillEffectType.PhysicalDamage,
            power: 10,
            accuracyPercent: skillAccuracy,
            critChancePercent: skillCrit);

        BattleActorDefinition heroDef = new(
            actorId: "party.hero",
            displayName: "Hero",
            faction: CombatFaction.Party,
            maxHp: 100,
            atk: 10,
            def: 0,
            matk: 0,
            mdef: 0,
            initiative: 20,
            skillIds: ["skill.test"],
            playerControlled: true);

        BattleActorDefinition goblinDef = new(
            actorId: "enemy.goblin",
            displayName: "Goblin",
            faction: CombatFaction.Enemy,
            maxHp: 100,
            atk: 0,
            def: 0,
            matk: 0,
            mdef: 0,
            initiative: 5,
            skillIds: ["skill.test"]);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new StartBattleCommand("battle.stat", [heroDef, goblinDef], [skill], seed: 1, sequence: 1),
            BuildContext(sink)).IsSuccess);

        // Seed stat bonuses AFTER battle starts so the stat block exists.
        if (attackerAccStat != 0) gameState.ActorStatsState.GetOrCreate("party.hero").SetBase(StandardStats.Accuracy, attackerAccStat);
        if (attackerCritStat != 0) gameState.ActorStatsState.GetOrCreate("party.hero").SetBase(StandardStats.CritChance, attackerCritStat);
        if (critDmgStat != 0) gameState.ActorStatsState.GetOrCreate("party.hero").SetBase(StandardStats.CritDamage, critDmgStat);
        if (targetEvaStat != 0) gameState.ActorStatsState.GetOrCreate("enemy.goblin").SetBase(StandardStats.Evasion, targetEvaStat);

        Assert.True(dispatcher.Dispatch(
            gameState,
            new UseBattleSkillCommand("party.hero", "skill.test", "enemy.goblin"),
            BuildContext(sink)).IsSuccess);

        return sink;
    }

    private static CommandContext BuildContext(InMemoryDomainEventSink sink)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 999, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            new InMemoryGameDefinitionCatalog().AddCurrency(new CurrencyDefinition("currency.gold", 999)));
    }
}
