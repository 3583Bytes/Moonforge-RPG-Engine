using System;
using System.Collections.Generic;

namespace Moonforge.Core.Combat
{

    public sealed class BattleSkillDefinition
    {
        private static readonly IReadOnlyDictionary<string, int> EmptyCosts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private static readonly IReadOnlyList<StatusApplicationDefinition> EmptyApplications =
            System.Array.Empty<StatusApplicationDefinition>();

        public BattleSkillDefinition(
            string id,
            BattleSkillEffectType effectType,
            int power,
            int cooldownTurns = 0,
            IReadOnlyDictionary<string, int>? resourceCosts = null,
            string? displayName = null,
            string? description = null,
            IReadOnlyList<StatusApplicationDefinition>? appliesStatuses = null,
            string? damageTypeId = null,
            BattleSkillTargetMode targetMode = BattleSkillTargetMode.Single,
            int accuracyPercent = 100,
            int damageVariancePercent = 0,
            int critChancePercent = 0,
            int critMultiplierPercent = 200,
            int maxPp = 0)
        {
            Id = id;
            EffectType = effectType;
            Power = power;
            CooldownTurns = cooldownTurns < 0 ? 0 : cooldownTurns;
            ResourceCosts = resourceCosts ?? EmptyCosts;
            DisplayName = displayName;
            Description = description;
            AppliesStatuses = appliesStatuses ?? EmptyApplications;
            DamageTypeId = string.IsNullOrWhiteSpace(damageTypeId) ? null : damageTypeId;
            TargetMode = targetMode;
            AccuracyPercent = accuracyPercent < 0 ? 0 : (accuracyPercent > 100 ? 100 : accuracyPercent);
            DamageVariancePercent = damageVariancePercent < 0 ? 0 : (damageVariancePercent > 100 ? 100 : damageVariancePercent);
            CritChancePercent = critChancePercent < 0 ? 0 : (critChancePercent > 100 ? 100 : critChancePercent);
            CritMultiplierPercent = critMultiplierPercent < 100 ? 100 : critMultiplierPercent;
            MaxPp = maxPp < 0 ? 0 : maxPp;
        }

        public string Id { get; }

        public BattleSkillEffectType EffectType { get; }

        public int Power { get; }

        public int CooldownTurns { get; }

        public IReadOnlyDictionary<string, int> ResourceCosts { get; }

        public string? DisplayName { get; }

        public string? Description { get; }

        public IReadOnlyList<StatusApplicationDefinition> AppliesStatuses { get; }

        /// <summary>
        /// Overrides the damage type id used when looking up <see cref="DamageTypeDefinition"/>.
        /// When null, the runtime falls back to the effect type's default id
        /// (PhysicalDamage → <c>"physical"</c>, MagicalDamage → <c>"magical"</c>).
        /// </summary>
        public string? DamageTypeId { get; }

        /// <summary>
        /// How the skill resolves targets. <see cref="BattleSkillTargetMode.Single"/> (default)
        /// uses the explicit target supplied with the command; the other modes fan the effect
        /// out across multiple actors and the command's target is ignored.
        /// </summary>
        public BattleSkillTargetMode TargetMode { get; }

        /// <summary>
        /// Probability (0–100) that the skill lands on a given target. 100 means it never
        /// misses. Rolled once per target; on a miss the skill's effect is skipped and a
        /// <see cref="Events.BattleActionMissedEvent"/> is published.
        /// </summary>
        public int AccuracyPercent { get; }

        /// <summary>
        /// Per-skill damage/heal randomization range, expressed as ± percent. 0 means
        /// deterministic damage. 15 means each hit is rolled in [85%, 115%] of the base
        /// resolved amount. Rolled once per target.
        /// </summary>
        public int DamageVariancePercent { get; }

        /// <summary>
        /// Probability (0–100) of a critical hit on damage skills. 0 disables crits.
        /// Heals never crit even when this is non-zero. Rolled once per target.
        /// </summary>
        public int CritChancePercent { get; }

        /// <summary>
        /// Damage multiplier applied on a critical hit, expressed as percent
        /// (200 = 2× damage, the default). Clamped to a minimum of 100. Applied to
        /// the base damage before variance jitter.
        /// </summary>
        public int CritMultiplierPercent { get; }

        /// <summary>
        /// Per-skill ammunition (Pokemon-style PP / "Power Points"). 0 means unlimited — the
        /// skill never runs out (default, preserves the engine's pre-PP behavior). A positive
        /// value caps how many times the skill can be used between PP restores; once depleted,
        /// further attempts fail with a "no PP" error.
        /// </summary>
        public int MaxPp { get; }

        public BattleSkillDefinition Clone()
        {
            Dictionary<string, int> costs = new(ResourceCosts.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in ResourceCosts)
            {
                costs[pair.Key] = pair.Value;
            }

            return new BattleSkillDefinition(
                Id,
                EffectType,
                Power,
                CooldownTurns,
                costs,
                DisplayName,
                Description,
                AppliesStatuses,
                DamageTypeId,
                TargetMode,
                AccuracyPercent,
                DamageVariancePercent,
                CritChancePercent,
                CritMultiplierPercent,
                MaxPp);
        }
    }
}
