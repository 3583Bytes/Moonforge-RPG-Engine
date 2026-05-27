using Moonforge.Core.Exploration;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Interactables.Queries
{

    public sealed class GetInteractableAtQuery : IQuery<InteractableInstance?>
    {
        public GetInteractableAtQuery(GridPosition position)
        {
            Position = position;
        }

        public GridPosition Position { get; }
    }
}
