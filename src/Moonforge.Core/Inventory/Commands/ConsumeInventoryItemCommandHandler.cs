using Moonforge.Core.Inventory.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Inventory.Commands;

public sealed class ConsumeInventoryItemCommandHandler : ICommandHandler<ConsumeInventoryItemCommand>
{
    public DomainResult Handle(GameState gameState, ConsumeInventoryItemCommand command, CommandContext context)
    {
        if (!gameState.InventoryBag.TryConsume(command.ItemId, command.Quantity, out string? error))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.InsufficientResources, error ?? "Unable to consume item."));
        }

        int newQuantity = gameState.InventoryBag.GetTotalQuantity(command.ItemId);
        context.EventSink.Publish(new InventoryItemChangedEvent(command.ItemId, -command.Quantity, newQuantity));
        return DomainResult.Success();
    }
}
