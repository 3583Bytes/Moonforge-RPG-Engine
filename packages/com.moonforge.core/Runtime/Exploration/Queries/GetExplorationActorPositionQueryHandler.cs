using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Exploration.Queries;

public sealed class GetExplorationActorPositionQueryHandler : IQueryHandler<GetExplorationActorPositionQuery, GridPosition?>
{
    public GridPosition? Query(GameState gameState, GetExplorationActorPositionQuery query)
    {
        return gameState.ExplorationState.TryGetActor(query.ActorId, out ExplorationActorState actor)
            ? actor.Position
            : null;
    }
}
