namespace Moonforge.Core.Stats
{

    /// <summary>
    /// Recommended string keys for built-in actor stats. Games are free to use other keys;
    /// these constants exist only to encourage a consistent vocabulary across modules.
    /// </summary>
    public static class StandardStats
    {
        public const string Strength = "str";
        public const string Vitality = "vit";
        public const string Intelligence = "int";
        public const string Agility = "agi";
        public const string Luck = "luk";

        public const string MaxHp = "maxhp";
        public const string MaxMp = "maxmp";
        public const string Attack = "atk";
        public const string Defense = "def";
        public const string MagicAttack = "matk";
        public const string MagicDefense = "mdef";
        public const string Initiative = "initiative";

        public const string CritChance = "crit";
        public const string CritDamage = "critdmg";
        public const string Accuracy = "acc";
        public const string Evasion = "eva";

        public const string ResistancePhysical = "res.physical";
        public const string ResistanceMagical = "res.magical";
        public const string ResistanceFire = "res.fire";
        public const string ResistanceIce = "res.ice";
        public const string ResistanceLightning = "res.lightning";
        public const string ResistanceHoly = "res.holy";
        public const string ResistanceDark = "res.dark";
        public const string ResistancePoison = "res.poison";

        /// <summary>
        /// Conventional stat ID for the percent resistance to a given damage type. Matches the
        /// default <c>ResistanceStatId</c> on <c>DamageTypeDefinition</c>.
        /// </summary>
        public static string Resistance(string damageTypeId) => $"res.{damageTypeId}";
    }
}
