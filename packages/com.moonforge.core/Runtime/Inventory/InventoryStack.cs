namespace Moonforge.Core.Inventory;

public sealed class InventoryStack
{
    public InventoryStack(string itemId, int quantity, int stackLimit)
    {
        ItemId = itemId;
        Quantity = quantity;
        StackLimit = stackLimit;
    }

    public string ItemId { get; }

    public int Quantity { get; set; }

    public int StackLimit { get; }
}
