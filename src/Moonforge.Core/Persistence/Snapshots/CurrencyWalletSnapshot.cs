using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots;

public sealed class CurrencyWalletSnapshot
{
    public List<CurrencyBalanceSnapshot> Balances { get; set; } = new();

    public List<CurrencyBalanceSnapshot> Maxes { get; set; } = new();
}

public sealed class CurrencyBalanceSnapshot
{
    public string CurrencyId { get; set; } = string.Empty;

    public long Amount { get; set; }
}
