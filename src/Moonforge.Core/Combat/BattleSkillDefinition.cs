using System;
using System.Collections.Generic;

namespace Moonforge.Core.Combat;

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
        string? damageTypeId = null)
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

    public BattleSkillDefinition Clone()
    {
        Dictionary<string, int> costs = new(ResourceCosts.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> pair in ResourceCosts)
        {
            costs[pair.Key] = pair.Value;
        }

        return new BattleSkillDefinition(Id, EffectType, Power, CooldownTurns, costs, DisplayName, Description, AppliesStatuses, DamageTypeId);
    }
}
