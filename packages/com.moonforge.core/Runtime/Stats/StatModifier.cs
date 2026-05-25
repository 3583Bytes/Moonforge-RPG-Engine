using System;

namespace Moonforge.Core.Stats;

/// <summary>
/// A single contribution to a stat aggregation. Modifiers are deduplicated/removed by the
/// (<see cref="SourceKind"/>, <see cref="SourceId"/>) pair so that an equipment unequip or
/// status expiration can withdraw exactly the modifiers it added.
/// </summary>
public sealed class StatModifier
{
    public StatModifier(
        string statId,
        StatModifierBucket bucket,
        double value,
        string sourceKind,
        string sourceId,
        int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(statId))
        {
            throw new ArgumentException("Stat ID is required.", nameof(statId));
        }

        if (string.IsNullOrWhiteSpace(sourceKind))
        {
            throw new ArgumentException("Source kind is required.", nameof(sourceKind));
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("Source ID is required.", nameof(sourceId));
        }

        StatId = statId;
        Bucket = bucket;
        Value = value;
        SourceKind = sourceKind;
        SourceId = sourceId;
        Priority = priority;
    }

    public string StatId { get; }

    public StatModifierBucket Bucket { get; }

    public double Value { get; }

    /// <summary>Category tag (e.g. "equipment", "status", "progression", "buff").</summary>
    public string SourceKind { get; }

    /// <summary>Unique identifier within <see cref="SourceKind"/>; used to remove specific modifiers.</summary>
    public string SourceId { get; }

    /// <summary>Used only for <see cref="StatModifierBucket.Override"/> tiebreaking; higher wins.</summary>
    public int Priority { get; }

    public StatModifier Clone()
    {
        return new StatModifier(StatId, Bucket, Value, SourceKind, SourceId, Priority);
    }
}
