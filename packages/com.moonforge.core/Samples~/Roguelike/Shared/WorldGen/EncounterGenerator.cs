using Moonforge.Core.Combat;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Encounters;
using Moonforge.Core.Runtime.Random;

namespace Moonforge.Sample.Roguelike.WorldGen;

internal static class EncounterGenerator
{
    private const string HeroActorId = "party.hero";
    private const string GoldCurrencyId = "currency.gold";

    internal const string CryptTableId = "encounter.theme.crypt";
    internal const string WarrensTableId = "encounter.theme.warrens";
    internal const string CatacombsTableId = "encounter.theme.catacombs";
    internal const string InfernoTableId = "encounter.theme.inferno";

    /// <summary>
    /// Registers one <see cref="EncounterTableDefinition"/> per theme on <paramref name="catalog"/>.
    /// Each entry's <c>actorId</c> is a template ID; <see cref="EnemyTemplates"/> resolves the
    /// rolled ID back to an <see cref="EnemyTemplate"/> so depth scaling and affixes can be
    /// applied on top of the table's deterministic pick.
    /// </summary>
    public static void RegisterEncounterTables(InMemoryGameDefinitionCatalog catalog)
    {
        catalog.AddEncounterTable(new EncounterTableDefinition(
            CryptTableId,
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition("enemy.template.crypt.bonefiend", weight: 1),
                new EncounterEntryDefinition("enemy.template.crypt.faded_acolyte", weight: 1)
            ],
            displayName: "Crypt"));

