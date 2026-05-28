namespace Moonforge.Core.Data.Definitions
{

    public sealed class CraftIngredientDefinition
    {
        public CraftIngredientDefinition(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }

        public string ItemId { get; }

        public int Quantity { get; }
    }
}
