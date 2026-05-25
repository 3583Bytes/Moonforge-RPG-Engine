using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots;

public sealed class ShopStateSnapshot
{
    public List<ShopStockSnapshot> Stocks { get; set; } = new();

    public List<ShopRestockSnapshot> Restocks { get; set; } = new();
}

public sealed class ShopStockSnapshot
{
    public string Key { get; set; } = string.Empty;

    public int Stock { get; set; }
}

public sealed class ShopRestockSnapshot
{
    public string ShopId { get; set; } = string.Empty;

    public long LastRestockMinute { get; set; }
}
