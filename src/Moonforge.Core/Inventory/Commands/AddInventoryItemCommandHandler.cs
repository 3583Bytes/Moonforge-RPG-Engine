using Moonforge.Core.Inventory.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Inventory.Commands;

public sealed class AddInventoryItemCommandHandler : ICommandHandler<AddInventoryItemCommand>
{
    public DomainResult Handle(GameState gameState, AddInventoryItemCommand command, CommandContext context)
    {
        if (!context.Definitions.TryGetItem(command.ItemId, out Data.Definitions.ItemDefinition itemDefinition))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown item definition '{command.ItemId}'."));
        }

        if (!gameState.InventoryBag.TryAdd(command.ItemId, command.Quantity, itemDefinition.StackLimit, out string? error))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, error ?? "Unable to add item."));
        }

        int newQuantity = gameState.InventoryBag.GetTotalQuantity(command.ItemId);
        context.EventSink.Publish(new InventoryItemChangedEvent(command.ItemId, command.Quantity, newQuantity));
        return DomainResult.Success();
    }
}
