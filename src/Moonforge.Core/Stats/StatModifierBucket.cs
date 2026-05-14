namespace Moonforge.Core.Stats;

/// <summary>
/// Stage at which a <see cref="StatModifier"/> contributes during stat aggregation.
/// Pipeline order: Flat → AddPercent → MultPercent → Override.
/// </summary>
public enum StatModifierBucket
{
    /// <summary>Added to the base value. Stacks additively across all sources.</summary>
    Flat = 0,

    /// <summary>Percent (0.20 = +20%) added to a single sum, then multiplied with (base + flats).</summary>
    AddPercent = 1,

    /// <summary>Percent (0.10 = +10%) compounded multiplicatively after the additive percent pass.</summary>
    MultPercent = 2,

    /// <summary>Replaces the computed value entirely. Highest Priority wins; ties broken by SourceId ascending.</summary>
    Override = 3
}
