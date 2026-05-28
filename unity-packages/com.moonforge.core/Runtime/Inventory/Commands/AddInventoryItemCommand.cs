using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Inventory.Commands
{

    public sealed class AddInventoryItemCommand : ICommand
    {
        public AddInventoryItemCommand(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public string ItemId { get; }

        public int Quantity { get; }
    }
}
