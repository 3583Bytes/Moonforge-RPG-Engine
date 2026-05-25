using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Quests.Queries;

public sealed class GetQuestObjectiveProgressQueryHandler : IQueryHandler<GetQuestObjectiveProgressQuery, int>
{
    public int Query(GameState gameState, GetQuestObjectiveProgressQuery query)
    {
        if (!gameState.QuestState.TryGet(query.QuestId, out QuestInstanceState quest))
        {
            return 0;
        }

        return quest.GetObjectiveProgress(query.ObjectiveId);
    }
}
