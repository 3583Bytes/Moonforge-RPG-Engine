using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Combat.Queries
{

    public sealed class GetActiveBattleStatusQueryHandler : IQueryHandler<GetActiveBattleStatusQuery, BattleStatus?>
    {
        public BattleStatus? Query(GameState gameState, GetActiveBattleStatusQuery query)
        {
            return gameState.ActiveBattle?.Status;
        }
    }
}
