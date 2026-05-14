namespace Moonforge.Core.Data.Definitions;

public sealed class ShopEntryDefinition
{
    public ShopEntryDefinition(string itemId, int? maxStock = null)
    {
        ItemId = itemId;
        MaxStock = maxStock;
    }

    public string ItemId { get; }

    /// <summary>
    /// Null means unlimited stock.
    /// </summary>
    public int? MaxStock { get; }
}
