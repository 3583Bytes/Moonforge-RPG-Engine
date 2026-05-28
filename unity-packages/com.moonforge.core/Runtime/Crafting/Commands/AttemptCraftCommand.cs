using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Crafting.Commands
{

    public sealed class AttemptCraftCommand : ICommand
    {
        public AttemptCraftCommand(string recipeId, int crafterSkill, int quantity = 1)
        {
            RecipeId = recipeId;
            CrafterSkill = crafterSkill;
            Quantity = quantity;
        }

        public string RecipeId { get; }

        public int CrafterSkill { get; }

        public int Quantity { get; }
    }
}
