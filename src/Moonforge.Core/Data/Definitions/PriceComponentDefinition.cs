namespace Moonforge.Core.Data.Definitions;

public sealed class PriceComponentDefinition
{
    public PriceComponentDefinition(string currencyId, long amount)
    {
        CurrencyId = currencyId;
        Amount = amount;
    }

    public string CurrencyId { get; }

    public long Amount { get; }
}
