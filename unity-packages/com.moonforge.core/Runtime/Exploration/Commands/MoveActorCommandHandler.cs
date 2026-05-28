using System;
using Moonforge.Core.Exploration.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Exploration.Commands
{

    public sealed class MoveActorCommandHandler : ICommandHandler<MoveActorCommand>
    {
        public DomainResult Handle(GameState gameState, MoveActorCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor ID is required."));
            }

            if (Math.Abs(command.DeltaX) + Math.Abs(command.DeltaY) != 1)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    "Movement must be one cardinal step (4-directional)."));
            }

            if (!gameState.ExplorationState.TryGetActor(command.ActorId, out ExplorationActorState actor))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Actor '{command.ActorId}' was not found."));
            }

            ExplorationMapState map = gameState.ExplorationState.Map;
            if (!map.IsConfigured)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Exploration map is not configured."));
            }

            GridPosition from = actor.Position;
            GridPosition to = new(from.X + command.DeltaX, from.Y + command.DeltaY);

            if (!map.IsInBounds(to))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Target tile is out of bounds."));
            }

            if (!map.IsWalkable(to))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Target tile is not walkable."));
            }

            if (gameState.ExplorationState.IsBlockingActorAt(to, command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.Conflict, "Target tile is blocked by another actor."));
            }

            gameState.ExplorationState.SetActorPosition(command.ActorId, to);
            context.EventSink.Publish(new ExplorationActorMovedEvent(command.ActorId, from.X, from.Y, to.X, to.Y));
            return DomainResult.Success();
        }
    }
}
