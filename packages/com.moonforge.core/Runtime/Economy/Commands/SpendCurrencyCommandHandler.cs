using Moonforge.Core.Economy.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Economy.Commands;

public sealed class SpendCurrencyCommandHandler : ICommandHandler<SpendCurrencyCommand>
{
    public DomainResult Handle(GameState gameState, SpendCurrencyCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.CurrencyId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Currency ID is required."));
        }

        if (command.Amount <= 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Spend amount must be positive."));
        }

        if (!context.Definitions.TryGetCurrency(command.CurrencyId, out Data.Definitions.CurrencyDefinition currencyDefinition))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown currency definition '{command.CurrencyId}'."));
        }

        gameState.CurrencyWallet.ConfigureMax(command.CurrencyId, currencyDefinition.MaxBalance);
        CurrencySpendResult result = gameState.CurrencyWallet.Spend(command.CurrencyId, command.Amount);
        if (!result.Success)
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.InsufficientResources,
                $"Insufficient currency '{command.CurrencyId}'. Requested={command.Amount}, available={result.PreviousBalance}."));
        }

        context.EventSink.Publish(new CurrencyChangedEvent(command.CurrencyId, result.PreviousBalance, result.NewBalance));
        return DomainResult.Success();
    }
}
