using Moonforge.Core.Exploration.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Exploration.Commands
{

    public sealed class SwitchExplorationMapCommandHandler : ICommandHandler<SwitchExplorationMapCommand>
    {
        public DomainResult Handle(GameState gameState, SwitchExplorationMapCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.MapId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Map ID is required."));
            }

            ExplorationState exploration = gameState.ExplorationState;
            string fromMapId = exploration.ActiveMapId;

            if (!exploration.TrySwitchMap(command.MapId, out string? error))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, error ?? "Unable to switch exploration map."));
            }

            // Place carried actors on the now-active target map, removing them from the
            // map they came from. Any failure fails the whole dispatch — the dispatcher
            // rolls the switch and every prior carry back.
            ExplorationMapState map = exploration.Map;
            foreach (ExplorationActorCarry carry in command.CarryActors)
            {
                if (string.IsNullOrWhiteSpace(carry.ActorId))
                {
                    return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Carried actor ID is required."));
                }

                GridPosition position = new(carry.X, carry.Y);
                if (!map.IsInBounds(position))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.ValidationFailed,
                        $"Carry position for '{carry.ActorId}' is out of bounds on map '{command.MapId}'."));
                }

                if (!map.IsWalkable(position))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.Conflict,
                        $"Carry position for '{carry.ActorId}' is not walkable on map '{command.MapId}'."));
                }

                if (exploration.IsBlockingActorAt(position, carry.ActorId))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.Conflict,
                        $"Carry position for '{carry.ActorId}' is blocked by another actor."));
                }

                if (fromMapId.Length > 0)
                {
                    exploration.RemoveActorFromMap(fromMapId, carry.ActorId);
                }

                exploration.UpsertActor(carry.ActorId, position, carry.BlocksMovement);
                context.EventSink.Publish(new ExplorationActorPositionedEvent(carry.ActorId, carry.X, carry.Y, carry.BlocksMovement));
            }

            context.EventSink.Publish(new ExplorationMapSwitchedEvent(fromMapId, command.MapId));
            return DomainResult.Success();
        }
    }
}
