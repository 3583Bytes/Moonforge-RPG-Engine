namespace Moonforge.Core.Data.Definitions
{

    public interface IGameDefinitionCatalog
    {
        bool TryGetItem(string itemId, out ItemDefinition itemDefinition);

        bool TryGetCurrency(string currencyId, out CurrencyDefinition currencyDefinition);

        bool TryGetRecipe(string recipeId, out RecipeDefinition recipeDefinition);

        bool TryGetShop(string shopId, out ShopDefinition shopDefinition);

        bool TryGetQuest(string questId, out QuestDefinition questDefinition);

        bool TryGetDialogue(string dialogueId, out DialogueDefinition dialogueDefinition);

        bool TryGetEquipmentSlot(string slotId, out EquipmentSlotDefinition slotDefinition);

        bool TryGetEquipment(string itemId, out EquipmentDefinition equipmentDefinition);

        bool TryGetStatusEffect(string statusId, out StatusEffectDefinition statusDefinition);

        bool TryGetExperienceCurve(string curveId, out ExperienceCurveDefinition curveDefinition);

        bool TryGetStat(string statId, out StatDefinition statDefinition);

        bool TryGetDamageType(string damageTypeId, out DamageTypeDefinition damageTypeDefinition);

        bool TryGetTypeEffectivenessChart(string chartId, out TypeEffectivenessChartDefinition chartDefinition);

        bool TryGetLootTable(string lootTableId, out LootTableDefinition lootTableDefinition);

        bool TryGetEncounterTable(string encounterTableId, out EncounterTableDefinition encounterTableDefinition);

        bool TryGetInteractable(string interactableId, out InteractableDefinition interactableDefinition);

        bool TryGetEvolution(string evolutionId, out EvolutionDefinition evolutionDefinition);
    }
}
