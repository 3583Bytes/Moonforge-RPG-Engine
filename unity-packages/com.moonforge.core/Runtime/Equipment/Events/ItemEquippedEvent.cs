using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Equipment.Events
{

    public sealed class ItemEquippedEvent : DomainEvent
    {
        public ItemEquippedEvent(string slotId, string itemId, string? replacedItemId)
            : base(nameof(ItemEquippedEvent))
        {
            SlotId = slotId;
            ItemId = itemId;
            ReplacedItemId = replacedItemId;
        }

        public string SlotId { get; }

        public string ItemId { get; }

        public string? ReplacedItemId { get; }
    }
}
