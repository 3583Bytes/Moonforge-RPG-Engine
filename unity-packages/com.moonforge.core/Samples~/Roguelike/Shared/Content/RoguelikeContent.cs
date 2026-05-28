using System;
using System.Collections.Generic;
using Moonforge.Core.Combat;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Dialogue;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Interactables;
using Moonforge.Core.Loot;
using Moonforge.Core.Stats;
using Moonforge.Sample.Roguelike.WorldGen;

namespace Moonforge.Sample.Roguelike.Content
{

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
        new[]
        {
            "quest.contract.hunt.warrens",
            "quest.contract.remedy",
            "quest.contract.guard.patrol",
            "quest.contract.scouting"
        };

        public static readonly ClassProfile[] ClassProfiles =
        new[]
        {
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
        };

        public static readonly GearMetadata[] GearCatalog =
        new[]
        {
            new GearMetadata(ItemBronzeBlade, "Bronze Blade", SlotWeapon,    Atk: 2, Def: 0, Matk: 0, Mdef: 0, Initiative: 0),
            new GearMetadata(ItemOakWand,     "Oak Wand",    SlotWeapon,    Atk: 0, Def: 0, Matk: 2, Mdef: 0, Initiative: 0, GrantedSkillIds: new[] {"skill.bolt"}),
            new GearMetadata(ItemLeatherVest, "Leather Vest", SlotArmor,    Atk: 0, Def: 2, Matk: 0, Mdef: 0, Initiative: 0),
            new GearMetadata(ItemMysticRobe,  "Mystic Robe", SlotArmor,     Atk: 0, Def: 0, Matk: 1, Mdef: 2, Initiative: 0),
            new GearMetadata(ItemIronRing,    "Iron Ring",   SlotAccessory, Atk: 1, Def: 1, Matk: 0, Mdef: 0, Initiative: 0, Crit: 5),
            new GearMetadata(ItemLuckyCharm,  "Lucky Charm", SlotAccessory, Atk: 0, Def: 0, Matk: 0, Mdef: 1, Initiative: 2, Acc: 5, Eva: 5)
        };

        public static readonly MetaUnlockDefinition[] MetaUnlockDefinitions =
        new[]
        {
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
        };

        public static readonly ClassAbilityDefinition[] ClassAbilities =
        new[]
        {
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
        };

        public static DialogueDefinition BuildGuardDialogue()
        {
            return new DialogueDefinition(
                id: GuardDialogueId,
                startNodeId: "start",
                nodes:
                new[]
                {
                    new DialogueNodeDefinition(
                        id: "start",
                        textKey: "dialogue.guard.start.text",
                        choices:
                        new[]
                        {
                            new DialogueChoiceDefinition(
                                id: "ask_dungeon",
                                textKey: "dialogue.guard.choice.dungeon",
                                nextNodeId: "info_dungeon",
                                effects:
                                new[]
                                {
                                    new DialogueEffectDefinition(DialogueEffectType.SetWorldBool, FlagGuardBriefed, boolValue: true),
                                    new DialogueEffectDefinition(DialogueEffectType.EmitTalkSignal, GuardActorId)
                                }),
                            new DialogueChoiceDefinition(
                                id: "ask_tips",
                                textKey: "dialogue.guard.choice.tips",
                                nextNodeId: "info_tips"),
                            new DialogueChoiceDefinition(
                                id: "ask_fountain",
                                textKey: "dialogue.guard.choice.fountain",
                                nextNodeId: "info_fountain",
                                conditions:
                                new[]
                                {
                                    new DialogueConditionDefinition(DialogueConditionType.WorldBoolEquals, FlagGuardBriefed, boolValue: true)
                                }),
                            new DialogueChoiceDefinition(
                                id: "bye",
                                textKey: "dialogue.guard.choice.bye")
                        }),
                    new DialogueNodeDefinition(
                        id: "info_dungeon",
                        textKey: "dialogue.guard.info.dungeon.text",
                        choices:
                        new[]
                        {
                            new DialogueChoiceDefinition(id: "back", textKey: "dialogue.guard.choice.bye", nextNodeId: "start")
                        }),
                    new DialogueNodeDefinition(
                        id: "info_tips",
                        textKey: "dialogue.guard.info.tips.text",
                        choices:
                        new[]
                        {
                            new DialogueChoiceDefinition(id: "back", textKey: "dialogue.guard.choice.bye", nextNodeId: "start")
                        }),
                    new DialogueNodeDefinition(
                        id: "info_fountain",
                        textKey: "dialogue.guard.info.fountain.text",
                        choices:
                        new[]
                        {
                            new DialogueChoiceDefinition(id: "back", textKey: "dialogue.guard.choice.bye", nextNodeId: "start")
                        })
                });
        }

