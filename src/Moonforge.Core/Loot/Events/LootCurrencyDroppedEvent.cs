using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Loot.Events;

public sealed class LootCurrencyDroppedEvent : DomainEvent
{
    public LootCurrencyDroppedEvent(string tableId, string currencyId, long amount)
        : base(nameof(LootCurrencyDroppedEvent))
    {
        TableId = tableId;
        CurrencyId = currencyId;
        Amount = amount;
    }

    public string TableId { get; }

    public string CurrencyId { get; }

    public long Amount { get; }
}
