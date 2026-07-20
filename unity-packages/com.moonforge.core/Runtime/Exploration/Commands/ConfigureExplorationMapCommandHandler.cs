using Moonforge.Core.Exploration.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Exploration.Commands
{

    public sealed class ConfigureExplorationMapCommandHandler : ICommandHandler<ConfigureExplorationMapCommand>
    {
        public DomainResult Handle(GameState gameState, ConfigureExplorationMapCommand command, CommandContext context)
        {
            if (!gameState.ExplorationState.TryConfigureMap(
                    command.MapId,
                    command.Width,
                    command.Height,
                    command.Tiles,
                    out string? error))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.ValidationFailed,
                    error ?? "Unable to configure exploration map."));
            }

            gameState.ExplorationState.ClearActors();
            context.EventSink.Publish(new ExplorationMapConfiguredEvent(command.MapId, command.Width, command.Height));
            return DomainResult.Success();
        }
    }
}
