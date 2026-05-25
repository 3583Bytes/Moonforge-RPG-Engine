using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Interactables.Queries;

public sealed class GetInteractableAtQueryHandler : IQueryHandler<GetInteractableAtQuery, InteractableInstance?>
{
    public InteractableInstance? Query(GameState gameState, GetInteractableAtQuery query)
    {
        return gameState.InteractablesState.FindAt(query.Position);
    }
}
