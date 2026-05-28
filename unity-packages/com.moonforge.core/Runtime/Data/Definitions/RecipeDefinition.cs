using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions
{

    public sealed class RecipeDefinition
    {
        public RecipeDefinition(
            string id,
            int difficulty,
            double successChanceAtEqualSkill,
            double skillDeltaPerPoint,
            double minSuccessChance,
            double maxSuccessChance,
            CraftFailConsumePolicy failConsumePolicy,
            IReadOnlyList<CraftIngredientDefinition>? ingredients = null,
            IReadOnlyList<CraftCurrencyCostDefinition>? currencyCosts = null,
            IReadOnlyList<CraftOutputDefinition>? outputs = null,
            string? displayName = null,
            string? description = null)
        {
            Id = id;
            Difficulty = difficulty;
            SuccessChanceAtEqualSkill = successChanceAtEqualSkill;
            SkillDeltaPerPoint = skillDeltaPerPoint;
            MinSuccessChance = minSuccessChance;
            MaxSuccessChance = maxSuccessChance;
            FailConsumePolicy = failConsumePolicy;
            Ingredients = ingredients ?? System.Array.Empty<CraftIngredientDefinition>();
            CurrencyCosts = currencyCosts ?? System.Array.Empty<CraftCurrencyCostDefinition>();
            Outputs = outputs ?? System.Array.Empty<CraftOutputDefinition>();
            DisplayName = displayName;
            Description = description;
        }

        public string Id { get; }

        public int Difficulty { get; }

        public double SuccessChanceAtEqualSkill { get; }

        public double SkillDeltaPerPoint { get; }

        public double MinSuccessChance { get; }

        public double MaxSuccessChance { get; }

        public CraftFailConsumePolicy FailConsumePolicy { get; }

        public IReadOnlyList<CraftIngredientDefinition> Ingredients { get; }

        public IReadOnlyList<CraftCurrencyCostDefinition> CurrencyCosts { get; }

        public IReadOnlyList<CraftOutputDefinition> Outputs { get; }

        public string? DisplayName { get; }

        public string? Description { get; }
    }
}
