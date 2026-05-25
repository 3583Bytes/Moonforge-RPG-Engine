using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Economy.Events;

public sealed class CurrencyChangedEvent : DomainEvent
{
    public CurrencyChangedEvent(string currencyId, long previousBalance, long newBalance)
        : base(nameof(CurrencyChangedEvent))
    {
        CurrencyId = currencyId;
        PreviousBalance = previousBalance;
        NewBalance = newBalance;
    }

    public string CurrencyId { get; }

    public long PreviousBalance { get; }

    public long NewBalance { get; }
}
