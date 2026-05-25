namespace Moonforge.Core.Data.Definitions;

/// <summary>
/// Optional metadata for a stat ID. Stats do not need to be registered to be used —
/// registration is required only to declare clamps, a non-zero default base, or a
/// formula that derives the stat's base value from other stats / variables.
/// </summary>
public sealed class StatDefinition
{
    public StatDefinition(
        string id,
        int defaultBase = 0,
        int? min = null,
        int? max = null,
        string? derivedFromFormula = null,
        string? displayName = null)
    {
        Id = id;
        DefaultBase = defaultBase;
        Min = min;
        Max = max;
        DerivedFromFormula = derivedFromFormula;
        DisplayName = displayName;
    }

    public string Id { get; }

    /// <summary>Base value when no entry is stored in <c>StatBlock.Base</c>.</summary>
    public int DefaultBase { get; }

    public int? Min { get; }

    public int? Max { get; }

    /// <summary>
    /// Optional expression evaluated through <c>IFormulaEvaluator</c>. When set, the stat's
    /// base value is computed instead of read from the stat block. Other stats (and any
    /// caller-supplied extra variables such as <c>level</c>) are exposed as variables.
    /// </summary>
    public string? DerivedFromFormula { get; }

    public string? DisplayName { get; }
}
