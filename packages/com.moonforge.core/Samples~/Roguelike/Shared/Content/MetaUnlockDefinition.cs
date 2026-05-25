namespace Moonforge.Sample.Roguelike.Content;

public sealed record MetaUnlockDefinition(
    MetaUnlockId Id,
    string Name,
    string Description,
    int TokenCost);
