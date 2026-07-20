using System;
using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Combat.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Commands
{

    // Turn flow: advancing to the next actor (skipping downed/prevented actors), re-sorting the
    // turn order at round boundaries by current effective initiative, and normalizing the
    // pointer past actors that were downed since the last turn.
    internal sealed partial class BattleRuntime
    {
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
