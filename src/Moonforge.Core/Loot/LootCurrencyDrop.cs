namespace Moonforge.Core.Loot;

public sealed class LootCurrencyDrop
{
    public LootCurrencyDrop(string currencyId, long amount)
    {
        CurrencyId = currencyId;
        Amount = amount;
    }

    public string CurrencyId { get; }

    public long Amount { get; }
}
