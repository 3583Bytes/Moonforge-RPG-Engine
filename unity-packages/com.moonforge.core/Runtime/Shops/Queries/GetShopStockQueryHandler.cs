using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Shops.Queries
{

    public sealed class GetShopStockQueryHandler : IQueryHandler<GetShopStockQuery, int?>
    {
        private readonly IGameDefinitionCatalog _definitions;

        public GetShopStockQueryHandler(IGameDefinitionCatalog definitions)
        {
            _definitions = definitions;
        }

        public int? Query(GameState gameState, GetShopStockQuery query)
        {
            if (!_definitions.TryGetShop(query.ShopId, out ShopDefinition shopDefinition))
            {
                return null;
            }

            foreach (ShopEntryDefinition entry in shopDefinition.Entries)
            {
                if (entry.ItemId != query.ItemId)
                {
                    continue;
                }

                if (!entry.MaxStock.HasValue)
                {
                    return null;
                }

                return gameState.ShopState.GetOrInitializeStock(query.ShopId, query.ItemId, entry.MaxStock.Value);
            }

            return null;
        }
    }
}
