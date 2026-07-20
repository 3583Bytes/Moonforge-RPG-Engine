using System;
using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands
{

    // Skill resolution: validation, targeting, damage/heal application, and cost consumption.
    internal sealed partial class BattleRuntime
    {
        private static DomainResult TryApplySkill(
            GameState gameState,
            BattleState battle,
            string actorId,
            string skillId,
            string targetActorId,
            CommandContext context)
        {
            if (!battle.TryGetActor(actorId, out BattleActorState actor))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Actor '{actorId}' not found."));
            }

            if (actor.IsDowned)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, $"Actor '{actorId}' is downed."));
            }

            if (!actor.SkillIds.Contains(skillId, StringComparer.Ordinal))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    $"Actor '{actorId}' does not know skill '{skillId}'."));
            }

            if (!battle.TryGetSkill(skillId, out BattleSkillDefinition skill))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Skill '{skillId}' not found."));
            }

            if (actor.Cooldowns.TryGetValue(skillId, out int cooldownRemaining) && cooldownRemaining > 0)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Skill '{skillId}' is on cooldown for {cooldownRemaining} more turn(s)."));
            }

            if (skill.MaxPp > 0)
            {
                int currentPp = actor.SkillPp.TryGetValue(skillId, out int pp) ? pp : 0;
                if (currentPp <= 0)
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.InsufficientResources,
                        $"Skill '{skillId}' is out of PP."));
                }
            }

            foreach (KeyValuePair<string, int> cost in skill.ResourceCosts)
            {
                int available = actor.Resources.TryGetValue(cost.Key, out int amount) ? amount : 0;
                if (available < cost.Value)
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.InsufficientResources,
                        $"Skill '{skillId}' requires {cost.Value} {cost.Key} but actor has {available}."));
                }
            }

            List<BattleActorState> targets = ResolveTargets(battle, actor, skill, targetActorId, out DomainError? targetError);
            if (targetError is not null)
            {
                return DomainResult.Fail(targetError);
            }

            if (targets.Count == 0)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    $"Skill '{skillId}' has no valid targets."));
            }

            foreach (BattleActorState target in targets)
            {
                ApplySkillToTarget(gameState, battle, actor, target, skill, context);
            }

            ConsumeSkillCosts(actor, skill, context);
            return DomainResult.Success();
        }

        private static List<BattleActorState> ResolveTargets(
            BattleState battle,
            BattleActorState actor,
            BattleSkillDefinition skill,
            string explicitTargetId,
            out DomainError? error)
        {
            error = null;
            List<BattleActorState> results = new();

            switch (skill.TargetMode)
            {
                case BattleSkillTargetMode.Single:
                {
                    if (!battle.TryGetActor(explicitTargetId, out BattleActorState target))
                    {
                        error = new DomainError(DomainErrorCode.NotFound, $"Target actor '{explicitTargetId}' not found.");
                        return results;
                    }

                    if (!IsValidTarget(actor, target, skill))
                    {
                        error = new DomainError(
                            DomainErrorCode.ValidationFailed,
                            $"Target '{explicitTargetId}' is invalid for skill '{skill.Id}'.");
                        return results;
                    }

                    results.Add(target);
                    return results;
                }

                case BattleSkillTargetMode.Self:
                {
                    if (IsValidTarget(actor, actor, skill))
                    {
                        results.Add(actor);
                    }

                    return results;
                }

                case BattleSkillTargetMode.AllAllies:
                case BattleSkillTargetMode.AllEnemies:
                case BattleSkillTargetMode.AllOthers:
                {
                    List<BattleActorState> ordered = battle.TurnOrder
                        .Select(id => battle.Actors[id])
                        .ToList();

                    foreach (BattleActorState candidate in ordered)
                    {
                        bool matches = skill.TargetMode switch
                        {
                            BattleSkillTargetMode.AllAllies => candidate.Faction == actor.Faction,
                            BattleSkillTargetMode.AllEnemies => candidate.Faction != actor.Faction,
                            BattleSkillTargetMode.AllOthers => candidate.ActorId != actor.ActorId,
                            _ => false
                        };

                        if (!matches)
                        {
                            continue;
                        }

                        if (!IsValidTarget(actor, candidate, skill))
                        {
                            continue;
                        }

                        results.Add(candidate);
                    }

                    return results;
                }

                default:
                    error = new DomainError(
                        DomainErrorCode.UnsupportedOperation,
                        $"Unsupported target mode '{skill.TargetMode}'.");
                    return results;
            }
        }

        private static void ApplySkillToTarget(
            GameState gameState,
            BattleState battle,
            BattleActorState actor,
            BattleActorState target,
            BattleSkillDefinition skill,
            CommandContext context)
        {
            int effectiveAccuracy = skill.AccuracyPercent
                + GetEffectiveStatSigned(gameState, actor, "acc", 0, context)
                - GetEffectiveStatSigned(gameState, target, "eva", 0, context);
            if (effectiveAccuracy > 100) effectiveAccuracy = 100;
            if (effectiveAccuracy < 0) effectiveAccuracy = 0;

            if (effectiveAccuracy < 100)
            {
                int accuracyRoll = battle.RngState.NextInt(100);
                if (accuracyRoll >= effectiveAccuracy)
                {
                    context.EventSink.Publish(new BattleActionMissedEvent(
                        battle.BattleId,
                        actor.ActorId,
                        skill.Id,
                        target.ActorId));
                    return;
                }
            }

            switch (skill.EffectType)
            {
                case BattleSkillEffectType.Heal:
                {
                    int healAmount = Math.Max(1, GetEffectiveStat(gameState, actor, "matk", actor.Matk, context) + skill.Power);
                    healAmount = ApplyVariance(battle, healAmount, skill.DamageVariancePercent);
                    int maxHp = GetEffectiveStat(gameState, target, "maxhp", target.MaxHp, context);
                    int previous = target.Hp;
                    int next = Math.Min(maxHp, previous + healAmount);
                    target.Hp = next;
                    context.EventSink.Publish(new BattleActionResolvedEvent(
                        battle.BattleId,
                        actor.ActorId,
                        skill.Id,
                        target.ActorId,
                        next - previous,
                        wasHeal: true));
                    break;
                }

                case BattleSkillEffectType.PhysicalDamage:
                case BattleSkillEffectType.MagicalDamage:
                {
                    int damage;
                    if (skill.EffectType == BattleSkillEffectType.PhysicalDamage)
                    {
                        damage = ResolveTypedDamage(
                            gameState, actor, target, skill, context,
                            damageTypeId: skill.DamageTypeId ?? StandardDamageTypes.Physical,
                            legacyAttackStatId: "atk", legacyAttackScalar: actor.Atk,
                            legacyDefenseStatId: "def", legacyDefenseScalar: target.Def);
                    }
                    else
                    {
                        damage = ResolveTypedDamage(
                            gameState, actor, target, skill, context,
                            damageTypeId: skill.DamageTypeId ?? StandardDamageTypes.Magical,
                            legacyAttackStatId: "matk", legacyAttackScalar: actor.Matk,
                            legacyDefenseStatId: "mdef", legacyDefenseScalar: target.Mdef);
                    }

                    bool wasCritical = false;
                    if (damage > 0)
                    {
                        int effectiveCritChance = skill.CritChancePercent
                            + GetEffectiveStatSigned(gameState, actor, "crit", 0, context);
                        if (effectiveCritChance > 100) effectiveCritChance = 100;
                        if (effectiveCritChance < 0) effectiveCritChance = 0;

                        if (effectiveCritChance > 0)
                        {
                            int critRoll = battle.RngState.NextInt(100);
                            if (critRoll < effectiveCritChance)
                            {
                                wasCritical = true;
                                int effectiveCritMultiplier = skill.CritMultiplierPercent
                                    + GetEffectiveStatSigned(gameState, actor, "critdmg", 0, context);
                                if (effectiveCritMultiplier < 100) effectiveCritMultiplier = 100;
                                damage = (int)Math.Round(damage * (effectiveCritMultiplier / 100.0), MidpointRounding.AwayFromZero);
                            }
                        }

                        damage = ApplyVariance(battle, damage, skill.DamageVariancePercent);
                        if (damage < 1)
                        {
                            damage = 1;
                        }
                    }

                    int previousHp = target.Hp;
                    target.Hp = Math.Max(0, target.Hp - damage);
                    context.EventSink.Publish(new BattleActionResolvedEvent(
                        battle.BattleId,
                        actor.ActorId,
                        skill.Id,
                        target.ActorId,
                        damage,
                        wasHeal: false,
                        wasCritical: wasCritical));

                    if (previousHp > 0 && target.Hp == 0)
                    {
                        context.EventSink.Publish(new QuestSignalEvent(QuestSignalType.Kill, target.ActorId, 1));
                    }

                    break;
                }

                case BattleSkillEffectType.Buff:
                case BattleSkillEffectType.Debuff:
                    // No HP change; statuses applied below carry the effect.
                    break;
            }

            ApplyStatusApplicationsAfterSkill(gameState, battle, actor, target, skill, context);
        }

        private static int ApplyVariance(BattleState battle, int baseAmount, int variancePercent)
        {
            if (variancePercent <= 0 || baseAmount == 0)
            {
                return baseAmount;
            }

            int range = (variancePercent * 2) + 1;
            int offset = battle.RngState.NextInt(range) - variancePercent;
            int multiplier = 100 + offset;
            double scaled = (baseAmount * multiplier) / 100.0;
            return (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        }

        private static int ResolveTypedDamage(
            GameState gameState,
            BattleActorState actor,
            BattleActorState target,
            BattleSkillDefinition skill,
            CommandContext context,
            string damageTypeId,
            string legacyAttackStatId,
            int legacyAttackScalar,
            string legacyDefenseStatId,
            int legacyDefenseScalar)
        {
            if (!context.Definitions.TryGetDamageType(damageTypeId, out DamageTypeDefinition typeDef))
            {
                // Legacy path: pre-stat-system math, preserved exactly.
                int attacker = GetEffectiveStat(gameState, actor, legacyAttackStatId, legacyAttackScalar, context);
                int defender = GetEffectiveStat(gameState, target, legacyDefenseStatId, legacyDefenseScalar, context);
                return Math.Max(1, attacker + skill.Power - defender);
            }

            int atk = GetEffectiveStat(gameState, actor, typeDef.AttackStatId, LegacyScalarFor(actor, typeDef.AttackStatId), context);
            int rawAttack = atk + skill.Power;

            int flatDefense = 0;
            if (!string.IsNullOrWhiteSpace(typeDef.FlatDefenseStatId))
            {
                flatDefense = GetEffectiveStat(gameState, target, typeDef.FlatDefenseStatId!, LegacyScalarFor(target, typeDef.FlatDefenseStatId!), context);
            }

            int afterFlat = rawAttack - flatDefense;
            int resistance = GetEffectiveStatSigned(gameState, target, typeDef.ResistanceStatId, 0, context);
            if (resistance >= 100)
            {
                return 0; // hard immunity cap
            }

            double scaled = afterFlat * (100.0 - resistance) / 100.0;

            // Type-chart effectiveness layers on top of percent resistance. A 0× chart entry
            // grants full immunity even if the percent resistance stat is zero.
            if (!string.IsNullOrWhiteSpace(typeDef.EffectivenessChartId)
                && context.Definitions.TryGetTypeEffectivenessChart(typeDef.EffectivenessChartId!, out TypeEffectivenessChartDefinition chart))
            {
                int multiplierPercent = chart.GetMultiplierPercent(typeDef.Id, target.DefenderTypeIds);
                if (multiplierPercent == 0)
                {
                    return 0;
                }

                scaled = scaled * multiplierPercent / 100.0;
            }

            int rounded = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
            return Math.Max(1, rounded);
        }

        private static bool IsValidTarget(BattleActorState actor, BattleActorState target, BattleSkillDefinition skill)
        {
            bool sameFaction = actor.Faction == target.Faction;

            switch (skill.EffectType)
            {
                case BattleSkillEffectType.Heal:
                    // Single-target heal: must not be at full HP. AoE heal: pre-filter
                    // skips full-HP allies silently so the cast still succeeds for everyone else.
                    return sameFaction && target.Hp < target.MaxHp;
                case BattleSkillEffectType.PhysicalDamage:
                case BattleSkillEffectType.MagicalDamage:
                    return !sameFaction && !target.IsDowned;
                case BattleSkillEffectType.Buff:
                    return sameFaction && !target.IsDowned;
                case BattleSkillEffectType.Debuff:
                    return !sameFaction && !target.IsDowned;
                default:
                    return false;
            }
        }

        private static void ConsumeSkillCosts(BattleActorState actor, BattleSkillDefinition skill, CommandContext context)
        {
            foreach (KeyValuePair<string, int> cost in skill.ResourceCosts)
            {
                int remaining = (actor.Resources.TryGetValue(cost.Key, out int amount) ? amount : 0) - cost.Value;
                actor.Resources[cost.Key] = remaining < 0 ? 0 : remaining;
            }

            if (skill.MaxPp > 0)
            {
                int currentPp = actor.SkillPp.TryGetValue(skill.Id, out int pp) ? pp : skill.MaxPp;
                int next = currentPp - 1;
                if (next < 0) next = 0;
                actor.SkillPp[skill.Id] = next;
                if (next == 0)
                {
                    context.EventSink.Publish(new Events.SkillPpDepletedEvent(actor.ActorId, skill.Id));
                }
            }

            if (skill.CooldownTurns > 0)
            {
                actor.Cooldowns[skill.Id] = skill.CooldownTurns;
            }
        }
    }
}