        /// <summary>
        /// Builds the full roguelike-sample <see cref="IGameDefinitionCatalog"/>: currencies,
        /// items, quests, shop, dialogue, status effects, experience curve, recipe, equipment
        /// slots, stats, damage types, gear catalog, encounter loot tables, encounter tables, and
        /// town interactables. Called once during run setup; the result is held on
        /// <c>GameState</c>-less <c>CommandContext</c>s for the lifetime of the run.
        /// </summary>
        public static IGameDefinitionCatalog BuildCatalog()
        {
            InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
                .AddCurrency(new CurrencyDefinition(GoldCurrencyId, 999_999))
                .AddCurrency(new CurrencyDefinition(TokenCurrencyId, 999))
                .AddItem(new ItemDefinition(
                    MediumPotionItemId,
                    10,
                    buyPriceOptions:
                    new[]
                    {
                        new PriceOptionDefinition(new[] {new PriceComponentDefinition(GoldCurrencyId, 18)}),
                        new PriceOptionDefinition(new[] {new PriceComponentDefinition(TokenCurrencyId, 1)})
                    },
                    sellPrice: new[] {new PriceComponentDefinition(GoldCurrencyId, 8)}))
                .AddItem(new ItemDefinition(
                    HerbItemId,
                    20,
                    buyPriceOptions:
                    new[]
                    {
                        new PriceOptionDefinition(new[] {new PriceComponentDefinition(GoldCurrencyId, 4)})
                    },
                    sellPrice: new[] {new PriceComponentDefinition(GoldCurrencyId, 1)}))
                .AddItem(new ItemDefinition(ItemBronzeBlade, 1))
                .AddItem(new ItemDefinition(ItemOakWand, 1))
                .AddItem(new ItemDefinition(ItemLeatherVest, 1))
                .AddItem(new ItemDefinition(ItemMysticRobe, 1))
                .AddItem(new ItemDefinition(ItemIronRing, 1))
                .AddItem(new ItemDefinition(ItemLuckyCharm, 1))
                .AddQuest(new QuestDefinition(
                    id: "quest.contract.hunt.warrens",
                    objectives:
                    new[]
                    {
                        new QuestObjectiveDefinition("obj.kill.warrens", QuestObjectiveType.Kill, targetId: "theme.warrens", requiredCount: 3, displayName: "Warrens packs culled")
                    },
                    displayName: "Vermin Purge",
                    description: "Cull 3 Warrens packs",
                    rewardCurrency: new[] {new CurrencyDelta(GoldCurrencyId, 32), new CurrencyDelta(TokenCurrencyId, 1)}))
                .AddQuest(new QuestDefinition(
                    id: "quest.contract.remedy",
                    objectives:
                    new[]
                    {
                        new QuestObjectiveDefinition("obj.collect.herb", QuestObjectiveType.Collect, targetId: HerbItemId, requiredCount: 4, displayName: "Herbs gathered"),
                        new QuestObjectiveDefinition("obj.visit.healer", QuestObjectiveType.Visit, targetId: VisitHealerTargetId, requiredCount: 1, displayName: "Healer visited"),
                        new QuestObjectiveDefinition(
                            "obj.root.remedy.and",
                            QuestObjectiveType.CompositeAnd,
                            childObjectiveIds: new[] {"obj.collect.herb", "obj.visit.healer"})
                    },
                    rootObjectiveIds: new[] {"obj.root.remedy.and"},
                    displayName: "Remedy Supply",
                    description: "Gather 4 herbs and visit the healer",
                    rewardCurrency: new[] {new CurrencyDelta(GoldCurrencyId, 36), new CurrencyDelta(TokenCurrencyId, 1)}))
                .AddQuest(new QuestDefinition(
                    id: "quest.contract.guard.patrol",
                    objectives:
                    new[]
                    {
                        new QuestObjectiveDefinition("obj.talk.guard", QuestObjectiveType.Talk, targetId: GuardActorId, requiredCount: 1, displayName: "Guard reports"),
                        new QuestObjectiveDefinition("obj.visit.fountain", QuestObjectiveType.Visit, targetId: VisitFountainTargetId, requiredCount: 1, displayName: "Fountain inspected"),
                        new QuestObjectiveDefinition(
                            "obj.root.patrol.and",
                            QuestObjectiveType.CompositeAnd,
                            childObjectiveIds: new[] {"obj.talk.guard", "obj.visit.fountain"})
                    },
                    rootObjectiveIds: new[] {"obj.root.patrol.and"},
                    displayName: "Patrol Duty",
                    description: "Speak to guard and inspect fountain",
                    rewardCurrency: new[] {new CurrencyDelta(GoldCurrencyId, 28), new CurrencyDelta(TokenCurrencyId, 1)}))
                .AddQuest(new QuestDefinition(
                    id: "quest.contract.scouting",
                    objectives:
                    new[]
                    {
                        new QuestObjectiveDefinition("obj.kill.crypt", QuestObjectiveType.Kill, targetId: "theme.crypt", requiredCount: 2, displayName: "Crypt packs culled"),
                        new QuestObjectiveDefinition("obj.kill.warrens", QuestObjectiveType.Kill, targetId: "theme.warrens", requiredCount: 2, displayName: "Warrens packs culled"),
                        new QuestObjectiveDefinition(
                            "obj.root.scout.or",
                            QuestObjectiveType.CompositeOr,
                            childObjectiveIds: new[] {"obj.kill.crypt", "obj.kill.warrens"})
                    },
                    rootObjectiveIds: new[] {"obj.root.scout.or"},
                    displayName: "Scouting Order",
                    description: "Eliminate 2 Crypt or 2 Warrens packs",
                    rewardCurrency: new[] {new CurrencyDelta(GoldCurrencyId, 45), new CurrencyDelta(TokenCurrencyId, 2)}))
                .AddShop(new ShopDefinition(
                    TownShopId,
                    new[]
                    {
                        new ShopEntryDefinition(MediumPotionItemId, maxStock: 4),
                        new ShopEntryDefinition(HerbItemId)
                    },
                    restockIntervalMinutes: 30))
                .AddDialogue(BuildGuardDialogue())
                .AddStatusEffect(new StatusEffectDefinition(
                    id: "status.poison",
                    durationTurns: 3,
                    tickHpDelta: -2,
                    displayName: "Poison",
                    description: "Loses 2 HP at the start of each turn for 3 turns."))
                .AddStatusEffect(new StatusEffectDefinition(
                    id: "status.wreath_of_flame",
                    durationTurns: 4,
                    statModifiers: new Dictionary<string, int> { ["matk"] = 5 },
                    displayName: "Wreath of Flame",
                    description: "Magical attack power is significantly increased."))
                .AddStatusEffect(new StatusEffectDefinition(
                    id: "status.cinderbrand",
                    durationTurns: 3,
                    statModifiers: new Dictionary<string, int> { ["def"] = -3 },
                    displayName: "Cinderbrand",
                    description: "Smoldering brand on the skin. Physical defense is reduced."))
                .AddExperienceCurve(new ExperienceCurveDefinition(
                    id: HeroCurveId,
                    // Levels 2-15: original curve (~1.3x growth per level). Levels 16-25: extended
                    // with gentler ~1.15-1.2x growth so deep runs can keep leveling. Combined
                    // with the depth-scaled XP formula above, a deep run can plausibly reach
                    // level 20+ in a single descent.
                    xpThresholds: new long[]
                    {
                        20, 60, 120, 200, 320, 480, 700, 1000, 1400, 1900,
                        2500, 3200, 4000, 5000,
                        6200, 7600, 9200, 11000, 13000, 15200, 17600, 20200, 23000, 26000
                    },
                    displayName: "Hero Curve",
                    // Each level adds Flat stat modifiers via LevelUpStatGrowthReactor. Vit
                    // also propagates to MaxHp through the derived MaxHp formula.
                    statGainsPerLevel: new Dictionary<string, int>
                    {
                        [StandardStats.Vitality] = 1,
                        ["atk"] = 1,
                        ["def"] = 1
                    }))
                .AddRecipe(new RecipeDefinition(
                    id: AlchemistBrewRecipeId,
                    difficulty: 1,
                    successChanceAtEqualSkill: 0.75,
                    skillDeltaPerPoint: 0.05,
                    minSuccessChance: 0.5,
                    maxSuccessChance: 0.95,
                    failConsumePolicy: CraftFailConsumePolicy.ConsumeAll,
                    ingredients: new[] {new CraftIngredientDefinition(HerbItemId, 2)},
                    currencyCosts: new[] {new CraftCurrencyCostDefinition(GoldCurrencyId, 5)},
                    outputs: new[] {new CraftOutputDefinition(MediumPotionItemId, 1)}))
                .AddEquipmentSlot(new EquipmentSlotDefinition(SlotWeapon, "Weapon"))
                .AddEquipmentSlot(new EquipmentSlotDefinition(SlotArmor, "Armor"))
                .AddEquipmentSlot(new EquipmentSlotDefinition(SlotAccessory, "Accessory"))
                .AddStat(new StatDefinition(StandardStats.Vitality, displayName: "Vitality"))
                // MaxHp is derived: class vitality + 4 HP per level beyond the first.
                .AddStat(new StatDefinition(
                    StandardStats.MaxHp,
                    min: 1,
                    derivedFromFormula: "vit + (level - 1) * 4",
                    displayName: "Max HP"))
                // Damage types: physical/magical keep flat defense; elementals are pure-resistance.
                // Skills with no DamageTypeId still fall through these — the runtime resolves
                // PhysicalDamage → "physical" and MagicalDamage → "magical" by default.
                .AddDamageType(new DamageTypeDefinition(
                    StandardDamageTypes.Physical,
                    attackStatId: StandardStats.Attack,
                    flatDefenseStatId: StandardStats.Defense,
                    resistanceStatId: StandardStats.ResistancePhysical,
                    displayName: "Physical"))
                .AddDamageType(new DamageTypeDefinition(
                    StandardDamageTypes.Magical,
                    attackStatId: StandardStats.MagicAttack,
                    flatDefenseStatId: StandardStats.MagicDefense,
                    resistanceStatId: StandardStats.ResistanceMagical,
                    displayName: "Magical"))
                .AddDamageType(new DamageTypeDefinition(
                    StandardDamageTypes.Fire,
                    attackStatId: StandardStats.MagicAttack,
                    flatDefenseStatId: null,
                    resistanceStatId: StandardStats.ResistanceFire,
                    displayName: "Fire"))
                .AddDamageType(new DamageTypeDefinition(
                    StandardDamageTypes.Ice,
                    attackStatId: StandardStats.MagicAttack,
                    flatDefenseStatId: null,
                    resistanceStatId: StandardStats.ResistanceIce,
                    displayName: "Ice"))
                .AddDamageType(new DamageTypeDefinition(
                    StandardDamageTypes.Holy,
                    attackStatId: StandardStats.MagicAttack,
                    flatDefenseStatId: null,
                    resistanceStatId: StandardStats.ResistanceHoly,
                    displayName: "Holy"))
                .AddDamageType(new DamageTypeDefinition(
                    StandardDamageTypes.Dark,
                    attackStatId: StandardStats.MagicAttack,
                    flatDefenseStatId: null,
                    resistanceStatId: StandardStats.ResistanceDark,
                    displayName: "Dark"));

            for (int i = 0; i < GearCatalog.Length; i++)
            {
                definitions.AddEquipment(GearCatalog[i].ToEquipmentDefinition());
            }

            RegisterEncounterLootTables(definitions);
            EncounterGenerator.RegisterEncounterTables(definitions);
            RegisterTownInteractables(definitions);

            return definitions;
        }

