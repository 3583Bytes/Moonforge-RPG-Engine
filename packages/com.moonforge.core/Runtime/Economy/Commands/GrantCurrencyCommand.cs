using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Economy.Commands;

public sealed class GrantCurrencyCommand : ICommand
{
    public GrantCurrencyCommand(string currencyId, long amount)
    {
        CurrencyId = currencyId;
        Amount = amount;
    }

    public string CurrencyId { get; }

    public long Amount { get; }
}
