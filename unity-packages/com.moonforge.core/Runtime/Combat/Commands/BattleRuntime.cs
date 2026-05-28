using System;
using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Loot.Commands;
using Moonforge.Core.Progression;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Combat.Commands
{

    internal sealed class BattleRuntime
    {
        public static readonly BattleRuntime Instance = new();
        private readonly EconomyTransactionCommandHandler _rewardTransactionHandler = new();
        private readonly GrantExperienceCommandHandler _experienceGrantHandler = new();
        private readonly RollAndGrantLootCommandHandler _lootHandler = new();

        private BattleRuntime()
        {
        }

        public DomainResult ResolvePlayerAction(
            GameState gameState,
            UseBattleSkillCommand command,
            CommandContext context)
        {
            if (!TryGetActiveBattle(gameState, out BattleState battle, out DomainError? error))
            {
                return DomainResult.Fail(error!);
            }

            if (battle.Status != BattleStatus.Active)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Battle is not active."));
            }

            NormalizeTurnForDownedActors(gameState, battle, context);
            BattleActorState actor = GetCurrentTurnActor(battle);
            if (actor.ActorId != command.ActorId)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{command.ActorId}' is not the current turn actor ('{actor.ActorId}')."));
            }

            if (!actor.PlayerControlled)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{actor.ActorId}' is AI controlled. Use ExecuteAiTurnCommand."));
            }

            if (IsActorPrevented(actor, context, out string preventStatus))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{actor.ActorId}' is prevented from acting by status '{preventStatus}'."));
            }

            DomainResult actionResult = TryApplySkill(gameState, battle, actor.ActorId, command.SkillId, command.TargetActorId, context);
            if (!actionResult.IsSuccess)
            {
                return actionResult;
            }

            DomainResult endResult = UpdateBattleEndState(gameState, battle, context);
            if (!endResult.IsSuccess)
            {
                return endResult;
            }

            AdvanceTurnIfNeeded(gameState, battle, context);
            return DomainResult.Success();
        }

        public DomainResult ResolveAiTurn(GameState gameState, CommandContext context)
        {
            if (!TryGetActiveBattle(gameState, out BattleState battle, out DomainError? error))
            {
                return DomainResult.Fail(error!);
            }

            if (battle.Status != BattleStatus.Active)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Battle is not active."));
            }

            NormalizeTurnForDownedActors(gameState, battle, context);
            BattleActorState actor = GetCurrentTurnActor(battle);
            if (actor.PlayerControlled)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{actor.ActorId}' is player controlled. Use UseBattleSkillCommand."));
            }

            if (IsActorPrevented(actor, context, out string preventStatus))
            {
                context.EventSink.Publish(new StatusPreventedActionEvent(actor.ActorId, preventStatus));
                AdvanceTurnIfNeeded(gameState, battle, context);
                return DomainResult.Success();
            }

            if (!TrySelectAiAction(battle, actor, out string skillId, out string targetActorId))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.UnsupportedOperation,
                    $"AI actor '{actor.ActorId}' has no valid action/target."));
            }

            DomainResult actionResult = TryApplySkill(gameState, battle, actor.ActorId, skillId, targetActorId, context);
            if (!actionResult.IsSuccess)
            {
                return actionResult;
            }

            DomainResult endResult = UpdateBattleEndState(gameState, battle, context);
            if (!endResult.IsSuccess)
            {
                return endResult;
            }

            AdvanceTurnIfNeeded(gameState, battle, context);
            return DomainResult.Success();
        }

        public DomainResult ResolveCapture(GameState gameState, AttemptCaptureCommand command, CommandContext context)
        {
            if (!TryGetActiveBattle(gameState, out BattleState battle, out DomainError? error))
            {
                return DomainResult.Fail(error!);
            }

            if (battle.Status != BattleStatus.Active)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Battle is not active."));
            }

            if (string.IsNullOrWhiteSpace(command.ActorId) || string.IsNullOrWhiteSpace(command.TargetActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Capturer and target ids are required."));
            }

            NormalizeTurnForDownedActors(gameState, battle, context);
            BattleActorState capturer = GetCurrentTurnActor(battle);
            if (capturer.ActorId != command.ActorId)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{command.ActorId}' is not the current turn actor ('{capturer.ActorId}')."));
            }

            if (capturer.Faction != CombatFaction.Party)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.UnsupportedOperation,
                    "Only party actors can attempt captures."));
            }

            if (IsActorPrevented(capturer, context, out string preventStatus))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{capturer.ActorId}' is prevented from acting by status '{preventStatus}'."));
            }

            if (!battle.TryGetActor(command.TargetActorId, out BattleActorState target))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Target actor '{command.TargetActorId}' not found."));
            }

            if (target.Faction != CombatFaction.Enemy)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.UnsupportedOperation,
                    "Only enemy actors can be captured."));
            }

            if (target.IsDowned)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Target '{target.ActorId}' is downed and cannot be captured."));
            }

            if (target.CaptureBaseRate <= 0)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.UnsupportedOperation,
                    $"Target '{target.ActorId}' cannot be captured (capture base rate is 0)."));
            }

            // hpFactor: 1.0 at 0 HP, 1/3 at full HP — same shape as the Gen 1 Pokemon formula.
            int maxHp = target.MaxHp <= 0 ? 1 : target.MaxHp;
            int hp = target.Hp < 0 ? 0 : target.Hp;
            double hpFactor = (3.0 * maxHp - 2.0 * hp) / (3.0 * maxHp);
            double rawChance = target.CaptureBaseRate * hpFactor * (command.BonusPercent / 100.0);
            if (rawChance < 0) rawChance = 0;
            if (rawChance > 100) rawChance = 100;
            int chancePercent = (int)System.Math.Round(rawChance, System.MidpointRounding.AwayFromZero);

            int roll = battle.RngState.NextInt(100);
            bool captured = roll < chancePercent;

            if (!captured)
            {
                context.EventSink.Publish(new CaptureAttemptFailedEvent(battle.BattleId, capturer.ActorId, target.ActorId, chancePercent));
                AdvanceTurnIfNeeded(gameState, battle, context);
                return DomainResult.Success();
            }

            int capturedHp = target.Hp;
            int capturedMaxHp = target.MaxHp;
            string? capturedSpeciesId = target.SpeciesId;
            Dictionary<string, int>? capturedSkillPp = null;
            if (target.SkillPp.Count > 0)
            {
                capturedSkillPp = new Dictionary<string, int>(target.SkillPp.Count, StringComparer.Ordinal);
                foreach (KeyValuePair<string, int> pair in target.SkillPp)
                {
                    capturedSkillPp[pair.Key] = pair.Value;
                }
            }

            battle.RemoveActor(target.ActorId);

            context.EventSink.Publish(new BattleActorCapturedEvent(
                battle.BattleId,
                capturer.ActorId,
                target.ActorId,
                capturedHp,
                capturedMaxHp,
                capturedSpeciesId,
                capturedSkillPp));

            DomainResult endResult = UpdateBattleEndState(gameState, battle, context);
            if (!endResult.IsSuccess)
            {
                return endResult;
            }

            AdvanceTurnIfNeeded(gameState, battle, context);
            return DomainResult.Success();
        }

        public DomainResult ResolveSwap(GameState gameState, SwapBattleActorCommand command, CommandContext context)
        {
            if (!TryGetActiveBattle(gameState, out BattleState battle, out DomainError? error))
            {
                return DomainResult.Fail(error!);
            }

            if (battle.Status != BattleStatus.Active)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Battle is not active."));
            }

            if (command.InActor is null)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Incoming actor definition is required."));
            }

            if (string.IsNullOrWhiteSpace(command.OutActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Outgoing actor id is required."));
            }

            NormalizeTurnForDownedActors(gameState, battle, context);
            BattleActorState currentActor = GetCurrentTurnActor(battle);
            if (currentActor.ActorId != command.OutActorId)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{command.OutActorId}' is not the current turn actor ('{currentActor.ActorId}')."));
            }

            if (currentActor.Faction != CombatFaction.Party)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.UnsupportedOperation,
                    "Only party actors can be swapped."));
            }

            if (IsActorPrevented(currentActor, context, out string preventStatus))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Actor '{currentActor.ActorId}' is prevented from acting by status '{preventStatus}'."));
            }

            if (command.InActor.Faction != CombatFaction.Party)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    "Incoming actor must be on the party faction."));
            }

            if (string.IsNullOrWhiteSpace(command.InActor.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Incoming actor id is required."));
            }

            if (command.InActor.ActorId == command.OutActorId)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Incoming and outgoing actor are the same."));
            }

            if (battle.Actors.ContainsKey(command.InActor.ActorId))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Incoming actor '{command.InActor.ActorId}' is already in this battle."));
            }

            foreach (string skillId in command.InActor.SkillIds)
            {
                if (!battle.TryGetSkill(skillId, out _))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.ValidationFailed,
                        $"Incoming actor '{command.InActor.ActorId}' references unknown battle skill '{skillId}'."));
                }
            }

            PersistActorSkillPpIfTracked(gameState, currentActor);

            int slot = battle.TurnOrder.IndexOf(command.OutActorId);
            battle.RemoveActor(command.OutActorId);
            BattleActorState incomingState = new(command.InActor);
            HydrateSkillPp(gameState, battle, incomingState);
            battle.AddActor(incomingState);
            if (slot >= 0)
            {
                battle.TurnOrder.Insert(slot, command.InActor.ActorId);
                battle.TurnIndex = slot;
            }
            else
            {
                battle.TurnOrder.Add(command.InActor.ActorId);
            }

            context.EventSink.Publish(new BattleActorSwappedEvent(
                battle.BattleId,
                command.OutActorId,
                command.InActor.ActorId));

            DomainResult endResult = UpdateBattleEndState(gameState, battle, context);
            if (!endResult.IsSuccess)
            {
                return endResult;
            }

            AdvanceTurnIfNeeded(gameState, battle, context);
            return DomainResult.Success();
        }

        public BattleState CreateBattle(GameState gameState, StartBattleCommand command, CommandContext context)
        {
            BattleState battle = new(command.BattleId, new BattleRngState(command.Seed, command.Sequence))
            {
                RewardLootTableId = command.RewardLootTableId
            };

            foreach (BattleSkillDefinition skill in command.Skills)
            {
                battle.AddSkill(skill.Clone());
            }

            foreach (BattleActorDefinition actorDefinition in command.Actors)
            {
                BattleActorState actorState = new(actorDefinition);
                HydrateSkillPp(gameState, battle, actorState);
                battle.AddActor(actorState);
            }

            foreach (CurrencyDelta delta in command.RewardCurrency)
            {
                battle.RewardCurrency.Add(new CurrencyDelta(delta.CurrencyId, delta.Amount));
            }

            foreach (InventoryDelta delta in command.RewardInventory)
            {
                battle.RewardInventory.Add(new InventoryDelta(delta.ItemId, delta.Amount));
            }

            List<BattleActorState> ordered = battle.Actors.Values
                .OrderByDescending(x => x.Initiative)
                .ThenBy(x => x.ActorId, StringComparer.Ordinal)
                .ToList();

            foreach (BattleActorState actor in ordered)
            {
                battle.TurnOrder.Add(actor.ActorId);
            }

            battle.TurnIndex = 0;
            battle.Round = 1;

            context.EventSink.Publish(new BattleStartedEvent(battle.BattleId));
            context.EventSink.Publish(new BattleTurnAdvancedEvent(
                battle.BattleId,
                GetCurrentTurnActor(battle).ActorId,
                battle.Round));
            return battle;
        }

        private static bool TryGetActiveBattle(GameState gameState, out BattleState battle, out DomainError? error)
        {
            if (gameState.ActiveBattle is null)
            {
                battle = null!;
                error = new DomainError(DomainErrorCode.NotFound, "No active battle.");
                return false;
            }

            battle = gameState.ActiveBattle;
            error = null;
            return true;
        }

        private static BattleActorState GetCurrentTurnActor(BattleState battle)
        {
            if (battle.TurnOrder.Count == 0)
            {
                throw new InvalidOperationException("Battle has empty turn order.");
            }

            string actorId = battle.TurnOrder[battle.TurnIndex];
            return battle.Actors[actorId];
        }

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

        private static int GetEffectiveStat(GameState gameState, BattleActorState actor, string statKey, int baseValue, CommandContext context)
        {
            int signed = GetEffectiveStatSigned(gameState, actor, statKey, baseValue, context);
            return signed < 0 ? 0 : signed;
        }

        private static int GetEffectiveStatSigned(GameState gameState, BattleActorState actor, string statKey, int baseValue, CommandContext context)
        {
            if (gameState.ActorStatsState.TryGet(actor.ActorId, out StatBlock block))
            {
                IReadOnlyDictionary<string, double>? extra = null;
                if (gameState.ProgressionState.TryGet(actor.ActorId, out ActorProgression progression))
                {
                    extra = new Dictionary<string, double>(StringComparer.Ordinal) { ["level"] = progression.Level };
                }

                return block.Get(statKey, context.Definitions, context.FormulaEvaluator, extra, fallbackBase: baseValue);
            }

            // Fallback path: actors with no stat block use scalar BattleActorState fields plus
            // legacy status-modifier aggregation. Mirrors the original engine behavior.
            int modifier = 0;
            foreach (ActiveStatusEffect effect in actor.ActiveStatusEffects.Values)
            {
                if (context.Definitions.TryGetStatusEffect(effect.StatusId, out StatusEffectDefinition def)
                    && def.StatModifiers.TryGetValue(statKey, out int delta))
                {
                    modifier += delta;
                }
            }

            return baseValue + modifier;
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

        private static int LegacyScalarFor(BattleActorState actor, string statKey)
        {
            return statKey switch
            {
                "atk" => actor.Atk,
                "def" => actor.Def,
                "matk" => actor.Matk,
                "mdef" => actor.Mdef,
                "maxhp" => actor.MaxHp,
                "initiative" => actor.Initiative,
                _ => 0
            };
        }

        private static bool IsActorPrevented(BattleActorState actor, CommandContext context, out string statusId)
        {
            foreach (ActiveStatusEffect effect in actor.ActiveStatusEffects.Values)
            {
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
                .ThenBy(x => x.SkillId, StringComparer.Ordinal)
                .ToList();

            foreach (BattleAiRuleDefinition rule in orderedRules)
            {
                if (!actor.SkillIds.Contains(rule.SkillId, StringComparer.Ordinal))
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
                        .ThenBy(x => x.ActorId, StringComparer.Ordinal);
                    break;

                case BattleAiTargetPolicy.HighestThreatEnemy:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction != actor.Faction && !x.IsDowned)
                        .OrderByDescending(x => x.Atk + x.Matk)
                        .ThenBy(x => x.ActorId, StringComparer.Ordinal);
                    break;

                case BattleAiTargetPolicy.LowestHpAlly:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction == actor.Faction)
                        .OrderBy(x => x.Hp)
                        .ThenBy(x => x.ActorId, StringComparer.Ordinal);
                    break;

                case BattleAiTargetPolicy.Self:
                    candidates = new[] { actor };
                    break;

                case BattleAiTargetPolicy.RandomEnemy:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction != actor.Faction && !x.IsDowned)
                        .OrderBy(x => x.ActorId, StringComparer.Ordinal);
                    break;

                case BattleAiTargetPolicy.RandomAlly:
                    candidates = battle.Actors.Values
                        .Where(x => x.Faction == actor.Faction)
                        .OrderBy(x => x.ActorId, StringComparer.Ordinal);
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

        private DomainResult UpdateBattleEndState(GameState gameState, BattleState battle, CommandContext context)
        {
            bool anyPartyAlive = battle.GetAliveFaction(CombatFaction.Party).Any();
            bool anyEnemyAlive = battle.GetAliveFaction(CombatFaction.Enemy).Any();

            if (anyPartyAlive && anyEnemyAlive)
            {
                return DomainResult.Success();
            }

            battle.Status = anyPartyAlive ? BattleStatus.Victory : BattleStatus.Defeat;
            if (battle.Status == BattleStatus.Victory && !battle.RewardsApplied)
            {
                DomainResult rewardsResult = _rewardTransactionHandler.Handle(
                    gameState,
                    new EconomyTransactionCommand(
                        currencyDeltas: battle.RewardCurrency,
                        inventoryDeltas: battle.RewardInventory),
                    context);
                if (!rewardsResult.IsSuccess)
                {
                    return rewardsResult;
                }

                if (!string.IsNullOrWhiteSpace(battle.RewardLootTableId))
                {
                    DomainResult lootResult = _lootHandler.Handle(
                        gameState,
                        new RollAndGrantLootCommand(battle.RewardLootTableId!),
                        context);
                    if (!lootResult.IsSuccess)
                    {
                        return lootResult;
                    }
                }

                long totalXp = 0;
                foreach (BattleActorState actor in battle.Actors.Values)
                {
                    if (actor.Faction == CombatFaction.Enemy && actor.IsDowned)
                    {
                        totalXp += actor.XpReward;
                    }
                }

                if (totalXp > 0)
                {
                    foreach (BattleActorState partyActor in battle.Actors.Values)
                    {
                        if (partyActor.Faction != CombatFaction.Party || partyActor.IsDowned)
                        {
                            continue;
                        }

                        if (!gameState.ProgressionState.TryGet(partyActor.ActorId, out _))
                        {
                            continue;
                        }

                        DomainResult xpResult = _experienceGrantHandler.Handle(
                            gameState,
                            new GrantExperienceCommand(partyActor.ActorId, totalXp),
                            context);
                        if (!xpResult.IsSuccess)
                        {
                            return xpResult;
                        }
                    }
                }

                battle.RewardsApplied = true;
            }

            PersistAllTrackedSkillPp(gameState, battle);

            // Snapshot final HP / MaxHp into the event before the handler nulls ActiveBattle —
            // consumers that need post-battle actor HP (e.g. party-wipe detection, persistent
            // health bars) can read it from the event payload directly.
            Dictionary<string, int> finalHp = new(StringComparer.Ordinal);
            Dictionary<string, int> finalMaxHp = new(StringComparer.Ordinal);
            foreach (BattleActorState actor in battle.Actors.Values)
            {
                finalHp[actor.ActorId] = actor.Hp;
                finalMaxHp[actor.ActorId] = actor.MaxHp;
            }
            context.EventSink.Publish(new BattleEndedEvent(battle.BattleId, battle.Status, finalHp, finalMaxHp));
            return DomainResult.Success();
        }

        private static void HydrateSkillPp(GameState gameState, BattleState battle, BattleActorState actor)
        {
            bool tracked = gameState.ActorSkillPpState.IsTracked(actor.ActorId);
            foreach (string skillId in actor.SkillIds)
            {
                if (!battle.TryGetSkill(skillId, out BattleSkillDefinition skill) || skill.MaxPp <= 0)
                {
                    continue;
                }

                int pp = skill.MaxPp;
                if (tracked && gameState.ActorSkillPpState.TryGetSkillPp(actor.ActorId, skillId, out int stored))
                {
                    pp = stored;
                }

                actor.SkillPp[skillId] = pp;
            }
        }

        private static void PersistActorSkillPpIfTracked(GameState gameState, BattleActorState actor)
        {
            if (!gameState.ActorSkillPpState.IsTracked(actor.ActorId))
            {
                return;
            }

            foreach (KeyValuePair<string, int> pair in actor.SkillPp)
            {
                gameState.ActorSkillPpState.SetSkillPp(actor.ActorId, pair.Key, pair.Value);
            }
        }

        private static void PersistAllTrackedSkillPp(GameState gameState, BattleState battle)
        {
            foreach (BattleActorState actor in battle.Actors.Values)
            {
                PersistActorSkillPpIfTracked(gameState, actor);
            }
        }

        private void AdvanceTurnIfNeeded(GameState gameState, BattleState battle, CommandContext context)
        {
            if (battle.Status != BattleStatus.Active)
            {
                return;
            }

            int originalIndex = battle.TurnIndex;
            int maxLoops = Math.Max(1, battle.TurnOrder.Count * 8);
            int loops = 0;

            while (true)
            {
                battle.TurnIndex++;
                if (battle.TurnIndex >= battle.TurnOrder.Count)
                {
                    // Pokemon-style: re-sort by current effective initiative so equipment,
                    // status effects, and stat-block modifiers shift turn order between
                    // rounds (e.g. a Slow status drops the affected actor's slot).
                    RecomputeTurnOrder(gameState, battle, context);
                    battle.TurnIndex = 0;
                    battle.Round++;
                }

                loops++;
                if (loops > maxLoops)
                {
                    return;
                }

                BattleActorState nextActor = battle.Actors[battle.TurnOrder[battle.TurnIndex]];
                if (nextActor.IsDowned)
                {
                    continue;
                }

                ApplyTurnRefresh(gameState, nextActor, context);

                if (nextActor.IsDowned)
                {
                    DomainResult endResult = UpdateBattleEndState(gameState, battle, context);
                    if (!endResult.IsSuccess || battle.Status != BattleStatus.Active)
                    {
                        return;
                    }

                    continue;
                }

                if (IsActorPrevented(nextActor, context, out string preventStatus))
                {
                    context.EventSink.Publish(new StatusPreventedActionEvent(nextActor.ActorId, preventStatus));
                    continue;
                }

                if (battle.TurnIndex != originalIndex)
                {
                    context.EventSink.Publish(new BattleTurnAdvancedEvent(
                        battle.BattleId,
                        nextActor.ActorId,
                        battle.Round));
                }

                return;
            }
        }

        private static void RecomputeTurnOrder(GameState gameState, BattleState battle, CommandContext context)
        {
            List<BattleActorState> ordered = battle.Actors.Values
                .OrderByDescending(x => GetEffectiveStatSigned(gameState, x, "initiative", x.Initiative, context))
                .ThenBy(x => x.ActorId, StringComparer.Ordinal)
                .ToList();

            battle.TurnOrder.Clear();
            foreach (BattleActorState actor in ordered)
            {
                battle.TurnOrder.Add(actor.ActorId);
            }
        }

        private void NormalizeTurnForDownedActors(GameState gameState, BattleState battle, CommandContext context)
        {
            if (battle.Status != BattleStatus.Active || battle.TurnOrder.Count == 0)
            {
                return;
            }

            int guard = 0;
            while (battle.Actors[battle.TurnOrder[battle.TurnIndex]].IsDowned && guard++ < battle.TurnOrder.Count)
            {
                AdvanceTurnIfNeeded(gameState, battle, context);
                if (battle.Status != BattleStatus.Active)
                {
                    return;
                }
            }
        }
    }
}
