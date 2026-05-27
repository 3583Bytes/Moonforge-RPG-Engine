using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Quests.Queries
{

    public sealed class GetQuestObjectiveProgressQuery : IQuery<int>
    {
        public GetQuestObjectiveProgressQuery(string questId, string objectiveId)
        {
            QuestId = questId;
            ObjectiveId = objectiveId;
        }

        public string QuestId { get; }

        public string ObjectiveId { get; }
    }
}
