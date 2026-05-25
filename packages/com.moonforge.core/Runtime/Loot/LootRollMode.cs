namespace Moonforge.Core.Loot;

/// <summary>
/// Determines how a <c>LootTableDefinition</c>'s entries are resolved.
/// </summary>
public enum LootRollMode
{
    /// <summary>
    /// Pick exactly one entry using weighted random selection. Each entry's
    /// <c>Weight</c> contributes proportionally; <c>ChancePercent</c> is ignored.
    /// </summary>
    PickOne = 0,

    /// <summary>
    /// Roll every entry independently. Each entry's <c>ChancePercent</c> (0-100) determines
    /// whether it drops; <c>Weight</c> is ignored.
    /// </summary>
    RollEach = 1
}
