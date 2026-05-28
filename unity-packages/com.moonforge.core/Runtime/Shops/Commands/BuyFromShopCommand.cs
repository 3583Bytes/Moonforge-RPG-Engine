using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Shops.Commands
{

    public sealed class BuyFromShopCommand : ICommand
    {
        public BuyFromShopCommand(string shopId, string itemId, int quantity = 1, int priceOptionIndex = 0)
        {
            ShopId = shopId;
            ItemId = itemId;
            Quantity = quantity;
            PriceOptionIndex = priceOptionIndex;
        }

        public string ShopId { get; }

        public string ItemId { get; }

        public int Quantity { get; }

        public int PriceOptionIndex { get; }
    }
}
