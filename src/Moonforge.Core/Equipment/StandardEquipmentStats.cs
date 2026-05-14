namespace Moonforge.Core.Equipment;

/// <summary>
/// Recommended string keys for combat-related equipment stat bonuses. Games are free to use other keys;
/// these constants exist only to encourage a consistent vocabulary across the built-in combat module.
/// </summary>
public static class StandardEquipmentStats
{
    public const string Attack = "atk";
    public const string Defense = "def";
    public const string MagicAttack = "matk";
    public const string MagicDefense = "mdef";
    public const string Initiative = "initiative";
    public const string MaxHp = "maxhp";
}