        catalog.AddEncounterTable(new EncounterTableDefinition(
            WarrensTableId,
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition("enemy.template.warrens.rotfang_rat", weight: 1),
                new EncounterEntryDefinition("enemy.template.warrens.ghoul_runner", weight: 1)
            ],
            displayName: "Warrens"));

        catalog.AddEncounterTable(new EncounterTableDefinition(
            CatacombsTableId,
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition("enemy.template.catacombs.cultist", weight: 1),
                new EncounterEntryDefinition("enemy.template.catacombs.bone_archer", weight: 1),
                new EncounterEntryDefinition("enemy.template.catacombs.cult_acolyte", weight: 1)
            ],
            displayName: "Catacombs"));

        catalog.AddEncounterTable(new EncounterTableDefinition(
            InfernoTableId,
            EncounterRollMode.PickOne,
            [
                new EncounterEntryDefinition("enemy.template.inferno.ember_imp", weight: 1),
                new EncounterEntryDefinition("enemy.template.inferno.hell_hound", weight: 1)
            ],
            displayName: "Inferno"));
    }

    public static EncounterBlueprint Generate(
        int depth,
        int battleSequence,
        IRandomSource random,
        IGameDefinitionCatalog definitions)
    {
        EncounterTheme theme = ResolveTheme(depth, random);
        List<BattleSkillDefinition> skills = BuildCommonSkills();

        List<BattleActorDefinition> actors =
        [
            CreateHero(depth)
        ];

        int enemyCount = ResolveEnemyCount(depth, random);
        int championCount = 0;
        int eliteCount = 0;
        Dictionary<string, IReadOnlyDictionary<string, int>> actorResistances = new(StringComparer.Ordinal);

        if (!definitions.TryGetEncounterTable(theme.TableId, out EncounterTableDefinition themeTable))
        {
            throw new InvalidOperationException($"Encounter table '{theme.TableId}' was not registered.");
        }

        for (int i = 0; i < enemyCount; i++)
        {
            EnemyRank rank = ResolveEnemyRank(depth, random);
            if (rank == EnemyRank.Champion)
            {
                championCount++;
            }
            else if (rank == EnemyRank.Elite)
            {
                eliteCount++;
            }

            EncounterRollResult roll = EncounterResolver.Roll(random, themeTable);
            string templateId = roll.Spawns.Count > 0
                ? roll.Spawns[0].ActorId
                : throw new InvalidOperationException($"Encounter table '{theme.TableId}' rolled an empty result.");

            EnemyTemplate template = EnemyTemplates.Resolve(templateId);
            BattleActorDefinition enemyDefinition = CreateEnemy(theme, template, depth, battleSequence, i + 1, rank, random);
            actors.Add(enemyDefinition);
            if (template.Resistances is not null && template.Resistances.Count > 0)
            {
                actorResistances[enemyDefinition.ActorId] = template.Resistances;
            }
        }

        int rewardGold = (8 + (depth * 2)) + random.NextInt(6 + enemyCount) + (championCount * 5) + (eliteCount * 12);
        List<CurrencyDelta> rewardCurrency =
        [
            new CurrencyDelta(GoldCurrencyId, rewardGold)
        ];

        // Random item drops (herbs, potions) are rolled by the loot table after the
        // battle resolves; we pick a tier here based on the pack's strongest rank.
        string lootTableId = eliteCount > 0
            ? LootTableIds.EncounterElite
            : championCount > 0
                ? LootTableIds.EncounterChampion
                : LootTableIds.EncounterNormal;

        string rankText = eliteCount > 0
            ? $" [Elite x{eliteCount}]"
            : championCount > 0
                ? $" [Champion x{championCount}]"
                : string.Empty;
        string intro = $"{theme.DisplayName} pack ({enemyCount}) closes in.{rankText}";
        return new EncounterBlueprint(
            theme.Id,
            intro,
            actors,
            skills,
            rewardCurrency,
            RewardInventory: [],
            championCount,
            eliteCount,
            RewardLootTableId: lootTableId,
            ActorResistances: actorResistances);
    }

    public static EncounterBlueprint GenerateBoss(
        int depth,
        int battleSequence,
        IRandomSource random,
        IGameDefinitionCatalog definitions)
    {
        BossTemplate boss = ResolveBossTemplate(depth);
        List<BattleSkillDefinition> skills = BuildCommonSkills();
        // Slam is the boss's reliable melee hit — slightly randomized so consecutive
        // turns don't feel mechanical, with a small crit chance for the occasional spike.
        skills.Add(new BattleSkillDefinition(
            "skill.boss.slam",
            BattleSkillEffectType.PhysicalDamage,
            power: 12,
            damageVariancePercent: 12,
            critChancePercent: 10));

        // Nova is the boss's elemental signature: fire-typed AoE that fans across every
        // party member. The roguelike currently runs a one-hero party so the cast still
        // resolves on a single actor, but the skill is configured AoE so a future
        // multi-character party (companions, summons) gets the full sweep automatically.
        // 90% accuracy + 18% variance give it the "big telegraphed spell" feel.
        skills.Add(new BattleSkillDefinition(
            "skill.boss.nova",
            BattleSkillEffectType.MagicalDamage,
            power: 8,
            displayName: "Fire Nova",
            damageTypeId: StandardDamageTypes.Fire,
            targetMode: BattleSkillTargetMode.AllEnemies,
            accuracyPercent: 90,
            damageVariancePercent: 18));

        // Wreath of Flame is a self-buff: no damage, just applies a +matk status to the
        // boss for several turns so subsequent Novas hit harder. Cooldown 5 keeps it
        // from spamming — the AI re-casts it after the buff has expired.
        skills.Add(new BattleSkillDefinition(
            "skill.boss.wreath",
            BattleSkillEffectType.Buff,
            power: 0,
            cooldownTurns: 5,
            displayName: "Wreath of Flame",
            targetMode: BattleSkillTargetMode.Self,
            appliesStatuses:
            [
                new StatusApplicationDefinition("status.wreath_of_flame", StatusApplicationTarget.Self, chancePercent: 100)
            ]));

        // Cinderbrand is the debuff counterpart: single-target, applies a defense-down
        // status to the chosen enemy so subsequent Slams hit harder. Cooldown 4 keeps
        // the boss from spamming it.
        skills.Add(new BattleSkillDefinition(
            "skill.boss.cinderbrand",
            BattleSkillEffectType.Debuff,
            power: 0,
            cooldownTurns: 4,
            displayName: "Cinderbrand",
            targetMode: BattleSkillTargetMode.Single,
            appliesStatuses:
            [
                new StatusApplicationDefinition("status.cinderbrand", StatusApplicationTarget.Target, chancePercent: 100)
            ]));

        BattleActorDefinition bossActor = CreateBossEnemy(boss, depth, battleSequence);
        List<BattleActorDefinition> actors =
        [
            CreateHero(depth),
            bossActor
        ];

        Dictionary<string, IReadOnlyDictionary<string, int>> actorResistances = new(StringComparer.Ordinal);
        if (boss.Resistances is not null && boss.Resistances.Count > 0)
        {
            actorResistances[bossActor.ActorId] = boss.Resistances;
        }

        if (depth >= 6)
        {
            EncounterTheme supportTheme = ResolveTheme(depth, random);
            if (!definitions.TryGetEncounterTable(supportTheme.TableId, out EncounterTableDefinition supportTable))
            {
                throw new InvalidOperationException($"Encounter table '{supportTheme.TableId}' was not registered.");
            }

            EncounterRollResult roll = EncounterResolver.Roll(random, supportTable);
            if (roll.Spawns.Count > 0)
            {
                EnemyTemplate template = EnemyTemplates.Resolve(roll.Spawns[0].ActorId);
                BattleActorDefinition supportActor = CreateEnemy(supportTheme, template, depth, battleSequence, index: 2, rank: EnemyRank.Normal, random);
                actors.Add(supportActor);
                if (template.Resistances is not null && template.Resistances.Count > 0)
                {
                    actorResistances[supportActor.ActorId] = template.Resistances;
                }
            }
        }

        int rewardGold = 35 + (depth * 6) + random.NextInt(12);
        int rewardTokens = depth >= 9 ? 3 : 2;
        List<CurrencyDelta> rewardCurrency =
        [
            new CurrencyDelta(GoldCurrencyId, rewardGold),
            new CurrencyDelta("currency.token", rewardTokens)
        ];

        string intro = $"Boss floor {depth}: {boss.DisplayName} bars your path.";
        return new EncounterBlueprint(
            boss.ThemeId,
            intro,
            actors,
            skills,
            rewardCurrency,
            RewardInventory: [],
            ChampionCount: 0,
            EliteCount: 1,
            RewardLootTableId: LootTableIds.Boss,
            ActorResistances: actorResistances);
    }

    private static List<BattleSkillDefinition> BuildCommonSkills()
    {
        return
        [
            // 15% crit chance on the basic attack — visible enough to see a few crits
            // per dungeon run without making damage feel swingy.
            new BattleSkillDefinition(
                "skill.attack",
                BattleSkillEffectType.PhysicalDamage,
                power: 8,
                critChancePercent: 15),
            new BattleSkillDefinition("skill.potion", BattleSkillEffectType.Heal, power: 10),
            new BattleSkillDefinition("skill.claw", BattleSkillEffectType.PhysicalDamage, power: 5),
            new BattleSkillDefinition("skill.bolt", BattleSkillEffectType.MagicalDamage, power: 6),
            new BattleSkillDefinition("skill.heal", BattleSkillEffectType.Heal, power: 4),
            new BattleSkillDefinition(
                "skill.venom_bite",
                BattleSkillEffectType.PhysicalDamage,
                power: 3,
                displayName: "Venom Bite",
                appliesStatuses:
                [
                    new StatusApplicationDefinition("status.poison", StatusApplicationTarget.Target, chancePercent: 70)
                ])
        ];
    }

    private static BattleActorDefinition CreateHero(int depth)
    {
        int scaledAtk = 12 + (depth / 3);
        int scaledDef = 5 + (depth / 4);
        return new BattleActorDefinition(
            actorId: HeroActorId,
            displayName: "Hero",
            faction: CombatFaction.Party,
            maxHp: 36 + depth,
            atk: scaledAtk,
            def: scaledDef,
            matk: 4 + (depth / 4),
            mdef: 4 + (depth / 4),
            initiative: 20,
            skillIds: ["skill.attack", "skill.potion"],
            playerControlled: true);
    }

    private static BattleActorDefinition CreateEnemy(
        EncounterTheme theme,
        EnemyTemplate template,
        int depth,
        int battleSequence,
        int index,
        EnemyRank rank,
        IRandomSource random)
    {
        bool usesMagic = template.UsesMagic;
        int hpBonus = 0;
        int atkBonus = 0;
        int defBonus = 0;
        int matkBonus = 0;
        int mdefBonus = 0;
        int initiativeBonus = 0;
        string displayPrefix = string.Empty;

        if (rank != EnemyRank.Normal)
        {
            int affixCount = rank == EnemyRank.Elite ? 2 : 1;
            HashSet<EnemyAffix> usedAffixes = new();
            for (int i = 0; i < affixCount; i++)
            {
                EnemyAffix affix = RollUniqueAffix(random, usedAffixes);
                switch (affix)
                {
                    case EnemyAffix.Enraged:
                        atkBonus += 3;
                        initiativeBonus += 2;
                        break;
                    case EnemyAffix.Vampiric:
                        hpBonus += 4;
                        atkBonus += 1;
                        break;
                    case EnemyAffix.Bulwark:
                        hpBonus += 3;
                        defBonus += 2;
                        mdefBonus += 2;
                        break;
                    case EnemyAffix.Arcane:
                        usesMagic = true;
                        matkBonus += 3;
                        break;
                }
            }

            displayPrefix = rank == EnemyRank.Elite ? "Elite " : "Champion ";
        }

        int rankHpScale = rank switch
        {
            EnemyRank.Elite => 8,
            EnemyRank.Champion => 4,
            _ => 0
        };

        int rankAtkScale = rank switch
        {
            EnemyRank.Elite => 3,
            EnemyRank.Champion => 2,
            _ => 0
        };

        int hp = template.BaseHp + (depth * template.HpPerDepth) + random.NextInt(3) + hpBonus + rankHpScale;
        int atk = template.BaseAtk + (depth * template.AtkPerDepth) + atkBonus + rankAtkScale;
        int def = template.BaseDef + (depth / 3) + defBonus;
        int matk = (template.UsesMagic ? 4 + (depth / 2) : 2 + (depth / 4)) + matkBonus;
        int mdef = 2 + (depth / 4) + mdefBonus;
        int initiative = template.BaseInitiative + random.NextInt(3) + initiativeBonus;

        List<string> skillIds = usesMagic
            ? new List<string> { "skill.claw", "skill.bolt", "skill.heal" }
            : new List<string> { "skill.claw", "skill.heal" };
        if (string.Equals(theme.Id, "warrens", StringComparison.Ordinal))
        {
            skillIds.Add("skill.venom_bite");
        }

        int rankXpScale = rank switch
        {
            EnemyRank.Elite => 5,
            EnemyRank.Champion => 2,
            _ => 1
        };
        // Depth-scaled XP: linear early (Floor 1 unchanged), quadratic late so deep runs
        // level the hero meaningfully faster without trivializing the first few floors.
        // Floor 1 normal:  (4 + 1 + 0)   * 1 = 5   (unchanged)
        // Floor 10 normal: (4 + 10 + 12) * 1 = 26  (~+85%)
        // Floor 20 normal: (4 + 20 + 50) * 1 = 74  (~+208%)
        // Floor 30 normal: (4 + 30 + 112)* 1 = 146 (~+329%)
        long xpReward = (4 + depth + (depth * depth) / 8) * rankXpScale + (template.IsSupport ? 2 : 0);

        return new BattleActorDefinition(
            actorId: $"enemy.{theme.Id}.{battleSequence}.{index}",
            displayName: $"{displayPrefix}{template.DisplayName}",
            faction: CombatFaction.Enemy,
            maxHp: hp,
            atk: atk,
            def: def,
            matk: matk,
            mdef: mdef,
            initiative: initiative,
            skillIds: skillIds,
            playerControlled: false,
            aiPolicy: template.IsSupport ? BuildSupportEnemyAi() : BuildEnemyAi(usesMagic, useVenomBite: string.Equals(theme.Id, "warrens", StringComparison.Ordinal)),
            xpReward: xpReward);
    }

    private static BattleAiPolicyDefinition BuildSupportEnemyAi()
    {
        return new BattleAiPolicyDefinition(
            rules:
            [
                new BattleAiRuleDefinition(
                    skillId: "skill.heal",
                    priorityWeight: 110,
                    targetPolicy: BattleAiTargetPolicy.LowestHpAlly,
                    conditions: [new BattleAiConditionDefinition(BattleAiConditionType.AnyAllyHpBelowPercent, 65)]),
                new BattleAiRuleDefinition(
                    skillId: "skill.heal",
                    priorityWeight: 90,
                    targetPolicy: BattleAiTargetPolicy.Self,
                    conditions: [new BattleAiConditionDefinition(BattleAiConditionType.SelfHpBelowPercent, 40)])
            ],
            fallbackSkillId: "skill.bolt",
            fallbackTargetPolicy: BattleAiTargetPolicy.RandomEnemy);
    }

    private static BattleAiPolicyDefinition BuildEnemyAi(bool usesMagic, bool useVenomBite = false)
    {
        List<BattleAiRuleDefinition> rules =
        [
            new BattleAiRuleDefinition(
                skillId: "skill.heal",
                priorityWeight: 100,
                targetPolicy: BattleAiTargetPolicy.Self,
                conditions: [new BattleAiConditionDefinition(BattleAiConditionType.SelfHpBelowPercent, 45)])
        ];

        if (useVenomBite)
        {
            rules.Add(new BattleAiRuleDefinition(
                skillId: "skill.venom_bite",
                priorityWeight: 80,
                targetPolicy: BattleAiTargetPolicy.HighestThreatEnemy,
                conditions: [new BattleAiConditionDefinition(BattleAiConditionType.AnyEnemyHpBelowPercent, 100)]));
        }

        if (usesMagic)
        {
            rules.Add(new BattleAiRuleDefinition(
                skillId: "skill.bolt",
                priorityWeight: 70,
                targetPolicy: BattleAiTargetPolicy.HighestThreatEnemy,
                conditions: [new BattleAiConditionDefinition(BattleAiConditionType.AnyEnemyHpBelowPercent, 100)]));
        }

        return new BattleAiPolicyDefinition(
            rules: rules,
            fallbackSkillId: usesMagic ? "skill.bolt" : "skill.claw",
            fallbackTargetPolicy: BattleAiTargetPolicy.LowestHpEnemy);
    }

    private static EnemyAffix RollUniqueAffix(IRandomSource random, HashSet<EnemyAffix> usedAffixes)
    {
        EnemyAffix[] all =
        [
            EnemyAffix.Enraged,
            EnemyAffix.Vampiric,
            EnemyAffix.Bulwark,
            EnemyAffix.Arcane
        ];

        while (true)
        {
            EnemyAffix next = all[random.NextInt(all.Length)];
            if (usedAffixes.Add(next))
            {
                return next;
            }
        }
    }

    private static EnemyRank ResolveEnemyRank(int depth, IRandomSource random)
    {
        int championChance;
        int eliteChance;
        if (depth <= 2)
        {
            championChance = 8;
            eliteChance = 0;
        }
        else if (depth <= 5)
        {
            championChance = 15;
            eliteChance = 3;
        }
        else
        {
            championChance = 20;
            eliteChance = 8;
        }

        int roll = random.NextInt(100);
        if (roll < eliteChance)
        {
            return EnemyRank.Elite;
        }

        if (roll < eliteChance + championChance)
        {
            return EnemyRank.Champion;
        }

        return EnemyRank.Normal;
    }

    private static int ResolveEnemyCount(int depth, IRandomSource random)
    {
        int roll = random.NextInt(100);
        if (depth <= 2)
        {
            return roll < 75 ? 1 : 2;
        }

        if (depth <= 5)
        {
            return roll < 35 ? 1 : (roll < 85 ? 2 : 3);
        }

        if (depth <= 8)
        {
            // Mid-game (Inferno entry) is the toughest band — 45% chance of a 3-enemy pack.
            return roll < 55 ? 2 : 3;
        }

        if (depth <= 11)
        {
            // Deep floors lean toward 2-enemy packs so attrition doesn't dominate.
            return roll < 75 ? 2 : 3;
        }

        // Endgame floors (12+): 2-enemy packs are the norm; the difficulty comes from
        // higher enemy stats and the boss fights, not pack size.
        return roll < 90 ? 2 : 3;
    }

    private static EncounterTheme ResolveTheme(int depth, IRandomSource random)
    {
        if (depth <= 2)
        {
            return random.NextInt(100) < 65 ? Themes.Crypt : Themes.Warrens;
        }

        if (depth <= 5)
        {
            int roll = random.NextInt(100);
            if (roll < 40)
            {
                return Themes.Warrens;
            }

            return roll < 80 ? Themes.Catacombs : Themes.Crypt;
        }

        return random.NextInt(100) < 55 ? Themes.Inferno : Themes.Catacombs;
    }

    private static BossTemplate ResolveBossTemplate(int depth)
    {
        if (depth <= 3)
        {
            return Bosses.CryptWarden;
        }

        if (depth <= 6)
        {
            return Bosses.CatacombHierophant;
        }

        return Bosses.InfernalMastiff;
    }

    private static BattleActorDefinition CreateBossEnemy(BossTemplate boss, int depth, int battleSequence)
    {
        int hp = boss.BaseHp + (depth * boss.HpPerDepth);
        int atk = boss.BaseAtk + (depth * boss.AtkPerDepth);
        int def = boss.BaseDef + (depth / 2);
        int matk = boss.BaseMatk + (depth / 2);
        int mdef = boss.BaseMdef + (depth / 3);
        int initiative = boss.BaseInitiative + (depth / 4);
        return new BattleActorDefinition(
            actorId: $"enemy.boss.{boss.ThemeId}.{battleSequence}.1",
            displayName: $"Boss {boss.DisplayName}",
            faction: CombatFaction.Enemy,
            maxHp: hp,
            atk: atk,
            def: def,
            matk: matk,
            mdef: mdef,
            initiative: initiative,
            skillIds: ["skill.boss.slam", "skill.boss.nova", "skill.boss.wreath", "skill.boss.cinderbrand", "skill.heal"],
            playerControlled: false,
            aiPolicy: BuildBossAi(),
            // Boss XP also scales quadratically with depth.
            // Floor 3 boss:  30 + 24  + 4   = 58  (+7%)
            // Floor 9 boss:  30 + 72  + 40  = 142 (+39%)
            // Floor 15 boss: 30 + 120 + 112 = 262 (+75%)
            // Floor 30 boss: 30 + 240 + 450 = 720 (+167%)
            xpReward: 30 + (depth * 8) + ((depth * depth) / 2));
    }

    private static BattleAiPolicyDefinition BuildBossAi()
    {
        return new BattleAiPolicyDefinition(
            rules:
            [
                new BattleAiRuleDefinition(
                    skillId: "skill.heal",
                    priorityWeight: 120,
                    targetPolicy: BattleAiTargetPolicy.Self,
                    conditions: [new BattleAiConditionDefinition(BattleAiConditionType.SelfHpBelowPercent, 40)]),
                new BattleAiRuleDefinition(
                    skillId: "skill.boss.wreath",
                    priorityWeight: 100,
                    targetPolicy: BattleAiTargetPolicy.Self,
                    conditions: []),
                new BattleAiRuleDefinition(
                    skillId: "skill.boss.cinderbrand",
                    priorityWeight: 95,
                    targetPolicy: BattleAiTargetPolicy.HighestThreatEnemy,
                    conditions: []),
                new BattleAiRuleDefinition(
                    skillId: "skill.boss.nova",
                    priorityWeight: 90,
                    targetPolicy: BattleAiTargetPolicy.HighestThreatEnemy,
                    conditions: [new BattleAiConditionDefinition(BattleAiConditionType.AnyEnemyHpBelowPercent, 100)])
            ],
            fallbackSkillId: "skill.boss.slam",
            fallbackTargetPolicy: BattleAiTargetPolicy.LowestHpEnemy);
    }

    private static class Themes
    {
        public static readonly EncounterTheme Crypt = new("crypt", "Crypt", CryptTableId);
        public static readonly EncounterTheme Warrens = new("warrens", "Warrens", WarrensTableId);
        public static readonly EncounterTheme Catacombs = new("catacombs", "Catacombs", CatacombsTableId);
        public static readonly EncounterTheme Inferno = new("inferno", "Inferno", InfernoTableId);
    }

    private static class Bosses
    {
        public static readonly BossTemplate CryptWarden = new(
            ThemeId: "crypt",
            DisplayName: "Grave Warden",
            BaseHp: 44,
            HpPerDepth: 4,
            BaseAtk: 8,
            AtkPerDepth: 1,
            BaseDef: 4,
            BaseMatk: 6,
            BaseMdef: 4,
            BaseInitiative: 12);

        public static readonly BossTemplate CatacombHierophant = new(
            ThemeId: "catacombs",
            DisplayName: "Hierophant of Ash",
            BaseHp: 54,
            HpPerDepth: 4,
            BaseAtk: 9,
            AtkPerDepth: 1,
            BaseDef: 5,
            BaseMatk: 8,
            BaseMdef: 5,
            BaseInitiative: 13);

        public static readonly BossTemplate InfernalMastiff = new(
            ThemeId: "inferno",
            DisplayName: "Infernal Mastiff",
            BaseHp: 60,
            HpPerDepth: 2,
            BaseAtk: 10,
            AtkPerDepth: 1,
            BaseDef: 6,
            BaseMatk: 9,
            BaseMdef: 6,
            BaseInitiative: 14,
            // Immune to fire (its own element); vulnerable to ice. Demonstrates 100% immunity
            // and negative-resistance vulnerability in the same actor.
            Resistances: new Dictionary<string, int> { ["res.fire"] = 100, ["res.ice"] = -50 });
    }

    /// <summary>
    /// Lookup from the template actor IDs referenced by <see cref="EncounterTableDefinition"/>
    /// back to the per-template stats and behavior flags. Encounter tables tell us *which*
    /// enemy type spawned; this table tells us *what they look like*.
    /// </summary>
    private static class EnemyTemplates
    {
        private static readonly Dictionary<string, EnemyTemplate> _byId = new(StringComparer.Ordinal)
        {
            ["enemy.template.crypt.bonefiend"] =
                new EnemyTemplate("Bone Fiend", BaseHp: 14, HpPerDepth: 2, BaseAtk: 5, AtkPerDepth: 1, BaseDef: 1, BaseInitiative: 13, UsesMagic: false),
            ["enemy.template.crypt.faded_acolyte"] =
                new EnemyTemplate("Faded Acolyte", BaseHp: 12, HpPerDepth: 2, BaseAtk: 4, AtkPerDepth: 1, BaseDef: 1, BaseInitiative: 12, UsesMagic: true),
            ["enemy.template.warrens.rotfang_rat"] =
                new EnemyTemplate("Rotfang Rat", BaseHp: 11, HpPerDepth: 2, BaseAtk: 5, AtkPerDepth: 1, BaseDef: 0, BaseInitiative: 15, UsesMagic: false),
            ["enemy.template.warrens.ghoul_runner"] =
                new EnemyTemplate("Ghoul Runner", BaseHp: 13, HpPerDepth: 2, BaseAtk: 5, AtkPerDepth: 1, BaseDef: 1, BaseInitiative: 14, UsesMagic: false),
            ["enemy.template.catacombs.cultist"] =
                new EnemyTemplate("Cultist", BaseHp: 15, HpPerDepth: 2, BaseAtk: 5, AtkPerDepth: 1, BaseDef: 1, BaseInitiative: 13, UsesMagic: true),
            ["enemy.template.catacombs.bone_archer"] =
                new EnemyTemplate("Bone Archer", BaseHp: 14, HpPerDepth: 2, BaseAtk: 6, AtkPerDepth: 1, BaseDef: 1, BaseInitiative: 14, UsesMagic: false),
            ["enemy.template.catacombs.cult_acolyte"] =
                new EnemyTemplate("Cult Acolyte", BaseHp: 13, HpPerDepth: 2, BaseAtk: 3, AtkPerDepth: 1, BaseDef: 1, BaseInitiative: 12, UsesMagic: true, IsSupport: true),
            ["enemy.template.inferno.ember_imp"] =
                new EnemyTemplate("Ember Imp", BaseHp: 16, HpPerDepth: 2, BaseAtk: 6, AtkPerDepth: 1, BaseDef: 2, BaseInitiative: 14, UsesMagic: true,
                    Resistances: new Dictionary<string, int> { ["res.fire"] = 75, ["res.ice"] = -25 }),
            ["enemy.template.inferno.hell_hound"] =
                new EnemyTemplate("Hell Hound", BaseHp: 18, HpPerDepth: 2, BaseAtk: 7, AtkPerDepth: 1, BaseDef: 2, BaseInitiative: 16, UsesMagic: false,
                    Resistances: new Dictionary<string, int> { ["res.fire"] = 50, ["res.ice"] = -25 })
        };

        public static EnemyTemplate Resolve(string templateActorId)
        {
            if (!_byId.TryGetValue(templateActorId, out EnemyTemplate? template))
            {
                throw new InvalidOperationException($"Unknown enemy template '{templateActorId}'.");
            }

            return template;
        }
    }
}

