using System.Collections.Generic;
using System.Linq;
using Moonforge.Core.Economy.Commands;

namespace Moonforge.Core.Data.Definitions
{

    public sealed class QuestDefinition
    {
        public QuestDefinition(
            string id,
            IReadOnlyList<QuestObjectiveDefinition> objectives,
            IReadOnlyList<string>? rootObjectiveIds = null,
            bool autoTrack = true,
            bool autoClaim = false,
            string? displayName = null,
            string? description = null,
            IReadOnlyList<CurrencyDelta>? rewardCurrency = null,
            IReadOnlyList<InventoryDelta>? rewardInventory = null)
        {
            Id = id;
            Objectives = objectives ?? System.Array.Empty<QuestObjectiveDefinition>();
            AutoTrack = autoTrack;
            AutoClaim = autoClaim;
            RootObjectiveIds = rootObjectiveIds ?? InferRootObjectiveIds(Objectives);
            DisplayName = displayName;
            Description = description;
            RewardCurrency = rewardCurrency ?? System.Array.Empty<CurrencyDelta>();
            RewardInventory = rewardInventory ?? System.Array.Empty<InventoryDelta>();
        }

        public string Id { get; }

        public IReadOnlyList<QuestObjectiveDefinition> Objectives { get; }

        public IReadOnlyList<string> RootObjectiveIds { get; }

        public bool AutoTrack { get; }

        /// <summary>
        /// When true, the tracking reactor dispatches <see cref="Moonforge.Core.Quests.Commands.ClaimQuestRewardsCommand"/>
        /// automatically the moment a quest auto-completes — currency / inventory rewards are
        /// granted in the same transaction as the completing signal. Default false preserves
        /// the explicit-claim flow for consumers that want to gate rewards behind UI
        /// (e.g. an "Accept reward?" confirmation).
        /// </summary>
        public bool AutoClaim { get; }

        public string? DisplayName { get; }

        public string? Description { get; }

        public IReadOnlyList<CurrencyDelta> RewardCurrency { get; }

        public IReadOnlyList<InventoryDelta> RewardInventory { get; }

        private static IReadOnlyList<string> InferRootObjectiveIds(IReadOnlyList<QuestObjectiveDefinition> objectives)
        {
            HashSet<string> referencedChildren = new();
            foreach (QuestObjectiveDefinition objective in objectives)
            {
                foreach (string childId in objective.ChildObjectiveIds)
                {
                    referencedChildren.Add(childId);
                }
            }

            return objectives
                .Where(x => !referencedChildren.Contains(x.Id))
                .Select(x => x.Id)
                .ToArray();
        }
    }
}
