using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Runtime.Queries;
using Moonforge.Core.Runtime.Random;

namespace Moonforge.Core.Encounters.Queries
{

    public sealed class RollEncounterTableQueryHandler : IQueryHandler<RollEncounterTableQuery, EncounterRollResult>
    {
        private readonly IGameDefinitionCatalog _definitions;
        private readonly IRandomSource _randomSource;

        public RollEncounterTableQueryHandler(IGameDefinitionCatalog definitions, IRandomSource randomSource)
        {
            _definitions = definitions;
            _randomSource = randomSource;
        }

        public EncounterRollResult Query(GameState gameState, RollEncounterTableQuery query)
        {
            if (!_definitions.TryGetEncounterTable(query.TableId, out EncounterTableDefinition table))
            {
                return EncounterRollResult.Empty;
            }

            return EncounterResolver.Roll(_randomSource, table);
        }
    }
}
