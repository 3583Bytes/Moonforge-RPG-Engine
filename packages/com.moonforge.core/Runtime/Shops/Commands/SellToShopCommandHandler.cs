using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Shops.Events;

namespace Moonforge.Core.Shops.Commands;

public sealed class SellToShopCommandHandler : ICommandHandler<SellToShopCommand>
{
    private readonly EconomyTransactionCommandHandler _transactionHandler = new();

    public DomainResult Handle(GameState gameState, SellToShopCommand command, CommandContext context)
    {
        if (command.Quantity <= 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Sell quantity must be positive."));
        }

        if (!ShopCommandHelpers.TryEnsureShopAndItem(
                context,
                command.ShopId,
                command.ItemId,
                out ShopDefinition shopDefinition,
                out ItemDefinition itemDefinition,
                out _,
                out string error))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, error));
        }

        ShopCommandHelpers.EnsureRestocked(gameState, shopDefinition, context);

        if (itemDefinition.SellPrice.Count == 0)
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.ValidationFailed,
                $"Item '{command.ItemId}' has no configured sell price."));
        }

        List<CurrencyDelta> currencyDeltas = ShopCommandHelpers.CreateSellDeltas(itemDefinition.SellPrice, command.Quantity);
        List<InventoryDelta> inventoryDeltas = new()
        {
            new InventoryDelta(command.ItemId, -command.Quantity)
        };

        DomainResult transactionResult = _transactionHandler.Handle(
            gameState,
            new EconomyTransactionCommand(currencyDeltas, inventoryDeltas),
            context);
        if (!transactionResult.IsSuccess)
        {
            return transactionResult;
        }

        context.EventSink.Publish(new ShopTransactionEvent(
            ShopTransactionType.Sell,
            command.ShopId,
            command.ItemId,
            command.Quantity));
        return DomainResult.Success();
    }
}
