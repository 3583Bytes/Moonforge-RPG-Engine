using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Shops.Events;

public sealed class ShopTransactionEvent : DomainEvent
{
    public ShopTransactionEvent(
        ShopTransactionType transactionType,
        string shopId,
        string itemId,
        int quantity)
        : base(nameof(ShopTransactionEvent))
    {
        TransactionType = transactionType;
        ShopId = shopId;
        ItemId = itemId;
        Quantity = quantity;
    }

    public ShopTransactionType TransactionType { get; }

    public string ShopId { get; }

    public string ItemId { get; }

    public int Quantity { get; }
}
