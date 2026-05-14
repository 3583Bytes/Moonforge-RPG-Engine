using System;

namespace Moonforge.Core.Data.Definitions;

/// <summary>
/// Declarative metadata for one damage type. Tells the combat runtime which stat to read
/// for the attacker's offensive value, which stat (if any) is subtracted as flat defense,
/// and which stat carries the target's percent resistance.
/// </summary>
public sealed class DamageTypeDefinition
{
    public DamageTypeDefinition(
        string id,
        string attackStatId,
        string? flatDefenseStatId = null,
        string? resistanceStatId = null,
        string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Damage type ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(attackStatId))
        {
            throw new ArgumentException("Attack stat ID is required.", nameof(attackStatId));
        }

        Id = id;
        AttackStatId = attackStatId;
        FlatDefenseStatId = flatDefenseStatId;
        ResistanceStatId = string.IsNullOrWhiteSpace(resistanceStatId) ? $"res.{id}" : resistanceStatId;
        DisplayName = displayName;
    }

    public string Id { get; }

    /// <summary>Stat read from the attacker, e.g. <c>"atk"</c> for physical or <c>"matk"</c> for magical.</summary>
    public string AttackStatId { get; }

    /// <summary>Optional stat read from the target and subtracted flat before percent resistance applies.</summary>
    public string? FlatDefenseStatId { get; }

    /// <summary>
    /// Stat read from the target and applied as percent reduction. Values are in whole percent
    /// (50 = 50% reduction). Values ≥ 100 grant immunity; negative values inflict vulnerability.
    /// </summary>
    public string ResistanceStatId { get; }

    public string? DisplayName { get; }
}
