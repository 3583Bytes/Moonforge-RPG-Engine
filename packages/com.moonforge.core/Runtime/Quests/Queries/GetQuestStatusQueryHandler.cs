using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Quests.Queries;

public sealed class GetQuestStatusQueryHandler : IQueryHandler<GetQuestStatusQuery, QuestStatus>
{
    public QuestStatus Query(GameState gameState, GetQuestStatusQuery query)
    {
        if (!gameState.QuestState.TryGet(query.QuestId, out QuestInstanceState quest))
        {
            return QuestStatus.NotStarted;
        }

        return quest.Status;
    }
}
