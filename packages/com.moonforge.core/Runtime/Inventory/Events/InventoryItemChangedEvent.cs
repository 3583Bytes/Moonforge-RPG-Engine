using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Inventory.Events;

public sealed class InventoryItemChangedEvent : DomainEvent
{
    public InventoryItemChangedEvent(string itemId, int delta, int newQuantity)
        : base(nameof(InventoryItemChangedEvent))
    {
        ItemId = itemId;
        Delta = delta;
        NewQuantity = newQuantity;
    }

    public string ItemId { get; }

    public int Delta { get; }

    public int NewQuantity { get; }
}
