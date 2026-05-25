using Moonforge.Core.Economy.Events;
using Moonforge.Core.Inventory.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Economy.Commands;

public sealed class EconomyTransactionCommandHandler : ICommandHandler<EconomyTransactionCommand>
{
    public DomainResult Handle(GameState gameState, EconomyTransactionCommand command, CommandContext context)
    {
        foreach (CurrencyDelta currencyDelta in command.CurrencyDeltas)
        {
            if (string.IsNullOrWhiteSpace(currencyDelta.CurrencyId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Currency ID is required."));
            }

            if (!context.Definitions.TryGetCurrency(currencyDelta.CurrencyId, out Data.Definitions.CurrencyDefinition currencyDefinition))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown currency definition '{currencyDelta.CurrencyId}'."));
            }

            gameState.CurrencyWallet.ConfigureMax(currencyDelta.CurrencyId, currencyDefinition.MaxBalance);

            if (currencyDelta.Amount == 0)
            {
                continue;
            }

            if (currencyDelta.Amount > 0)
            {
                CurrencyGrantResult grant = gameState.CurrencyWallet.Grant(currencyDelta.CurrencyId, currencyDelta.Amount);
                context.EventSink.Publish(new CurrencyChangedEvent(currencyDelta.CurrencyId, grant.PreviousBalance, grant.NewBalance));
                if (grant.Clamped)
                {
                    long max = gameState.CurrencyWallet.GetMax(currencyDelta.CurrencyId);
                    context.EventSink.Publish(new CurrencyOverflowClampedEvent(currencyDelta.CurrencyId, currencyDelta.Amount, max));
                    context.EventSink.Publish(new WarningEvent(
                        "currency.overflow.clamped",
                        $"Currency '{currencyDelta.CurrencyId}' grant was clamped to max {max}."));
                }

                continue;
            }

            if (currencyDelta.Amount == long.MinValue)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Currency delta is out of range."));
            }

            long spendAmount = -currencyDelta.Amount;
            CurrencySpendResult spend = gameState.CurrencyWallet.Spend(currencyDelta.CurrencyId, spendAmount);
            if (!spend.Success)
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.InsufficientResources,
                    $"Insufficient currency '{currencyDelta.CurrencyId}'. Requested={spendAmount}, available={spend.PreviousBalance}."));
            }

            context.EventSink.Publish(new CurrencyChangedEvent(currencyDelta.CurrencyId, spend.PreviousBalance, spend.NewBalance));
        }

        foreach (InventoryDelta inventoryDelta in command.InventoryDeltas)
        {
            if (string.IsNullOrWhiteSpace(inventoryDelta.ItemId))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Item ID is required."));
            }

            if (!context.Definitions.TryGetItem(inventoryDelta.ItemId, out Data.Definitions.ItemDefinition itemDefinition))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown item definition '{inventoryDelta.ItemId}'."));
            }

            if (inventoryDelta.Amount == 0)
            {
                continue;
            }

            if (inventoryDelta.Amount > 0)
            {
                if (!gameState.InventoryBag.TryAdd(
                        inventoryDelta.ItemId,
                        inventoryDelta.Amount,
                        itemDefinition.StackLimit,
                        out string? addError))
                {
                    return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, addError ?? "Unable to add item."));
                }

                int newQuantity = gameState.InventoryBag.GetTotalQuantity(inventoryDelta.ItemId);
                context.EventSink.Publish(new InventoryItemChangedEvent(inventoryDelta.ItemId, inventoryDelta.Amount, newQuantity));
                continue;
            }

            int consumeAmount = -inventoryDelta.Amount;
            if (!gameState.InventoryBag.TryConsume(inventoryDelta.ItemId, consumeAmount, out string? consumeError))
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.InsufficientResources, consumeError ?? "Unable to consume item."));
            }

            int remaining = gameState.InventoryBag.GetTotalQuantity(inventoryDelta.ItemId);
            context.EventSink.Publish(new InventoryItemChangedEvent(inventoryDelta.ItemId, inventoryDelta.Amount, remaining));
        }

        return DomainResult.Success();
    }
}
