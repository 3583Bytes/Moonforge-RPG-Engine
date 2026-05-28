using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Interactables.Commands
{

    public sealed class InteractWithCommand : ICommand
    {
        public InteractWithCommand(string actorId, string instanceId)
        {
            ActorId = actorId;
            InstanceId = instanceId;
        }

        public string ActorId { get; }

        public string InstanceId { get; }
    }
}
