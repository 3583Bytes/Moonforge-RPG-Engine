using System.Collections.Generic;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Interactables.Queries
{

    public sealed class ListInteractablesQueryHandler : IQueryHandler<ListInteractablesQuery, IReadOnlyCollection<InteractableInstance>>
    {
        public IReadOnlyCollection<InteractableInstance> Query(GameState gameState, ListInteractablesQuery query)
        {
            return (IReadOnlyCollection<InteractableInstance>)gameState.InteractablesState.Instances.Values;
        }
    }
}
