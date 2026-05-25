namespace Moonforge.Core.Loot;

public sealed class LootDrop
{
    public LootDrop(string itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }

    public string ItemId { get; }

    public int Quantity { get; }
}
