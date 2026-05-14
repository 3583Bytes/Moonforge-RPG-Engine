using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

public sealed class ShopDefinition
{
    public ShopDefinition(
        string id,
        IReadOnlyList<ShopEntryDefinition> entries,
        long restockIntervalMinutes = 60)
    {
        Id = id;
        Entries = entries ?? System.Array.Empty<ShopEntryDefinition>();
        RestockIntervalMinutes = restockIntervalMinutes;
    }

    public string Id { get; }

    public IReadOnlyList<ShopEntryDefinition> Entries { get; }

    public long RestockIntervalMinutes { get; }
}
