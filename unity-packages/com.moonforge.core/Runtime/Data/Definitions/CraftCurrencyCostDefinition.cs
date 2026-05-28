namespace Moonforge.Core.Data.Definitions
{

    public sealed class CraftCurrencyCostDefinition
    {
        public CraftCurrencyCostDefinition(string currencyId, long amount)
        {
            CurrencyId = currencyId;
            Amount = amount;
        }

        public string CurrencyId { get; }

        public long Amount { get; }
    }
}
