using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Evolution.Commands
{

    public sealed class ConfigureActorEvolutionsCommandHandler : ICommandHandler<ConfigureActorEvolutionsCommand>
    {
        public DomainResult Handle(GameState gameState, ConfigureActorEvolutionsCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.ActorId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Actor id is required."));
            }

            gameState.EvolutionState.Set(command.ActorId, command.EvolutionIds);
            return DomainResult.Success();
        }
    }
}
