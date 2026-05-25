using System;
using System.Collections.Generic;
using Moonforge.Core.Combat;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Dialogue;

namespace Moonforge.Sample.Roguelike.Content;

/// <summary>
/// Static catalog of all roguelike-sample content: id constants, class/gear/meta-unlock
/// definitions, ability rosters, contract quest ids, and the guard's dialogue tree.
/// Moved here so both the console sample (compiled via csproj Compile glob) and the Unity
/// sample (via Roguelike.Shared.asmdef) share a single source of truth.
/// </summary>
public static class RoguelikeContent
{
    public const int SaveSchemaVersion = 1;
    public const string HeroActorId = "party.hero";
    public const string GuardActorId = "npc.guard";
    public const string GoldCurrencyId = "currency.gold";
    public const string TokenCurrencyId = "currency.token";
    public const string MediumPotionItemId = "item.potion.medium";
    public const string HerbItemId = "item.herb";
    public const string TownShopId = "shop.town.general";
    public const string VisitHealerTargetId = "town.healer";
    public const string VisitFountainTargetId = "town.fountain";
    public const string CacheInteractableDefId = "interactable.town.cache";
    public const string CacheInteractableInstanceId = "town.cache.01";
    public const string CacheLootTableId = "loot.town.cache";
    public const string FountainInteractableDefId = "interactable.town.fountain";
    public const string FountainInteractableInstanceId = "town.fountain.01";
    public const string FountainSignalKey = "town.fountain.touched";
    public const string ItemBronzeBlade = "item.gear.bronze_blade";
    public const string ItemOakWand = "item.gear.oak_wand";
    public const string ItemLeatherVest = "item.gear.leather_vest";
    public const string ItemMysticRobe = "item.gear.mystic_robe";
    public const string ItemIronRing = "item.gear.iron_ring";
    public const string ItemLuckyCharm = "item.gear.lucky_charm";
    public const string SlotWeapon = "slot.weapon";
    public const string SlotArmor = "slot.armor";
    public const string SlotAccessory = "slot.accessory";
    public const string AlchemistBrewRecipeId = "recipe.potion.medium.from.herbs";
    public const string HeroCurveId = "curve.hero";
    public const string GuardDialogueId = "dialogue.guard";
    public const string FlagGuardBriefed = "flag.guard.briefed";
    public const string WorldVarDeepestFloor = "dungeon.deepest_floor";
    public const string FocusResourceId = "focus";
    public const int MaxFocus = 3;

    public static readonly Dictionary<string, string> DialogueText = new(StringComparer.Ordinal)
    {
        ["dialogue.guard.start.text"] = "Halt, traveler. What do you need?",
        ["dialogue.guard.choice.dungeon"] = "What's down in the dungeon?",
        ["dialogue.guard.choice.tips"] = "Any tips for me?",
        ["dialogue.guard.choice.fountain"] = "Have you seen the fountain?",
        ["dialogue.guard.choice.bye"] = "Goodbye.",
        ["dialogue.guard.info.dungeon.text"] = "The descent grows colder. Cull the warrens packs near the stairs first.",
        ["dialogue.guard.info.tips.text"] = "Boss floors loom every third descent. Stock potions.",
        ["dialogue.guard.info.fountain.text"] = "Aye, the fountain heals the mind, not the body. Pay it a visit."
    };

    public static readonly string[] ContractQuestIds =
    [
        "quest.contract.hunt.warrens",
        "quest.contract.remedy",
        "quest.contract.guard.patrol",
        "quest.contract.scouting"
    ];

    public static readonly ClassProfile[] ClassProfiles =
    [
        new ClassProfile(
            PlayerClass.Knight,
            "Knight",
            "Balanced frontline with strong defense.",
            BasicSkillId: "skill.attack",
            MaxHpBase: 44,
            AtkBase: 12,
            DefBase: 7,
            MatkBase: 3,
            MdefBase: 5,
            InitiativeBase: 17),
        new ClassProfile(
            PlayerClass.Ranger,
            "Ranger",
            "Fast striker with higher initiative and crit pressure.",
            BasicSkillId: "skill.attack",
            MaxHpBase: 42,
            AtkBase: 13,
            DefBase: 4,
            MatkBase: 3,
            MdefBase: 4,
            InitiativeBase: 22),
        new ClassProfile(
            PlayerClass.Arcanist,
            "Arcanist",
            "Glass cannon using magical bolts.",
            BasicSkillId: "skill.bolt",
            MaxHpBase: 48,
            AtkBase: 7,
            DefBase: 3,
            MatkBase: 11,
            MdefBase: 6,
            InitiativeBase: 19)
    ];

