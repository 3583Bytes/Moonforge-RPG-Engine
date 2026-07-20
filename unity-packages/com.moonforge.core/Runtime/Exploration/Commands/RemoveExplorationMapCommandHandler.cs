using Moonforge.Core.Exploration.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Exploration.Commands
{

    public sealed class RemoveExplorationMapCommandHandler : ICommandHandler<RemoveExplorationMapCommand>
    {
        public DomainResult Handle(GameState gameState, RemoveExplorationMapCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.MapId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Map ID is required."));
            }

            if (!gameState.ExplorationState.TryRemoveMap(command.MapId, out string? error))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    error ?? "Unable to remove exploration map."));
            }

            context.EventSink.Publish(new ExplorationMapRemovedEvent(command.MapId));
            return DomainResult.Success();
        }
    }
}
