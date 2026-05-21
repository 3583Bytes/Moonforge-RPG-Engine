using System.Collections.Generic;
using Moonforge.Core.Combat;

namespace Moonforge.Sample.MonsterCatcher.Content;

/// <summary>
/// Move catalog. Each entry is a <see cref="BattleSkillDefinition"/> registered with the
/// active battle. The display name and emoji are looked up by id for the UI.
/// </summary>
internal static class Moves
{
    public const string Tackle = "move.tackle";
    public const string Scratch = "move.scratch";
    public const string Ember = "move.ember";
    public const string WaterGun = "move.water_gun";
    public const string VineWhip = "move.vine_whip";
    public const string Spark = "move.spark";
    public const string MudSlap = "move.mud_slap";
    public const string RockThrow = "move.rock_throw";
    public const string Gust = "move.gust";
    public const string Lick = "move.lick";
    public const string Bite = "move.bite";
    public const string Recover = "move.recover";

    public static IReadOnlyList<BattleSkillDefinition> All { get; } = new List<BattleSkillDefinition>
    {
        new(id: Tackle, BattleSkillEffectType.PhysicalDamage, power: 5, damageTypeId: TypeIds.Normal, maxPp: 30, displayName: "Tackle"),
        new(id: Scratch, BattleSkillEffectType.PhysicalDamage, power: 5, damageTypeId: TypeIds.Normal, maxPp: 30, displayName: "Scratch"),
        new(id: Ember, BattleSkillEffectType.MagicalDamage, power: 7, damageTypeId: TypeIds.Fire, maxPp: 25, displayName: "Ember"),
        new(id: WaterGun, BattleSkillEffectType.MagicalDamage, power: 7, damageTypeId: TypeIds.Water, maxPp: 25, displayName: "Water Gun"),
        new(id: VineWhip, BattleSkillEffectType.MagicalDamage, power: 7, damageTypeId: TypeIds.Grass, maxPp: 25, displayName: "Vine Whip"),
        new(id: Spark, BattleSkillEffectType.MagicalDamage, power: 7, damageTypeId: TypeIds.Electric, maxPp: 25, displayName: "Spark"),
        new(id: MudSlap, BattleSkillEffectType.MagicalDamage, power: 6, damageTypeId: TypeIds.Ground, maxPp: 30, displayName: "Mud Slap"),
        new(id: RockThrow, BattleSkillEffectType.PhysicalDamage, power: 8, damageTypeId: TypeIds.Rock, maxPp: 15, displayName: "Rock Throw"),
        new(id: Gust, BattleSkillEffectType.MagicalDamage, power: 6, damageTypeId: TypeIds.Flying, maxPp: 30, displayName: "Gust"),
        new(id: Lick, BattleSkillEffectType.PhysicalDamage, power: 5, damageTypeId: TypeIds.Ghost, maxPp: 30, displayName: "Lick"),
        new(id: Bite, BattleSkillEffectType.PhysicalDamage, power: 7, damageTypeId: TypeIds.Dark, maxPp: 25, displayName: "Bite"),
        new(id: Recover, BattleSkillEffectType.Heal, power: 10, maxPp: 10, displayName: "Recover")
    };
}