    public static readonly GearMetadata[] GearCatalog =
    [
        new GearMetadata(ItemBronzeBlade, "Bronze Blade", SlotWeapon,    Atk: 2, Def: 0, Matk: 0, Mdef: 0, Initiative: 0),
        new GearMetadata(ItemOakWand,     "Oak Wand",    SlotWeapon,    Atk: 0, Def: 0, Matk: 2, Mdef: 0, Initiative: 0, GrantedSkillIds: ["skill.bolt"]),
        new GearMetadata(ItemLeatherVest, "Leather Vest", SlotArmor,    Atk: 0, Def: 2, Matk: 0, Mdef: 0, Initiative: 0),
        new GearMetadata(ItemMysticRobe,  "Mystic Robe", SlotArmor,     Atk: 0, Def: 0, Matk: 1, Mdef: 2, Initiative: 0),
        new GearMetadata(ItemIronRing,    "Iron Ring",   SlotAccessory, Atk: 1, Def: 1, Matk: 0, Mdef: 0, Initiative: 0, Crit: 5),
        new GearMetadata(ItemLuckyCharm,  "Lucky Charm", SlotAccessory, Atk: 0, Def: 0, Matk: 0, Mdef: 1, Initiative: 2, Acc: 5, Eva: 5)
    ];

    public static readonly MetaUnlockDefinition[] MetaUnlockDefinitions =
    [
        new MetaUnlockDefinition(
            MetaUnlockId.FieldRations,
            "Field Rations",
            "Start each run with +1 medium potion.",
            TokenCost: 2),
        new MetaUnlockDefinition(
            MetaUnlockId.DeepPockets,
            "Deep Pockets",
            "Increase inventory capacity by +4.",
            TokenCost: 3),
        new MetaUnlockDefinition(
            MetaUnlockId.CombatDrills,
            "Combat Drills",
            "Heroes gain +2 ATK, +2 MATK, +1 DEF in battle.",
            TokenCost: 4),
        new MetaUnlockDefinition(
            MetaUnlockId.LuckyFinds,
            "Lucky Finds",
            "Increase gear drop chance by +10%.",
            TokenCost: 5)
    ];

