using System;
using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Equipment;

namespace Moonforge.Sample.Roguelike.Content;

public sealed record GearMetadata(
    string ItemId,
    string Name,
    string SlotId,
    int Atk,
    int Def,
    int Matk,
    int Mdef,
    int Initiative,
    IReadOnlyList<string>? GrantedSkillIds = null,
    int Crit = 0,
    int Acc = 0,
    int Eva = 0,
    int CritDmg = 0)
{
    public EquipmentDefinition ToEquipmentDefinition()
    {
        Dictionary<string, int> bonuses = new(StringComparer.Ordinal);
        if (Atk != 0) bonuses[StandardEquipmentStats.Attack] = Atk;
        if (Def != 0) bonuses[StandardEquipmentStats.Defense] = Def;
        if (Matk != 0) bonuses[StandardEquipmentStats.MagicAttack] = Matk;
        if (Mdef != 0) bonuses[StandardEquipmentStats.MagicDefense] = Mdef;
        if (Initiative != 0) bonuses[StandardEquipmentStats.Initiative] = Initiative;
        if (Crit != 0) bonuses[StandardEquipmentStats.CritChance] = Crit;
        if (Acc != 0) bonuses[StandardEquipmentStats.Accuracy] = Acc;
        if (Eva != 0) bonuses[StandardEquipmentStats.Evasion] = Eva;
        if (CritDmg != 0) bonuses[StandardEquipmentStats.CritDamage] = CritDmg;
        return new EquipmentDefinition(
            ItemId,
            SlotId,
            bonuses,
            displayName: Name,
            grantedSkillIds: GrantedSkillIds);
    }
}
