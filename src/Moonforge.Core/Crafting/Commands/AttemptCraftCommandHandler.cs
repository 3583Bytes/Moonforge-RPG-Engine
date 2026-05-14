using System;
using System.Collections.Generic;
using Moonforge.Core.Crafting.Events;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Crafting.Commands;

public sealed class AttemptCraftCommandHandler : ICommandHandler<AttemptCraftCommand>
{
    private readonly EconomyTransactionCommandHandler _economyTransactionCommandHandler = new();

    public DomainResult Handle(GameState gameState, AttemptCraftCommand command, CommandContext context)
    {
        if (string.IsNullOrWhiteSpace(command.RecipeId))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Recipe ID is required."));
        }

        if (command.CrafterSkill < 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Crafter skill must be >= 0."));
        }

        if (command.Quantity <= 0)
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Craft quantity must be positive."));
        }

        if (!context.Definitions.TryGetRecipe(command.RecipeId, out RecipeDefinition recipe))
        {
            return DomainResult.Fail(new DomainError(DomainErrorCode.NotFound, $"Unknown recipe definition '{command.RecipeId}'."));
        }

        double successChance = ComputeSuccessChance(recipe, command.CrafterSkill);
        double roll = context.RandomSource.NextDouble();
        bool success = roll <= successChance;

        bool consumeOnFail = recipe.FailConsumePolicy == CraftFailConsumePolicy.ConsumeAll;
        if (success || consumeOnFail)
        {
            List<CurrencyDelta> currencyDeltas = new();
            foreach (CraftCurrencyCostDefinition cost in recipe.CurrencyCosts)
            {
                long scaled = checked(cost.Amount * command.Quantity);
                currencyDeltas.Add(new CurrencyDelta(cost.CurrencyId, -scaled));
            }

            List<InventoryDelta> inventoryDeltas = new();
            foreach (CraftIngredientDefinition ingredient in recipe.Ingredients)
            {
                int scaled = checked(ingredient.Quantity * command.Quantity);
                inventoryDeltas.Add(new InventoryDelta(ingredient.ItemId, -scaled));
            }

            if (success)
            {
                foreach (CraftOutputDefinition output in recipe.Outputs)
                {
                    int scaled = checked(output.Quantity * command.Quantity);
                    inventoryDeltas.Add(new InventoryDelta(output.ItemId, scaled));
                }
            }

            DomainResult transactionResult = _economyTransactionCommandHandler.Handle(
                gameState,
                new EconomyTransactionCommand(currencyDeltas, inventoryDeltas),
                context);
            if (!transactionResult.IsSuccess)
            {
                return transactionResult;
            }
        }

        context.EventSink.Publish(new CraftAttemptedEvent(
            command.RecipeId,
            success,
            command.Quantity,
            command.CrafterSkill,
            successChance,
            roll));

        return DomainResult.Success();
    }

    private static double ComputeSuccessChance(RecipeDefinition recipe, int crafterSkill)
    {
        double chance = recipe.SuccessChanceAtEqualSkill + ((crafterSkill - recipe.Difficulty) * recipe.SkillDeltaPerPoint);
        chance = Math.Max(recipe.MinSuccessChance, chance);
        chance = Math.Min(recipe.MaxSuccessChance, chance);
        return chance;
    }
}
