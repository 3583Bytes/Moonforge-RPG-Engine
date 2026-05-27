namespace Moonforge.Sample.Roguelike.Rendering
{

    public enum BattleLogKind
    {
        Info = 0,
        Damage = 1,
        Heal = 2,
        Victory = 3,
        Defeat = 4,
        Intro = 5
    }

    public sealed record BattleLogEntry(string Text, BattleLogKind Kind);
}
