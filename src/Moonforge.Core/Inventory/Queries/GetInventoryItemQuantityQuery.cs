using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Inventory.Queries;

public sealed class GetInventoryItemQuantityQuery : IQuery<int>
{
    public GetInventoryItemQuantityQuery(string itemId)
    {
        ItemId = itemId;
    }

    public string ItemId { get; }
}
