using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Quests.Events;

public sealed class QuestAbandonedEvent : DomainEvent
{
    public QuestAbandonedEvent(string questId)
        : base(nameof(QuestAbandonedEvent))
    {
        QuestId = questId;
    }

    public string QuestId { get; }
}