        private static void RegisterEncounterLootTables(InMemoryGameDefinitionCatalog catalog)
        {
            // Bonus item drops layered on top of the per-encounter static gold reward. Items here
            // are random rolls (RollEach mode); deterministic gold/token scaling stays on the
            // EncounterBlueprint's RewardCurrency list.
            catalog.AddLootTable(new LootTableDefinition(
                LootTableIds.EncounterNormal,
                LootRollMode.RollEach,
                new[]
                {
                    LootEntryDefinition.Item(HerbItemId, chancePercent: 30)
                }));

            catalog.AddLootTable(new LootTableDefinition(
                LootTableIds.EncounterChampion,
                LootRollMode.RollEach,
                new[]
                {
                    LootEntryDefinition.Item(HerbItemId, chancePercent: 55),
                    LootEntryDefinition.Item(MediumPotionItemId, chancePercent: 20),
                    LootEntryDefinition.Currency(GoldCurrencyId, chancePercent: 100, minQuantity: 4, maxQuantity: 12)
                }));

            catalog.AddLootTable(new LootTableDefinition(
                LootTableIds.EncounterElite,
                LootRollMode.RollEach,
                new[]
                {
                    LootEntryDefinition.Item(HerbItemId, chancePercent: 75),
                    LootEntryDefinition.Item(MediumPotionItemId, chancePercent: 40),
                    LootEntryDefinition.Currency(GoldCurrencyId, chancePercent: 100, minQuantity: 10, maxQuantity: 30)
                }));

            catalog.AddLootTable(new LootTableDefinition(
                LootTableIds.Boss,
                LootRollMode.RollEach,
                new[]
                {
                    LootEntryDefinition.Item(HerbItemId, chancePercent: 75),
                    LootEntryDefinition.Item(MediumPotionItemId, chancePercent: 55),
                    LootEntryDefinition.Currency(GoldCurrencyId, chancePercent: 100, minQuantity: 25, maxQuantity: 60)
                }));
        }

