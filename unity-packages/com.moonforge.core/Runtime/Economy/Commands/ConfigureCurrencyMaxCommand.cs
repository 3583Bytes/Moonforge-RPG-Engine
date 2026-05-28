using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Economy.Commands
{

    public sealed class ConfigureCurrencyMaxCommand : ICommand
    {
        public ConfigureCurrencyMaxCommand(string currencyId, long maxValue)
        {
            CurrencyId = currencyId;
            MaxValue = maxValue;
        }

        public string CurrencyId { get; }

        public long MaxValue { get; }
    }
}
