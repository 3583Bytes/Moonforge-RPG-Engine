using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Interactables.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Interactables.Commands
{

    public sealed class PlaceInteractableCommandHandler : ICommandHandler<PlaceInteractableCommand>
    {
        public DomainResult Handle(GameState gameState, PlaceInteractableCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.InstanceId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Instance ID is required."));
            }

            if (!context.Definitions.TryGetInteractable(command.DefinitionId, out InteractableDefinition definition))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.NotFound,
                    $"Unknown interactable definition '{command.DefinitionId}'."));
            }

            if (gameState.InteractablesState.TryGet(command.InstanceId, out _))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.Conflict,
                    $"Interactable instance '{command.InstanceId}' already exists."));
            }

            InteractableInstance instance = new(
                command.InstanceId,
                command.DefinitionId,
                command.Position,
                status: InteractableStatus.Default,
                usesRemaining: definition.MaxUses,
                locked: definition.StartsLocked);
            gameState.InteractablesState.Add(instance);

            context.EventSink.Publish(new InteractablePlacedEvent(command.InstanceId, command.DefinitionId, command.Position));
            return DomainResult.Success();
        }
    }
}