        private static void RegisterTownInteractables(InMemoryGameDefinitionCatalog catalog)
        {
            // Town supply cache: fixed reward, one-time use. The interactable's Consumed status
            // becomes the source of truth for "have I looted this?", replacing the previous
            // bool field on the save data.
            catalog.AddLootTable(new LootTableDefinition(
                CacheLootTableId,
                LootRollMode.RollEach,
                new[]
                {
                    LootEntryDefinition.Currency(GoldCurrencyId, chancePercent: 100, minQuantity: 12, maxQuantity: 12),
                    LootEntryDefinition.Item(HerbItemId, chancePercent: 100, minQuantity: 2, maxQuantity: 2)
                }));
            catalog.AddInteractable(new InteractableDefinition(
                CacheInteractableDefId,
                effects: new[] {new InteractableEffectDefinition(InteractableEffectKind.GrantLootTable, CacheLootTableId)},
                maxUses: 1,
                displayName: "Supply Cache"));

            // Town fountain: unlimited interaction, fires an interaction signal that the game
            // forwards to the quest system (and uses for flavor text).
            catalog.AddInteractable(new InteractableDefinition(
                FountainInteractableDefId,
                effects: new[] {new InteractableEffectDefinition(InteractableEffectKind.EmitInteractionSignal, FountainSignalKey)},
                maxUses: -1,
                displayName: "Fountain"));
        }
    }
}
