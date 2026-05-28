using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Exploration.Commands
{

    public sealed class MoveActorCommand : ICommand
    {
        public MoveActorCommand(string actorId, int deltaX, int deltaY)
        {
            ActorId = actorId;
            DeltaX = deltaX;
            DeltaY = deltaY;
        }

        public string ActorId { get; }

        public int DeltaX { get; }

        public int DeltaY { get; }
    }
}
