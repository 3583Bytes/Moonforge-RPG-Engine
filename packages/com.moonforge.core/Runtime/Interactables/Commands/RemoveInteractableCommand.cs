using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Interactables.Commands
{

    public sealed class RemoveInteractableCommand : ICommand
    {
        public RemoveInteractableCommand(string instanceId)
        {
            InstanceId = instanceId;
        }

        public string InstanceId { get; }
    }
}
