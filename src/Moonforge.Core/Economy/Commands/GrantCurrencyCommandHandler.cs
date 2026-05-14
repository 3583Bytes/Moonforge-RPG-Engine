using System;
using Moonforge.Core.Economy.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Economy.Commands;

public sealed class GrantCurrencyCommandHandler : ICommandHandler<GrantCurrencyCommand>
{
    public DomainResult Handle(GameState gameState, GrantCurrencyCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.CurrencyId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Currency ID is required."));
        }

        if (command.Amount <= 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Grant amount must be positive."));
        }

        if (!context.Definitions.TryGetCurrency(command.CurrencyId, out Data.Definitions.CurrencyDefinition currencyDefinition))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown currency definition '{command.CurrencyId}'."));
        }

        try
        {
            gameState.CurrencyWallet.ConfigureMax(command.CurrencyId, currencyDefinition.MaxBalance);
            CurrencyGrantResult result = gameState.CurrencyWallet.Grant(command.CurrencyId, command.Amount);
            context.EventSink.Publish(new CurrencyChangedEvent(command.CurrencyId, result.PreviousBalance, result.NewBalance));

            if (result.Clamped)
            {
                long max = gameState.CurrencyWallet.GetMax(command.CurrencyId);
                context.EventSink.Publish(new CurrencyOverflowClampedEvent(command.CurrencyId, command.Amount, max));
                context.EventSink.Publish(new WarningEvent(
                    "currency.overflow.clamped",
                    $"Currency '{command.CurrencyId}' grant was clamped to max {max}."));
            }

            return DomainResult.Success();
        }
        catch (Exception ex)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, ex.Message));
        }
    }
}
