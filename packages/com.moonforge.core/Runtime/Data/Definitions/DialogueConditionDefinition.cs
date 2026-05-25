using Moonforge.Core.Quests;

namespace Moonforge.Core.Data.Definitions;

public sealed class DialogueConditionDefinition
{
    public DialogueConditionDefinition(
        DialogueConditionType conditionType,
        string key,
        bool boolValue = false,
        int intValue = 0,
        QuestStatus questStatus = QuestStatus.NotStarted)
    {
        ConditionType = conditionType;
        Key = key;
        BoolValue = boolValue;
        IntValue = intValue;
        QuestStatus = questStatus;
    }

    public DialogueConditionType ConditionType { get; }

    public string Key { get; }

    public bool BoolValue { get; }

    public int IntValue { get; }

    public QuestStatus QuestStatus { get; }
}
