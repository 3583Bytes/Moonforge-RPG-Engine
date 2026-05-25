using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Quests.Queries;

public sealed class GetQuestStatusQuery : IQuery<QuestStatus>
{
    public GetQuestStatusQuery(string questId)
    {
        QuestId = questId;
    }

    public string QuestId { get; }
}
