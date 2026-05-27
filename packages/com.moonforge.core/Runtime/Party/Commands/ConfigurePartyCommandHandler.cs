using System;
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

            try
            {
                gameState.PartyState.SetCaps(command.MaxActive, command.MaxRoster);
            }
            catch (Exception ex)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, ex.Message));
            }

            context.EventSink.Publish(new PartyConfiguredEvent(command.MaxActive, command.MaxRoster));
            return DomainResult.Success();
        }
    }
}
