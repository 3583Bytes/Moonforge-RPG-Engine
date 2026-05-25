using System;
using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

/// <summary>
/// Defines an XP progression curve as a list of cumulative XP thresholds.
/// <see cref="XpThresholds"/>[i] is the total XP required to reach level (i+2);
/// i.e. element 0 is "XP to reach level 2", element 1 is "XP to reach level 3", etc.
/// Level 1 is the starting level and requires no XP.
/// </summary>
public sealed class ExperienceCurveDefinition
{
    private static readonly IReadOnlyDictionary<string, int> EmptyGains =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public ExperienceCurveDefinition(
        string id,
        IReadOnlyList<long> xpThresholds,
        string? displayName = null,
        IReadOnlyDictionary<string, int>? statGainsPerLevel = null)
    {
        Id = id;
        XpThresholds = xpThresholds ?? Array.Empty<long>();
        DisplayName = displayName;
        StatGainsPerLevel = statGainsPerLevel ?? EmptyGains;
    }

    public string Id { get; }

    public IReadOnlyList<long> XpThresholds { get; }

    public string? DisplayName { get; }

    /// <summary>
    /// Per-level flat stat gains, applied as <see cref="Moonforge.Core.Stats.StatModifier"/>s
    /// (Flat bucket, sourceKind = <c>"progression"</c>) on every level-up by the built-in
    /// reactor wired in <c>DefaultCommandDispatcher</c>. Empty by default — leveling does
    /// nothing to stats unless this is populated.
    /// </summary>
    public IReadOnlyDictionary<string, int> StatGainsPerLevel { get; }

    /// <summary>The maximum level this curve supports (initial level 1 plus the number of thresholds).</summary>
    public int MaxLevel => XpThresholds.Count + 1;

    /// <summary>Returns the level reached for the given cumulative XP amount on this curve.</summary>
    public int ResolveLevelForXp(long xp)
    {
        if (xp < 0)
        {
            xp = 0;
        }

        int level = 1;
        for (int i = 0; i < XpThresholds.Count; i++)
        {
            if (xp >= XpThresholds[i])
            {
                level = i + 2;
            }
            else
            {
                break;
            }
        }

        return level;
    }
}
