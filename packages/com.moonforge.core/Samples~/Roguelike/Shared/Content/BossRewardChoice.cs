namespace Moonforge.Sample.Roguelike.Content;

public sealed record BossRewardChoice(
    BossRewardKind Kind,
    string Label,
    string Description,
    string? ItemId,
    long Amount);
