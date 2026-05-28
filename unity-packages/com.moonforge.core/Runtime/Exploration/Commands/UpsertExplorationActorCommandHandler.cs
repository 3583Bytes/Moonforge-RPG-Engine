using Moonforge.Core.Exploration.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Exploration.Commands
{

    public sealed class UpsertExplorationActorCommandHandler : ICommandHandler<UpsertExplorationActorCommand>
    {
        public DomainResult Handle(GameState gameState, UpsertExplorationActorCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor ID is required."));
            }

            ExplorationMapState map = gameState.ExplorationState.Map;
            if (!map.IsConfigured)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Exploration map is not configured."));
            }

            GridPosition position = new(command.X, command.Y);
            if (!map.IsInBounds(position))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor position is out of bounds."));
            }

            if (!map.IsWalkable(position))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Actor cannot be placed on a non-walkable tile."));
            }

            if (gameState.ExplorationState.IsBlockingActorAt(position, command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Target tile is blocked by another actor."));
            }

            gameState.ExplorationState.UpsertActor(command.ActorId, position, command.BlocksMovement);
            context.EventSink.Publish(new ExplorationActorPositionedEvent(command.ActorId, command.X, command.Y, command.BlocksMovement));
            return DomainResult.Success();
        }
    }
}
