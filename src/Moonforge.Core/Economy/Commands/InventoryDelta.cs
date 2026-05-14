namespace Moonforge.Core.Economy.Commands;

public sealed class InventoryDelta
{
    public InventoryDelta(string itemId, int amount)
    {
        ItemId = itemId;
        Amount = amount;
    }

    public string ItemId { get; }

    /// <summary>
    /// Positive to add, negative to consume.
    /// </summary>
    public int Amount { get; }
}
