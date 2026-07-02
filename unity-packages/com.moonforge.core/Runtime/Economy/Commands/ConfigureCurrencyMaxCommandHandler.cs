using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Economy.Commands
{

    public sealed class ConfigureCurrencyMaxCommandHandler : ICommandHandler<ConfigureCurrencyMaxCommand>
    {
        public DomainResult Handle(GameState gameState, ConfigureCurrencyMaxCommand command, CommandContext context)
        {
            if (string.IsNullOrWhiteSpace(command.CurrencyId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Currency ID is required."));
            }

            if (command.MaxValue < 0)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Currency max value must be >= 0."));
            }

            // Inputs are fully validated above; ConfigureMax cannot throw for these arguments.
            gameState.CurrencyWallet.ConfigureMax(command.CurrencyId, command.MaxValue);
            return DomainResult.Success();
        }
    }
}
