namespace Moonforge.Core.Loot
{

    /// <summary>
    /// Discriminator for the three shapes a <c>LootEntryDefinition</c> can take.
    /// </summary>
    public enum LootEntryKind
    {
        Item = 0,
        Currency = 1,
        NestedTable = 2
    }
}
