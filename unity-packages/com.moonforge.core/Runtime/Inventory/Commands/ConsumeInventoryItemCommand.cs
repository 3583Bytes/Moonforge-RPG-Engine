using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Inventory.Commands
{

    public sealed class ConsumeInventoryItemCommand : ICommand
    {
        public ConsumeInventoryItemCommand(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public string ItemId { get; }

        public int Quantity { get; }
    }
}
