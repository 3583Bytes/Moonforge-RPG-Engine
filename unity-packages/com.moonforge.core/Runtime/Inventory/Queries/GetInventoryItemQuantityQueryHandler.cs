using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Inventory.Queries
{

    public sealed class GetInventoryItemQuantityQueryHandler : IQueryHandler<GetInventoryItemQuantityQuery, int>
    {
        public int Query(GameState gameState, GetInventoryItemQuantityQuery query)
        {
            return gameState.InventoryBag.GetTotalQuantity(query.ItemId);
        }
    }
}
