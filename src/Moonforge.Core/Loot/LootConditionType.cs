namespace Moonforge.Core.Loot;

/// <summary>
/// Gates that can be evaluated against the current <c>GameState</c> to include or exclude
/// a loot entry from a roll.
/// </summary>
public enum LootConditionType
{
    WorldBoolEquals = 0,
    WorldIntAtLeast = 1,
    QuestStatusEquals = 2,
    ActorLevelAtLeast = 3
}
