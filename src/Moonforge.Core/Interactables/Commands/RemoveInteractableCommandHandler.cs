using Moonforge.Core.Interactables.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Interactables.Commands;

public sealed class RemoveInteractableCommandHandler : ICommandHandler<RemoveInteractableCommand>
{
    public DomainResult Handle(GameState gameState, RemoveInteractableCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.InstanceId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Instance ID is required."));
        }

        if (!gameState.InteractablesState.Remove(command.InstanceId))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"No interactable instance '{command.InstanceId}'."));
        }

        context.EventSink.Publish(new InteractableRemovedEvent(command.InstanceId));
        return DomainResult.Success();
    }
}
