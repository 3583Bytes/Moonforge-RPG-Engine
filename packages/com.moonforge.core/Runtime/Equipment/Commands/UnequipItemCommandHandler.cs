using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Equipment.Events;
using Moonforge.Core.Inventory.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Equipment.Commands;

public sealed class UnequipItemCommandHandler : ICommandHandler<UnequipItemCommand>
{
    public DomainResult Handle(GameState gameState, UnequipItemCommand command, CommandContext context)
    {
        string? equippedItemId = gameState.EquipmentState.GetEquippedItem(command.SlotId);
        if (equippedItemId is null)
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.ValidationFailed,
                $"Slot '{command.SlotId}' has no equipped item."));
        }

        if (!context.Definitions.TryGetItem(equippedItemId, out ItemDefinition itemDefinition))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Equipped item '{equippedItemId}' has no item definition."));
        }

        if (!gameState.InventoryBag.TryAdd(equippedItemId, 1, itemDefinition.StackLimit, out string? addError))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.ValidationFailed,
                addError ?? $"Unable to return '{equippedItemId}' to inventory."));
        }

        gameState.EquipmentState.Unequip(command.SlotId);
        if (gameState.ActorStatsState.TryGet(command.ActorId, out StatBlock block))
        {
            block.RemoveModifiersBySource(EquipmentStatSource.Kind, EquipmentStatSource.Id(command.SlotId, equippedItemId));
        }

        int newQuantity = gameState.InventoryBag.GetTotalQuantity(equippedItemId);

        context.EventSink.Publish(new InventoryItemChangedEvent(equippedItemId, 1, newQuantity));
        context.EventSink.Publish(new ItemUnequippedEvent(command.SlotId, equippedItemId));
        return DomainResult.Success();
    }
}
