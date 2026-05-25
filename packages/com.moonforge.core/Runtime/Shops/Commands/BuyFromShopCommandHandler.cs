using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Shops.Events;

namespace Moonforge.Core.Shops.Commands;

public sealed class BuyFromShopCommandHandler : ICommandHandler<BuyFromShopCommand>
{
    private readonly EconomyTransactionCommandHandler _transactionHandler = new();

    public DomainResult Handle(GameState gameState, BuyFromShopCommand command, CommandContext context)
    {
        if (command.Quantity <= 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Buy quantity must be positive."));
        }

        if (command.PriceOptionIndex < 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Price option index must be >= 0."));
        }

        if (!ShopCommandHelpers.TryEnsureShopAndItem(
                context,
                command.ShopId,
                command.ItemId,
                out ShopDefinition shopDefinition,
                out ItemDefinition itemDefinition,
                out ShopEntryDefinition shopEntry,
                out string error))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, error));
        }

        ShopCommandHelpers.EnsureRestocked(gameState, shopDefinition, context);

        int currentStock = ShopCommandHelpers.EnsureAndGetCurrentStock(gameState, shopDefinition, shopEntry);
        if (currentStock < command.Quantity)
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.InsufficientResources,
                $"Insufficient shop stock for '{command.ItemId}'. Requested={command.Quantity}, available={currentStock}."));
        }

        if (command.PriceOptionIndex >= itemDefinition.BuyPriceOptions.Count)
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.ValidationFailed,
                $"Invalid price option index {command.PriceOptionIndex} for item '{command.ItemId}'."));
        }

        PriceOptionDefinition selectedPrice = itemDefinition.BuyPriceOptions[command.PriceOptionIndex];
        List<CurrencyDelta> currencyDeltas = ShopCommandHelpers.CreateCostDeltas(selectedPrice.Components, command.Quantity);
        List<InventoryDelta> inventoryDeltas = new()
        {
            new InventoryDelta(command.ItemId, command.Quantity)
        };

        DomainResult transactionResult = _transactionHandler.Handle(
            gameState,
            new EconomyTransactionCommand(currencyDeltas, inventoryDeltas),
            context);
        if (!transactionResult.IsSuccess)
        {
            return transactionResult;
        }

        if (shopEntry.MaxStock.HasValue)
        {
            int newStock = currentStock - command.Quantity;
            gameState.ShopState.SetStock(command.ShopId, command.ItemId, newStock);
        }

        context.EventSink.Publish(new ShopTransactionEvent(
            ShopTransactionType.Buy,
            command.ShopId,
            command.ItemId,
            command.Quantity));
        return DomainResult.Success();
    }
}
