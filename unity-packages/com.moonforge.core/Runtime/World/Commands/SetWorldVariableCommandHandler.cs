using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.World.Events;

namespace Moonforge.Core.World.Commands
{

    public sealed class SetWorldVariableCommandHandler : ICommandHandler<SetWorldVariableCommand>
    {
        public DomainResult Handle(GameState gameState, SetWorldVariableCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.Key))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "World variable key is required."));
            }

            if (command.Value is null)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "World variable value is required."));
            }

            // Inputs are fully validated above; Set cannot throw for these arguments.
            gameState.WorldState.Set(command.Key, command.Value);
            context.EventSink.Publish(new WorldVariableChangedEvent(command.Key, command.Value));
            return DomainResult.Success();
        }
    }
}
