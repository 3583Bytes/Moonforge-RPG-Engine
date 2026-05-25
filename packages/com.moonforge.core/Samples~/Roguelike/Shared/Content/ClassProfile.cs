namespace Moonforge.Sample.Roguelike.Content;

public sealed record ClassProfile(
    PlayerClass ClassId,
    string Name,
    string Summary,
    string BasicSkillId,
    int MaxHpBase,
    int AtkBase,
    int DefBase,
    int MatkBase,
    int MdefBase,
    int InitiativeBase);
