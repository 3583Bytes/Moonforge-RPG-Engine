using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Queries;
using Moonforge.Core.Runtime.Random;

namespace Moonforge.Core.Loot.Queries
{

    public sealed class RollLootTableQueryHandler : IQueryHandler<RollLootTableQuery, LootRollResult>
    {
        private readonly IGameDefinitionCatalog _definitions;
        private readonly IRandomSource _randomSource;

        public RollLootTableQueryHandler(IGameDefinitionCatalog definitions, IRandomSource randomSource)
        {
            _definitions = definitions;
            _randomSource = randomSource;
        }

        public LootRollResult Query(GameState gameState, RollLootTableQuery query)
        {
            if (!_definitions.TryGetLootTable(query.TableId, out LootTableDefinition table))
            {
                return LootRollResult.Empty;
            }

            return LootResolver.Roll(gameState, _definitions, _randomSource, table);
        }
    }
}
