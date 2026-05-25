using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

public sealed class ItemDefinition
{
    public ItemDefinition(
        string id,
        int stackLimit,
        IReadOnlyList<PriceOptionDefinition>? buyPriceOptions = null,
        IReadOnlyList<PriceComponentDefinition>? sellPrice = null,
        string? displayName = null,
        string? description = null)
    {
        Id = id;
        StackLimit = stackLimit;
        BuyPriceOptions = buyPriceOptions ?? System.Array.Empty<PriceOptionDefinition>();
        SellPrice = sellPrice ?? System.Array.Empty<PriceComponentDefinition>();
        DisplayName = displayName;
        Description = description;
    }

    public string Id { get; }

    public int StackLimit { get; }

    public IReadOnlyList<PriceOptionDefinition> BuyPriceOptions { get; }

    public IReadOnlyList<PriceComponentDefinition> SellPrice { get; }

    public string? DisplayName { get; }

    public string? Description { get; }
}
