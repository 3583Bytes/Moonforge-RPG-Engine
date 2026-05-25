using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Combat.Queries;

public sealed class GetCurrentBattleTurnActorQueryHandler : IQueryHandler<GetCurrentBattleTurnActorQuery, string?>
{
    public string? Query(GameState gameState, GetCurrentBattleTurnActorQuery query)
    {
        if (gameState.ActiveBattle is null || gameState.ActiveBattle.TurnOrder.Count == 0)
        {
            return null;
        }

        return gameState.ActiveBattle.TurnOrder[gameState.ActiveBattle.TurnIndex];
    }
}
