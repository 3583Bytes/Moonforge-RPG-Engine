using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Loot.Commands;
using Moonforge.Core.Progression.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands
{

    // Battle-end detection and one-time reward payout (currency/inventory transaction, loot
    // roll, and per-party-actor XP). Reward-triggered reactors may consume RNG, so XP is
    // granted in ordinal actor-id order to keep the sequence deterministic.
    internal sealed partial class BattleRuntime
    {
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
                    // Sort so XP grants (and the level-up reactors they trigger, which may
                    // consume RNG) run in a deterministic order per actor.
                    List<string> rewardActorIds = new(battle.Actors.Keys);
                    rewardActorIds.Sort(System.StringComparer.Ordinal);

                    foreach (string rewardActorId in rewardActorIds)
                    {
                        BattleActorState partyActor = battle.Actors[rewardActorId];
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
            Dictionary<string, int> finalHp = new(System.StringComparer.Ordinal);
            Dictionary<string, int> finalMaxHp = new(System.StringComparer.Ordinal);
            List<string> snapshotActorIds = new(battle.Actors.Keys);
            snapshotActorIds.Sort(System.StringComparer.Ordinal);
            foreach (string snapshotActorId in snapshotActorIds)
            {
                BattleActorState actor = battle.Actors[snapshotActorId];
                finalHp[actor.ActorId] = actor.Hp;
                finalMaxHp[actor.ActorId] = actor.MaxHp;
            }
            context.EventSink.Publish(new BattleEndedEvent(battle.BattleId, battle.Status, finalHp, finalMaxHp));
            return DomainResult.Success();
        }
    }
}