internal sealed record EncounterBlueprint(
    string ThemeId,
    string IntroText,
    IReadOnlyList<BattleActorDefinition> Actors,
    IReadOnlyList<BattleSkillDefinition> Skills,
    IReadOnlyList<CurrencyDelta> RewardCurrency,
    IReadOnlyList<InventoryDelta> RewardInventory,
    int ChampionCount,
    int EliteCount,
    string? RewardLootTableId = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? ActorResistances = null);

internal static class LootTableIds
{
    public const string EncounterNormal = "loot.encounter.normal";
    public const string EncounterChampion = "loot.encounter.champion";
    public const string EncounterElite = "loot.encounter.elite";
    public const string Boss = "loot.boss";
}

internal sealed record EncounterTheme(
    string Id,
    string DisplayName,
    string TableId);

internal sealed record EnemyTemplate(
    string DisplayName,
    int BaseHp,
    int HpPerDepth,
    int BaseAtk,
    int AtkPerDepth,
    int BaseDef,
    int BaseInitiative,
    bool UsesMagic,
    bool IsSupport = false,
    IReadOnlyDictionary<string, int>? Resistances = null);

internal sealed record BossTemplate(
    string ThemeId,
    string DisplayName,
    int BaseHp,
    int HpPerDepth,
    int BaseAtk,
    int AtkPerDepth,
    int BaseDef,
    int BaseMatk,
    int BaseMdef,
    int BaseInitiative,
    IReadOnlyDictionary<string, int>? Resistances = null);

internal enum EnemyRank
{
    Normal = 0,
    Champion = 1,
    Elite = 2
}

internal enum EnemyAffix
{
    Enraged = 0,
    Vampiric = 1,
    Bulwark = 2,
    Arcane = 3
}
