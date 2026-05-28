using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.World.Queries
{

    public sealed class GetWorldVariableQueryHandler : IQueryHandler<GetWorldVariableQuery, WorldVariableValue?>
    {
        public WorldVariableValue? Query(GameState gameState, GetWorldVariableQuery query)
        {
            return gameState.WorldState.TryGet(query.Key, out WorldVariableValue value) ? value : null;
        }
    }
}
