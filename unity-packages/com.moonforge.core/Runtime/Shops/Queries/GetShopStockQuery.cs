using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Shops.Queries
{

    public sealed class GetShopStockQuery : IQuery<int?>
    {
        public GetShopStockQuery(string shopId, string itemId)
        {
            ShopId = shopId;
            ItemId = itemId;
        }

        public string ShopId { get; }

        public string ItemId { get; }
    }
}