    public static readonly ClassAbilityDefinition[] ClassAbilities =
    [
        new ClassAbilityDefinition(
            PlayerClass.Knight,
            "skill.knight.shieldbash",
            "Shield Bash",
            "Heavy strike that staggers the target.",
            BattleSkillEffectType.PhysicalDamage,
            Power: 14,
            CooldownTurns: 2,
            FocusCost: 1,
            TargetSelf: false),
        new ClassAbilityDefinition(
            PlayerClass.Knight,
            "skill.knight.guardstance",
            "Guard Stance",
            "Recover composure and restore HP.",
            BattleSkillEffectType.Heal,
            Power: 9,
            CooldownTurns: 3,
            FocusCost: 2,
            TargetSelf: true),
        new ClassAbilityDefinition(
            PlayerClass.Knight,
            "skill.knight.frostblade",
            "Frostblade Strike",
            "Enchanted sword strike infused with bitter cold. Bypasses fire defenses.",
            BattleSkillEffectType.PhysicalDamage,
            Power: 16,
            CooldownTurns: 2,
            FocusCost: 2,
            TargetSelf: false,
            DamageTypeId: StandardDamageTypes.Ice),
        new ClassAbilityDefinition(
            PlayerClass.Ranger,
            "skill.ranger.aimedshot",
            "Aimed Shot",
            "Precise shot with high damage.",
            BattleSkillEffectType.PhysicalDamage,
            Power: 13,
            CooldownTurns: 2,
            FocusCost: 1,
            TargetSelf: false),
        new ClassAbilityDefinition(
            PlayerClass.Ranger,
            "skill.ranger.volley",
            "Volley",
            "Rapid follow-up arrows.",
            BattleSkillEffectType.PhysicalDamage,
            Power: 10,
            CooldownTurns: 3,
            FocusCost: 2,
            TargetSelf: false),
        new ClassAbilityDefinition(
            PlayerClass.Ranger,
            "skill.ranger.firstaid",
            "First Aid",
            "Field-dressing kit. Patches wounds in a pinch.",
            BattleSkillEffectType.Heal,
            Power: 9,
            CooldownTurns: 3,
            FocusCost: 2,
            TargetSelf: true),
        new ClassAbilityDefinition(
            PlayerClass.Arcanist,
            "skill.arcanist.frostbolt",
            "Frost Bolt",
            "Hurl a shard of bitter cold. Devastates fire-creatures and creatures of flame.",
            BattleSkillEffectType.MagicalDamage,
            Power: 19,
            CooldownTurns: 1,
            FocusCost: 1,
            TargetSelf: false,
            DamageTypeId: StandardDamageTypes.Ice),
        new ClassAbilityDefinition(
            PlayerClass.Arcanist,
            "skill.arcanist.manasurge",
            "Mana Surge",
            "Channel energy to mend wounds.",
            BattleSkillEffectType.Heal,
            Power: 13,
            CooldownTurns: 2,
            FocusCost: 2,
            TargetSelf: true),
        new ClassAbilityDefinition(
            PlayerClass.Arcanist,
            "skill.arcanist.stormbolt",
            "Storm Bolt",
            "Crackling arc of lightning that strikes every enemy at once.",
            BattleSkillEffectType.MagicalDamage,
            Power: 11,
            CooldownTurns: 2,
            FocusCost: 2,
            TargetSelf: false,
            TargetMode: BattleSkillTargetMode.AllEnemies)
    ];

    public static DialogueDefinition BuildGuardDialogue()
    {
        return new DialogueDefinition(
            id: GuardDialogueId,
            startNodeId: "start",
            nodes:
            [
                new DialogueNodeDefinition(
                    id: "start",
                    textKey: "dialogue.guard.start.text",
                    choices:
                    [
                        new DialogueChoiceDefinition(
                            id: "ask_dungeon",
                            textKey: "dialogue.guard.choice.dungeon",
                            nextNodeId: "info_dungeon",
                            effects:
                            [
                                new DialogueEffectDefinition(DialogueEffectType.SetWorldBool, FlagGuardBriefed, boolValue: true),
                                new DialogueEffectDefinition(DialogueEffectType.EmitTalkSignal, GuardActorId)
                            ]),
                        new DialogueChoiceDefinition(
                            id: "ask_tips",
                            textKey: "dialogue.guard.choice.tips",
                            nextNodeId: "info_tips"),
                        new DialogueChoiceDefinition(
                            id: "ask_fountain",
                            textKey: "dialogue.guard.choice.fountain",
                            nextNodeId: "info_fountain",
                            conditions:
                            [
                                new DialogueConditionDefinition(DialogueConditionType.WorldBoolEquals, FlagGuardBriefed, boolValue: true)
                            ]),
                        new DialogueChoiceDefinition(
                            id: "bye",
                            textKey: "dialogue.guard.choice.bye")
                    ]),
                new DialogueNodeDefinition(
                    id: "info_dungeon",
                    textKey: "dialogue.guard.info.dungeon.text",
                    choices:
                    [
                        new DialogueChoiceDefinition(id: "back", textKey: "dialogue.guard.choice.bye", nextNodeId: "start")
                    ]),
                new DialogueNodeDefinition(
                    id: "info_tips",
                    textKey: "dialogue.guard.info.tips.text",
                    choices:
                    [
                        new DialogueChoiceDefinition(id: "back", textKey: "dialogue.guard.choice.bye", nextNodeId: "start")
                    ]),
                new DialogueNodeDefinition(
                    id: "info_fountain",
                    textKey: "dialogue.guard.info.fountain.text",
                    choices:
                    [
                        new DialogueChoiceDefinition(id: "back", textKey: "dialogue.guard.choice.bye", nextNodeId: "start")
                    ])
            ]);
    }
}
