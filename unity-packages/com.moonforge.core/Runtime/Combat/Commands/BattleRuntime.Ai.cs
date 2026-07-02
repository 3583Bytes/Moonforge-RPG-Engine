using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Data.Definitions;

namespace Moonforge.Core.Combat.Commands
{

    // AI turn selection: pick a rule (or fallback) whose conditions hold and that resolves a
    // valid target under its target policy. All ordering is ordinally tie-broken and all
    // random target picks go through battle.RngState so AI turns stay deterministic.
    internal sealed partial class BattleRuntime
    {
        private static bool TrySelectAiAction(
            BattleState battle,
            BattleActorState actor,
            out string skillId,
            out string targetActorId)
        {
            BattleAiPolicyDefinition policy = actor.AiPolicy ?? new BattleAiPolicyDefinition(
                rules: null,
                fallbackSkillId: actor.SkillIds.FirstOrDefault(),
                fallbackTargetPolicy: BattleAiTargetPolicy.LowestHpEnemy);

            List<BattleAiRuleDefinition> orderedRules = policy.Rules
                .OrderByDescending(x => x.PriorityWeight)
                .ThenBy(x => x.SkillId, System.StringComparer.Ordinal)
                .ToList();

            foreach (BattleAiRuleDefinition rule in orderedRules)
            {
                if (!actor.SkillIds.Contains(rule.SkillId, System.StringComparer.Ordinal))
                {
                    continue;
                }

                if (IsSkillOnCooldown(actor, rule.SkillId))
                {
                    continue;
                }

                if (!EvaluateAiConditions(battle, actor, rule.Conditions))
                {
                    continue;
                }

                if (!TryResolveTargetForPolicy(battle, actor, rule.SkillId, rule.TargetPolicy, out string resolvedTarget))
                {
                    continue;
                }

                skillId = rule.SkillId;
                targetActorId = resolvedTarget;
                return true;
            }

            string fallbackSkillId = policy.FallbackSkillId ?? actor.SkillIds.FirstOrDefault() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fallbackSkillId)
                && !IsSkillOnCooldown(actor, fallbackSkillId)
                && TryResolveTargetForPolicy(battle, actor, fallbackSkillId, policy.FallbackTargetPolicy, out string fallbackTarget))
            {
                skillId = fallbackSkillId;
                targetActorId = fallbackTarget;
                return true;
            }

            skillId = string.Empty;
            targetActorId = string.Empty;
            return false;
        }

        private static bool IsSkillOnCooldown(BattleActorState actor, string skillId)
        {
            return actor.Cooldowns.TryGetValue(skillId, out int remaining) && remaining > 0;
        }

        private static bool EvaluateAiConditions(
            BattleState battle,
            BattleActorState actor,
            IReadOnlyList<BattleAiConditionDefinition> conditions)
        {
            foreach (BattleAiConditionDefinition condition in conditions)
            {
                if (!EvaluateAiCondition(battle, actor, condition))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateAiCondition(BattleState battle, BattleActorState actor, BattleAiConditionDefinition condition)
        {
            switch (condition.Type)
            {
                case BattleAiConditionType.SelfHpBelowPercent:
                    return GetHpPercent(actor) <= condition.ThresholdPercent;

                case BattleAiConditionType.AnyAllyHpBelowPercent:
                    return battle.Actors.Values.Any(x =>
                        x.Faction == actor.Faction &&
                        !x.IsDowned &&
                        GetHpPercent(x) <= condition.ThresholdPercent);

                case BattleAiConditionType.AnyEnemyHpBelowPercent:
                    return battle.Actors.Values.Any(x =>
                        x.Faction != actor.Faction &&
                        !x.IsDowned &&
                        GetHpPercent(x) <= condition.ThresholdPercent);

                default:
                    return false;
            }
        }

        private static bool TryResolveTargetForPolicy(
            BattleState battle,
            BattleActorState actor,
            string skillId,
            BattleAiTargetPolicy targetPolicy,
            out string targetActorId)
        {
            if (!battle.TryGetSkill(skillId, out BattleSkillDefinition skill))
            {
                targetActorId = string.Empty;
                return false;
            }

            IEnumerable<BattleActorState> candidates;
            switch (targetPolicy)
            {
                case BattleAiTargetPolicy.LowestHpEnemy:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction != actor.Faction && !x.IsDowned)
                        .OrderBy(x => x.Hp)
                        .ThenBy(x => x.ActorId, System.StringComparer.Ordinal);
                    break;

                case BattleAiTargetPolicy.HighestThreatEnemy:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction != actor.Faction && !x.IsDowned)
                        .OrderByDescending(x => x.Atk + x.Matk)
                        .ThenBy(x => x.ActorId, System.StringComparer.Ordinal);
                    break;

                case BattleAiTargetPolicy.LowestHpAlly:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction == actor.Faction)
                        .OrderBy(x => x.Hp)
                        .ThenBy(x => x.ActorId, System.StringComparer.Ordinal);
                    break;

                case BattleAiTargetPolicy.Self:
                    candidates = new[] { actor };
                    break;

                case BattleAiTargetPolicy.RandomEnemy:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction != actor.Faction && !x.IsDowned)
                        .OrderBy(x => x.ActorId, System.StringComparer.Ordinal);
                    break;

                case BattleAiTargetPolicy.RandomAlly:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction == actor.Faction)
                        .OrderBy(x => x.ActorId, System.StringComparer.Ordinal);
                    break;

                default:
                    targetActorId = string.Empty;
                    return false;
            }

            List<BattleActorState> filtered = candidates.Where(x => IsValidTarget(actor, x, skill)).ToList();
            if (filtered.Count == 0)
            {
                targetActorId = string.Empty;
                return false;
            }

            if (targetPolicy == BattleAiTargetPolicy.RandomEnemy || targetPolicy == BattleAiTargetPolicy.RandomAlly)
            {
                int index = battle.RngState.NextInt(filtered.Count);
                targetActorId = filtered[index].ActorId;
                return true;
            }

            targetActorId = filtered[0].ActorId;
            return true;
        }

        private static double GetHpPercent(BattleActorState actor)
        {
            if (actor.MaxHp <= 0)
            {
                return 0;
            }

            return (actor.Hp * 100.0) / actor.MaxHp;
        }
    }
}
