namespace Moonforge.Sample.Roguelike.Session;

/// <summary>
/// Identifies which town landmark the hero is currently standing on, used by the
/// interaction handler to route the Interact action to the right behavior.
/// </summary>
public enum TownInteractionKind
{
    Guard = 0,
    Alchemist = 1,
    Healer = 2,
    Cache = 3,
    Fountain = 4,
    QuestBoard = 5,
    Shrine = 6,
    DungeonEntrance = 7
}
