using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Loot.Events
{

    public sealed class LootRolledEvent : DomainEvent
    {
        public LootRolledEvent(string tableId, int itemDropCount, int currencyDropCount)
            : base(nameof(LootRolledEvent))
        {
            TableId = tableId;
            ItemDropCount = itemDropCount;
            CurrencyDropCount = currencyDropCount;
        }

        public string TableId { get; }

        public int ItemDropCount { get; }

        public int CurrencyDropCount { get; }
    }
}
