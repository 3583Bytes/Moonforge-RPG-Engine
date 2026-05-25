using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Crafting.Events;

public sealed class CraftAttemptedEvent : DomainEvent
{
    public CraftAttemptedEvent(
        string recipeId,
        bool success,
        int quantity,
        int crafterSkill,
        double successChance,
        double roll)
        : base(nameof(CraftAttemptedEvent))
    {
        RecipeId = recipeId;
        Success = success;
        Quantity = quantity;
        CrafterSkill = crafterSkill;
        SuccessChance = successChance;
        Roll = roll;
    }

    public string RecipeId { get; }

    public bool Success { get; }

    public int Quantity { get; }

    public int CrafterSkill { get; }

    public double SuccessChance { get; }

    public double Roll { get; }
}
