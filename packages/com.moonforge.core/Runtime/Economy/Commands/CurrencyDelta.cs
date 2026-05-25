namespace Moonforge.Core.Economy.Commands;

public sealed class CurrencyDelta
{
    public CurrencyDelta(string currencyId, long amount)
    {
        CurrencyId = currencyId;
        Amount = amount;
    }

    public string CurrencyId { get; }

    /// <summary>
    /// Positive to grant, negative to spend.
    /// </summary>
    public long Amount { get; }
}
