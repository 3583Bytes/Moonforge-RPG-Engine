using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Exploration.Queries;

public sealed class GetExplorationActorPositionQuery : IQuery<GridPosition?>
{
    public GetExplorationActorPositionQuery(string actorId)
    {
        ActorId = actorId;
    }

    public string ActorId { get; }
}
