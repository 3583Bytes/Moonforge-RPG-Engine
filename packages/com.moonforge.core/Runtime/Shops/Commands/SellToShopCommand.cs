using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Shops.Commands;

public sealed class SellToShopCommand : ICommand
{
    public SellToShopCommand(string shopId, string itemId, int quantity = 1)
    {
        ShopId = shopId;
        ItemId = itemId;
        Quantity = quantity;
    }

    public string ShopId { get; }

    public string ItemId { get; }

    public int Quantity { get; }
}
