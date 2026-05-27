using System.Collections.Generic;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Equipment.Events;
using Moonforge.Core.Inventory.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Equipment.Commands
{

    public sealed class EquipItemCommandHandler : ICommandHandler<EquipItemCommand>
    {
        public DomainResult Handle(GameState gameState, EquipItemCommand command, CommandContext context)
        {
            if (!context.Definitions.TryGetEquipment(command.ItemId, out EquipmentDefinition equipmentDefinition))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.NotFound,
                    $"Unknown equipment definition '{command.ItemId}'."));
            }

            if (!context.Definitions.TryGetEquipmentSlot(equipmentDefinition.SlotId, out _))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.NotFound,
                    $"Unknown equipment slot '{equipmentDefinition.SlotId}'."));
            }

            if (!gameState.InventoryBag.TryConsume(command.ItemId, 1, out string? consumeError))
            {
                return DomainResult.Fail(new DomainError(
                    DomainErrorCode.InsufficientResources,
                    consumeError ?? $"Item '{command.ItemId}' is not in the inventory."));
            }

            int afterConsume = gameState.InventoryBag.GetTotalQuantity(command.ItemId);

            string? previouslyEquipped = gameState.EquipmentState.GetEquippedItem(equipmentDefinition.SlotId);
            if (previouslyEquipped is not null)
            {
                if (!context.Definitions.TryGetItem(previouslyEquipped, out ItemDefinition previousItemDefinition))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.NotFound,
                        $"Previously equipped item '{previouslyEquipped}' has no item definition."));
                }

                if (!gameState.InventoryBag.TryAdd(previouslyEquipped, 1, previousItemDefinition.StackLimit, out string? returnError))
                {
                    return DomainResult.Fail(new DomainError(
                        DomainErrorCode.ValidationFailed,
                        returnError ?? $"Unable to return previously equipped item '{previouslyEquipped}' to inventory."));
                }

                RemoveStatModifiers(gameState, command.ActorId, equipmentDefinition.SlotId, previouslyEquipped);

                int previousNewQuantity = gameState.InventoryBag.GetTotalQuantity(previouslyEquipped);
                context.EventSink.Publish(new InventoryItemChangedEvent(previouslyEquipped, 1, previousNewQuantity));
            }

            gameState.EquipmentState.SetEquipped(equipmentDefinition.SlotId, command.ItemId);
            ApplyStatModifiers(gameState, command.ActorId, equipmentDefinition);

            context.EventSink.Publish(new InventoryItemChangedEvent(command.ItemId, -1, afterConsume));
            context.EventSink.Publish(new ItemEquippedEvent(equipmentDefinition.SlotId, command.ItemId, previouslyEquipped));
            return DomainResult.Success();
        }

        private static void ApplyStatModifiers(GameState gameState, string actorId, EquipmentDefinition equipmentDefinition)
        {
            if (equipmentDefinition.StatBonuses.Count == 0)
            {
                return;
            }

            StatBlock block = gameState.ActorStatsState.GetOrCreate(actorId);
            string sourceId = EquipmentStatSource.Id(equipmentDefinition.SlotId, equipmentDefinition.ItemId);
            foreach (KeyValuePair<string, int> bonus in equipmentDefinition.StatBonuses)
            {
                block.AddModifier(new StatModifier(
                    bonus.Key,
                    StatModifierBucket.Flat,
                    bonus.Value,
                    EquipmentStatSource.Kind,
                    sourceId));
            }
        }

        private static void RemoveStatModifiers(GameState gameState, string actorId, string slotId, string itemId)
        {
            if (!gameState.ActorStatsState.TryGet(actorId, out StatBlock block))
            {
                return;
            }

            block.RemoveModifiersBySource(EquipmentStatSource.Kind, EquipmentStatSource.Id(slotId, itemId));
        }
    }
}
