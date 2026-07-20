using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Inventory.Events;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Quests.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Quests
{

    public sealed class QuestObjectiveTrackingReactor : IDomainEventReactor
    {
        private readonly ICommandHandler<ClaimQuestRewardsCommand> _claimHandler;

        /// <summary>
        /// Pass the handler you registered for <see cref="ClaimQuestRewardsCommand"/> so
        /// auto-claimed rewards behave identically to a directly dispatched claim.
        /// Defaults to the built-in handler.
        /// </summary>
        public QuestObjectiveTrackingReactor(ICommandHandler<ClaimQuestRewardsCommand>? claimRewardsHandler = null)
        {
            _claimHandler = claimRewardsHandler ?? new ClaimQuestRewardsCommandHandler();
        }

        public DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context)
        {
            switch (domainEvent)
            {
                case QuestSignalEvent questSignal:
                    return ApplySignal(gameState, questSignal.SignalType, questSignal.TargetId, questSignal.Amount, context);
                case InventoryItemChangedEvent inventoryChanged when inventoryChanged.Delta > 0:
                    return ApplySignal(gameState, QuestSignalType.Collect, inventoryChanged.ItemId, inventoryChanged.Delta, context);
                default:
                    return DomainResult.Success();
            }
        }

        private DomainResult ApplySignal(
            GameState gameState,
            QuestSignalType signalType,
            string targetId,
            int amount,
            CommandContext context)
        {
            // Snapshot and sort by quest id so progression/completion events fire in a
            // deterministic order regardless of dictionary insertion order, and so the
            // auto-claim sub-handler can't invalidate the iterator.
            List<KeyValuePair<string, QuestInstanceState>> quests = new(gameState.QuestState.Quests);
            quests.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));

            foreach ((string questId, QuestInstanceState instance) in quests)
            {
                if (instance.Status != QuestStatus.Active)
                {
                    continue;
                }

                if (!context.Definitions.TryGetQuest(questId, out QuestDefinition questDefinition))
                {
                    continue;
                }

                if (!questDefinition.AutoTrack)
                {
                    continue;
                }

                if (!TryBuildObjectiveMap(questDefinition, out Dictionary<string, QuestObjectiveDefinition> objectiveMap, out string mapError))
                {
                    return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, mapError));
                }

                bool changed = false;
                foreach (QuestObjectiveDefinition objective in questDefinition.Objectives)
                {
                    if (!IsPrimitiveSignalMatch(objective, signalType, targetId))
                    {
                        continue;
                    }

                    int required = Math.Max(1, objective.RequiredCount);
                    int previous = instance.GetObjectiveProgress(objective.Id);
                    int next = Math.Min(required, previous + amount);
                    if (next == previous)
                    {
                        continue;
                    }

                    instance.SetObjectiveProgress(objective.Id, next);
                    changed = true;
                    context.EventSink.Publish(new QuestObjectiveProgressedEvent(
                        questId,
                        objective.Id,
                        previous,
                        next,
                        required));
                }

                if (!changed)
                {
                    continue;
                }

                if (instance.Status == QuestStatus.Active && IsQuestComplete(questDefinition, objectiveMap, instance))
                {
                    instance.Status = QuestStatus.Completed;
                    context.EventSink.Publish(new QuestCompletedEvent(questId));

                    // AutoClaim → dispatch the reward claim inside the same transaction so
                    // currency/inventory rewards land alongside the completion event. If the
                    // claim fails (e.g. inventory full and the reward includes an item), the
                    // transaction rolls back including the objective progress update.
                    if (questDefinition.AutoClaim
                        && (questDefinition.RewardCurrency.Count > 0 || questDefinition.RewardInventory.Count > 0))
                    {
                        DomainResult claimResult = _claimHandler.Handle(
                            gameState,
                            new ClaimQuestRewardsCommand(questId),
                            context);
                        if (!claimResult.IsSuccess)
                        {
                            return claimResult;
                        }
                    }
                }
            }

            return DomainResult.Success();
        }

        private static bool TryBuildObjectiveMap(
            QuestDefinition questDefinition,
            out Dictionary<string, QuestObjectiveDefinition> objectiveMap,
            out string error)
        {
            objectiveMap = new Dictionary<string, QuestObjectiveDefinition>(StringComparer.Ordinal);
            foreach (QuestObjectiveDefinition objective in questDefinition.Objectives)
            {
                if (string.IsNullOrWhiteSpace(objective.Id))
                {
                    error = $"Quest '{questDefinition.Id}' has objective with empty ID.";
                    return false;
                }

                if (objectiveMap.ContainsKey(objective.Id))
                {
                    error = $"Quest '{questDefinition.Id}' has duplicate objective ID '{objective.Id}'.";
                    return false;
                }

                objectiveMap[objective.Id] = objective;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsPrimitiveSignalMatch(QuestObjectiveDefinition objective, QuestSignalType signalType, string targetId)
        {
            QuestSignalType expectedSignal;
            switch (objective.ObjectiveType)
            {
                case QuestObjectiveType.Kill:
                    expectedSignal = QuestSignalType.Kill;
                    break;
                case QuestObjectiveType.Collect:
                    expectedSignal = QuestSignalType.Collect;
                    break;
                case QuestObjectiveType.Talk:
                    expectedSignal = QuestSignalType.Talk;
                    break;
                case QuestObjectiveType.Visit:
                    expectedSignal = QuestSignalType.Visit;
                    break;
                default:
                    return false;
            }

            return expectedSignal == signalType && string.Equals(objective.TargetId, targetId, StringComparison.Ordinal);
        }

        private static bool IsQuestComplete(
            QuestDefinition questDefinition,
            IReadOnlyDictionary<string, QuestObjectiveDefinition> objectiveMap,
            QuestInstanceState instance)
        {
            if (questDefinition.RootObjectiveIds.Count == 0)
            {
                return false;
            }

            foreach (string rootObjectiveId in questDefinition.RootObjectiveIds)
            {
                if (!EvaluateObjective(rootObjectiveId, objectiveMap, instance, new HashSet<string>(StringComparer.Ordinal)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EvaluateObjective(
            string objectiveId,
            IReadOnlyDictionary<string, QuestObjectiveDefinition> objectiveMap,
            QuestInstanceState instance,
            HashSet<string> chain)
        {
            if (!objectiveMap.TryGetValue(objectiveId, out QuestObjectiveDefinition objective))
            {
                return false;
            }

            if (!chain.Add(objectiveId))
            {
                return false;
            }

            try
            {
                switch (objective.ObjectiveType)
                {
                    case QuestObjectiveType.Kill:
                    case QuestObjectiveType.Collect:
                    case QuestObjectiveType.Talk:
                    case QuestObjectiveType.Visit:
                        return instance.GetObjectiveProgress(objectiveId) >= Math.Max(1, objective.RequiredCount);

                    case QuestObjectiveType.CompositeAnd:
                        foreach (string childId in objective.ChildObjectiveIds)
                        {
                            if (!EvaluateObjective(childId, objectiveMap, instance, chain))
                            {
                                return false;
                            }
                        }

                        return true;

                    case QuestObjectiveType.CompositeOr:
                        foreach (string childId in objective.ChildObjectiveIds)
                        {
                            if (EvaluateObjective(childId, objectiveMap, instance, chain))
                            {
                                return true;
                            }
                        }

                        return false;

                    default:
                        return false;
                }
            }
            finally
            {
                chain.Remove(objectiveId);
            }
        }
    }
}
