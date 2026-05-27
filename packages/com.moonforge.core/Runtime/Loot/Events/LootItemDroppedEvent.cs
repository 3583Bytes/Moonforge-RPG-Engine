using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Loot.Events
{

    public sealed class LootItemDroppedEvent : DomainEvent
    {
        public LootItemDroppedEvent(string tableId, string itemId, int quantity)
            : base(nameof(LootItemDroppedEvent))
        {
            TableId = tableId;
            ItemId = itemId;
            Quantity = quantity;
        }

        public string TableId { get; }

        public string ItemId { get; }

        public int Quantity { get; }
    }
}
