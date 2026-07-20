using System.Collections.Generic;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Combat.Commands
{

    // Status effects: action prevention, post-skill application, per-turn ticking, and the
    // per-turn cooldown/resource refresh that ticking is bundled with.
    internal sealed partial class BattleRuntime
    {
        private static bool IsActorPrevented(BattleActorState actor, CommandContext context, out string statusId)
        {
            // Sort so the *reported* status is deterministic when multiple prevent-action
            // statuses are active (the prevented/not-prevented outcome never depends on order).
            List<string> statusKeys = new(actor.ActiveStatusEffects.Keys);
            statusKeys.Sort(System.StringComparer.Ordinal);

            foreach (string key in statusKeys)
            {
                ActiveStatusEffect effect = actor.ActiveStatusEffects[key];
                if (context.Definitions.TryGetStatusEffect(effect.StatusId, out StatusEffectDefinition def)
                    && def.PreventsAction)
                {
                    statusId = effect.StatusId;
                    return true;
                }
            }

            statusId = string.Empty;
            return false;
        }

        private static void ApplyStatusApplicationsAfterSkill(
            GameState gameState,
            BattleState battle,
            BattleActorState attacker,
            BattleActorState target,
            BattleSkillDefinition skill,
            CommandContext context)
        {
            if (skill.AppliesStatuses.Count == 0)
            {
                return;
            }

            foreach (StatusApplicationDefinition application in skill.AppliesStatuses)
            {
                BattleActorState recipient = application.TargetMode == StatusApplicationTarget.Self ? attacker : target;
                if (recipient.IsDowned)
                {
                    continue;
                }

                int roll = battle.RngState.NextInt(100);
                if (roll >= application.ChancePercent)
                {
                    continue;
                }

                if (!context.Definitions.TryGetStatusEffect(application.StatusId, out StatusEffectDefinition definition))
                {
                    continue;
                }

                int duration = application.DurationOverride ?? definition.DurationTurns;
                if (duration <= 0)
                {
                    continue;
                }

                if (recipient.ActiveStatusEffects.TryGetValue(application.StatusId, out ActiveStatusEffect existing))
                {
                    if (definition.StackPolicy == StatusStackPolicy.IgnoreIfPresent)
                    {
                        continue;
                    }

                    existing.RemainingTurns = duration;
                }
                else
                {
                    recipient.ActiveStatusEffects[application.StatusId] = new ActiveStatusEffect(application.StatusId, duration, attacker.ActorId);
                    StatusStatModifierMirror.Apply(gameState, recipient.ActorId, definition);
                }

                context.EventSink.Publish(new StatusAppliedEvent(recipient.ActorId, application.StatusId, duration, attacker.ActorId));
            }
        }

        private static void TickStatuses(GameState gameState, BattleActorState actor, CommandContext context)
        {
            if (actor.ActiveStatusEffects.Count == 0)
            {
                return;
            }

            List<string> statusKeys = new(actor.ActiveStatusEffects.Keys);
            foreach (string statusId in statusKeys)
            {
                if (!actor.ActiveStatusEffects.TryGetValue(statusId, out ActiveStatusEffect effect))
                {
                    continue;
                }

                if (!context.Definitions.TryGetStatusEffect(statusId, out StatusEffectDefinition definition))
                {
                    continue;
                }

                int hpDelta = 0;
                if (definition.TickHpDelta != 0 && !actor.IsDowned)
                {
                    int previousHp = actor.Hp;
                    int maxHp = GetEffectiveStat(gameState, actor, "maxhp", actor.MaxHp, context);
                    int next = actor.Hp + definition.TickHpDelta;
                    if (next < 0) next = 0;
                    if (next > maxHp) next = maxHp;
                    actor.Hp = next;
                    hpDelta = next - previousHp;
                }

                effect.RemainingTurns--;
                if (effect.RemainingTurns <= 0)
                {
                    actor.ActiveStatusEffects.Remove(statusId);
                    StatusStatModifierMirror.Remove(gameState, actor.ActorId, statusId);
                    if (hpDelta != 0)
                    {
                        context.EventSink.Publish(new StatusTickedEvent(actor.ActorId, statusId, hpDelta, 0));
                    }
                    context.EventSink.Publish(new StatusExpiredEvent(actor.ActorId, statusId));
                }
                else
                {
                    context.EventSink.Publish(new StatusTickedEvent(actor.ActorId, statusId, hpDelta, effect.RemainingTurns));
                }
            }
        }

        private static void ApplyTurnRefresh(GameState gameState, BattleActorState actor, CommandContext context)
        {
            List<string> cooldownKeys = new(actor.Cooldowns.Keys);
            foreach (string skillId in cooldownKeys)
            {
                int next = actor.Cooldowns[skillId] - 1;
                if (next <= 0)
                {
                    actor.Cooldowns.Remove(skillId);
                }
                else
                {
                    actor.Cooldowns[skillId] = next;
                }
            }

            foreach (KeyValuePair<string, int> refresh in actor.ResourceRefreshPerTurn)
            {
                int max = actor.ResourceMaxes.TryGetValue(refresh.Key, out int maxValue) ? maxValue : int.MaxValue;
                int current = actor.Resources.TryGetValue(refresh.Key, out int amount) ? amount : 0;
                int next = current + refresh.Value;
                if (next > max)
                {
                    next = max;
                }

                if (next < 0)
                {
                    next = 0;
                }

                actor.Resources[refresh.Key] = next;
            }

            TickStatuses(gameState, actor, context);
        }
    }
}
