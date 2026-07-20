using System;
using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Loot.Commands;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands
{

    /// <summary>
    /// Orchestrates a single active battle: player/AI actions, capture, swap, and battle
    /// creation. Determinism is a hard constraint — every roll goes through
    /// <c>battle.RngState</c> and every enumeration that affects outcomes is ordinally sorted.
    ///
    /// The implementation is split across several <c>partial</c> files by concern:
    /// <list type="bullet">
    ///   <item><c>BattleRuntime.cs</c> — public orchestration API + shared entry helpers.</item>
    ///   <item><c>BattleRuntime.SkillResolution.cs</c> — skill application, targeting, damage.</item>
    ///   <item><c>BattleRuntime.Stats.cs</c> — effective-stat resolution.</item>
    ///   <item><c>BattleRuntime.Statuses.cs</c> — status prevention, application, and ticking.</item>
    ///   <item><c>BattleRuntime.Ai.cs</c> — AI action/target selection.</item>
    ///   <item><c>BattleRuntime.TurnOrder.cs</c> — turn advancement and re-sorting.</item>
    ///   <item><c>BattleRuntime.Rewards.cs</c> — battle-end detection and reward payout.</item>
    ///   <item><c>BattleRuntime.SkillPp.cs</c> — per-skill PP hydrate/persist bridging.</item>
    /// </list>
    /// </summary>
    internal sealed partial class BattleRuntime
    {
        private readonly ICommandHandler<EconomyTransactionCommand> _rewardTransactionHandler;
        private readonly ICommandHandler<GrantExperienceCommand> _experienceGrantHandler;
        private readonly ICommandHandler<RollAndGrantLootCommand> _lootHandler;

        public BattleRuntime(
            ICommandHandler<EconomyTransactionCommand>? rewardTransactionHandler = null,
            ICommandHandler<GrantExperienceCommand>? experienceHandler = null,
            ICommandHandler<RollAndGrantLootCommand>? lootHandler = null)
        {
            _rewardTransactionHandler = rewardTransactionHandler ?? new EconomyTransactionCommandHandler();
            _experienceGrantHandler = experienceHandler ?? new GrantExperienceCommandHandler();
            _lootHandler = lootHandler ?? new RollAndGrantLootCommandHandler();
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
    }
}
