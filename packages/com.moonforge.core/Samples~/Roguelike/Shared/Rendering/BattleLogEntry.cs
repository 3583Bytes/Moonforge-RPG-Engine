namespace Moonforge.Sample.Roguelike.Rendering;

internal enum BattleLogKind
{
    Info = 0,
    Damage = 1,
    Heal = 2,
    Victory = 3,
    Defeat = 4,
    Intro = 5
}

internal sealed record BattleLogEntry(string Text, BattleLogKind Kind);
