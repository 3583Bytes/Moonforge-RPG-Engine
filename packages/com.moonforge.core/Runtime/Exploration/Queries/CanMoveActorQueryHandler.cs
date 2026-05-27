using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Exploration.Queries
{

    public sealed class CanMoveActorQueryHandler : IQueryHandler<CanMoveActorQuery, bool>
    {
        public bool Query(GameState gameState, CanMoveActorQuery query)
        {
            ExplorationMapState map = gameState.ExplorationState.Map;
            if (!map.IsConfigured)
            {
                return false;
            }

            if (!gameState.ExplorationState.TryGetActor(query.ActorId, out ExplorationActorState actor))
            {
                return false;
            }

            GridPosition target = new(query.TargetX, query.TargetY);
            if (!map.IsInBounds(target))
            {
                return false;
            }

            if (!map.IsWalkable(target))
            {
                return false;
            }

            return !gameState.ExplorationState.IsBlockingActorAt(target, actor.ActorId);
        }
    }
}
