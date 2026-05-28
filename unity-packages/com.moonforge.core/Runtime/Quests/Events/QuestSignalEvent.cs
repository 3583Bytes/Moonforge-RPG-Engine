using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Quests.Events
{

    public sealed class QuestSignalEvent : DomainEvent
    {
        public QuestSignalEvent(QuestSignalType signalType, string targetId, int amount)
            : base(nameof(QuestSignalEvent))
        {
            SignalType = signalType;
            TargetId = targetId;
            Amount = amount;
        }

        public QuestSignalType SignalType { get; }

        public string TargetId { get; }

        public int Amount { get; }
    }
}
