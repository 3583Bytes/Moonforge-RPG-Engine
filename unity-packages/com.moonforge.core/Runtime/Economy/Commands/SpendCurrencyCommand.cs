using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Economy.Commands
{

    public sealed class SpendCurrencyCommand : ICommand
    {
        public SpendCurrencyCommand(string currencyId, long amount)
        {
            CurrencyId = currencyId;
            Amount = amount;
        }

        public string CurrencyId { get; }

        public long Amount { get; }
    }
}
