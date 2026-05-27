using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Exploration.Queries
{

    public sealed class CanMoveActorQuery : IQuery<bool>
    {
        public CanMoveActorQuery(string actorId, int targetX, int targetY)
        {
            ActorId = actorId;
            TargetX = targetX;
            TargetY = targetY;
        }

        public string ActorId { get; }

        public int TargetX { get; }

        public int TargetY { get; }
    }
}
