using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Economy.Events;

public sealed class CurrencyOverflowClampedEvent : DomainEvent
{
    public CurrencyOverflowClampedEvent(string currencyId, long attemptedAdd, long clampedTo)
        : base(nameof(CurrencyOverflowClampedEvent))
    {
        CurrencyId = currencyId;
        AttemptedAdd = attemptedAdd;
        ClampedTo = clampedTo;
    }

    public string CurrencyId { get; }

    public long AttemptedAdd { get; }

    public long ClampedTo { get; }
}
