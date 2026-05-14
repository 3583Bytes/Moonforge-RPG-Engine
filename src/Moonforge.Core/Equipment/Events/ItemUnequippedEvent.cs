using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Equipment.Events;

public sealed class ItemUnequippedEvent : DomainEvent
{
    public ItemUnequippedEvent(string slotId, string itemId)
        : base(nameof(ItemUnequippedEvent))
    {
        SlotId = slotId;
        ItemId = itemId;
    }

    public string SlotId { get; }

    public string ItemId { get; }
}
