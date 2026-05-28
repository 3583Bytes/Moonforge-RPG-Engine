using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Quests.Events
{

    public sealed class QuestCompletedEvent : DomainEvent
    {
        public QuestCompletedEvent(string questId)
            : base(nameof(QuestCompletedEvent))
        {
            QuestId = questId;
        }

        public string QuestId { get; }
    }
}
