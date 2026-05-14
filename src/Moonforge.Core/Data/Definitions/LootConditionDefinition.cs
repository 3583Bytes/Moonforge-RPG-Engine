using Moonforge.Core.Loot;
using Moonforge.Core.Quests;

namespace Moonforge.Core.Data.Definitions;

public sealed class LootConditionDefinition
{
    public LootConditionDefinition(
        LootConditionType conditionType,
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

    public LootConditionType ConditionType { get; }

    /// <summary>
    /// Identifier used by the condition. World-variable conditions use a variable key;
    /// quest conditions use a quest ID; actor-level conditions use an actor ID.
    /// </summary>
    public string Key { get; }

    public bool BoolValue { get; }

    public int IntValue { get; }

    public QuestStatus QuestStatus { get; }
}
