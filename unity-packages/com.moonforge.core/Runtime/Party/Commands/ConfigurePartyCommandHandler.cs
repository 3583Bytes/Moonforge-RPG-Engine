using Moonforge.Core.Party.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Party.Commands
{

    public sealed class ConfigurePartyCommandHandler : ICommandHandler<ConfigurePartyCommand>
    {
        public DomainResult Handle(GameState gameState, ConfigurePartyCommand command, CommandContext context)
        {
            if (command.MaxActive <= 0)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "MaxActive must be positive."));
            }

            if (command.MaxRoster < command.MaxActive)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "MaxRoster must be >= MaxActive."));
            }

            if (command.MaxRoster < gameState.PartyState.Members.Count)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "MaxRoster cannot be set below current member count."));
            }

            if (command.MaxActive < gameState.PartyState.ActiveCount)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "MaxActive cannot be set below current active count."));
            }

            // Inputs are fully validated above; SetCaps cannot throw for these arguments.
            gameState.PartyState.SetCaps(command.MaxActive, command.MaxRoster);
            context.EventSink.Publish(new PartyConfiguredEvent(command.MaxActive, command.MaxRoster));
            return DomainResult.Success();
        }
    }
}
