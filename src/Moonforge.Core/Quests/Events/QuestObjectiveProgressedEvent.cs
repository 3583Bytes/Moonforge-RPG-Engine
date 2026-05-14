using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Quests.Events;

public sealed class QuestObjectiveProgressedEvent : DomainEvent
{
    public QuestObjectiveProgressedEvent(string questId, string objectiveId, int previousValue, int newValue, int required)
        : base(nameof(QuestObjectiveProgressedEvent))
    {
        QuestId = questId;
        ObjectiveId = objectiveId;
        PreviousValue = previousValue;
        NewValue = newValue;
        Required = required;
    }

    public string QuestId { get; }

    public string ObjectiveId { get; }

    public int PreviousValue { get; }

    public int NewValue { get; }

    public int Required { get; }
}
