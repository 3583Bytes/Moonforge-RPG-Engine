using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions
{

    public sealed class QuestObjectiveDefinition
    {
        public QuestObjectiveDefinition(
            string id,
            QuestObjectiveType objectiveType,
            string? targetId = null,
            int requiredCount = 1,
            IReadOnlyList<string>? childObjectiveIds = null,
            string? displayName = null)
        {
            Id = id;
            ObjectiveType = objectiveType;
            TargetId = targetId;
            RequiredCount = requiredCount;
            ChildObjectiveIds = childObjectiveIds ?? System.Array.Empty<string>();
            DisplayName = displayName;
        }

        public string Id { get; }

        public QuestObjectiveType ObjectiveType { get; }

        public string? TargetId { get; }

        public int RequiredCount { get; }

        public IReadOnlyList<string> ChildObjectiveIds { get; }

        public string? DisplayName { get; }
    }
}
