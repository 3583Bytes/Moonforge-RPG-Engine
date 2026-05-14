using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Quests.Events;

public sealed class QuestStartedEvent : DomainEvent
{
    public QuestStartedEvent(string questId)
        : base(nameof(QuestStartedEvent))
    {
        QuestId = questId;
    }

    public string QuestId { get; }
}
