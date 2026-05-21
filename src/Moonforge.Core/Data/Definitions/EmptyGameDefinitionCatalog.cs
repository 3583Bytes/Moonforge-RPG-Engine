namespace Moonforge.Core.Data.Definitions;

public sealed class EmptyGameDefinitionCatalog : IGameDefinitionCatalog
{
    public static readonly EmptyGameDefinitionCatalog Instance = new();

    private EmptyGameDefinitionCatalog()
    {
    }

    public bool TryGetItem(string itemId, out ItemDefinition itemDefinition)
    {
        itemDefinition = null!;
        return false;
    }

    public bool TryGetCurrency(string currencyId, out CurrencyDefinition currencyDefinition)
    {
        currencyDefinition = null!;
        return false;
    }

    public bool TryGetRecipe(string recipeId, out RecipeDefinition recipeDefinition)
    {
        recipeDefinition = null!;
        return false;
    }

    public bool TryGetShop(string shopId, out ShopDefinition shopDefinition)
    {
        shopDefinition = null!;
        return false;
    }

    public bool TryGetQuest(string questId, out QuestDefinition questDefinition)
    {
        questDefinition = null!;
        return false;
    }

    public bool TryGetDialogue(string dialogueId, out DialogueDefinition dialogueDefinition)
    {
        dialogueDefinition = null!;
        return false;
    }

    public bool TryGetEquipmentSlot(string slotId, out EquipmentSlotDefinition slotDefinition)
    {
        slotDefinition = null!;
        return false;
    }

    public bool TryGetEquipment(string itemId, out EquipmentDefinition equipmentDefinition)
    {
        equipmentDefinition = null!;
        return false;
    }

    public bool TryGetStatusEffect(string statusId, out StatusEffectDefinition statusDefinition)
    {
        statusDefinition = null!;
        return false;
    }

    public bool TryGetExperienceCurve(string curveId, out ExperienceCurveDefinition curveDefinition)
    {
        curveDefinition = null!;
        return false;
    }

    public bool TryGetStat(string statId, out StatDefinition statDefinition)
    {
        statDefinition = null!;
        return false;
    }

    public bool TryGetDamageType(string damageTypeId, out DamageTypeDefinition damageTypeDefinition)
    {
        damageTypeDefinition = null!;
        return false;
    }

    public bool TryGetTypeEffectivenessChart(string chartId, out TypeEffectivenessChartDefinition chartDefinition)
    {
        chartDefinition = null!;
        return false;
    }

    public bool TryGetLootTable(string lootTableId, out LootTableDefinition lootTableDefinition)
    {
        lootTableDefinition = null!;
        return false;
    }

    public bool TryGetEncounterTable(string encounterTableId, out EncounterTableDefinition encounterTableDefinition)
    {
        encounterTableDefinition = null!;
        return false;
    }

    public bool TryGetInteractable(string interactableId, out InteractableDefinition interactableDefinition)
    {
        interactableDefinition = null!;
        return false;
    }
}
