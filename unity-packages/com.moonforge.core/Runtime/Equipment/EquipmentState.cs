using System;
using System.Collections.Generic;

namespace Moonforge.Core.Equipment
{

    public sealed class EquipmentState
    {
        private readonly Dictionary<string, string> _equippedItems = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string> EquippedItems => _equippedItems;

        public bool IsSlotOccupied(string slotId)
        {
            return _equippedItems.ContainsKey(slotId);
        }

        public string? GetEquippedItem(string slotId)
        {
            return _equippedItems.TryGetValue(slotId, out string? itemId) ? itemId : null;
        }

        public void SetEquipped(string slotId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                throw new ArgumentException("Slot ID is required.", nameof(slotId));
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item ID is required.", nameof(itemId));
            }

            _equippedItems[slotId] = itemId;
        }

        public bool Unequip(string slotId)
        {
            return _equippedItems.Remove(slotId);
        }

        public void CopyFrom(EquipmentState source)
        {
            _equippedItems.Clear();
            foreach (KeyValuePair<string, string> pair in source._equippedItems)
            {
                _equippedItems[pair.Key] = pair.Value;
            }
        }
    }
}
