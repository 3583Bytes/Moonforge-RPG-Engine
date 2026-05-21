using System;
using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

public sealed class InMemoryGameDefinitionCatalog : IGameDefinitionCatalog
{
    private readonly Dictionary<string, ItemDefinition> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CurrencyDefinition> _currencies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RecipeDefinition> _recipes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ShopDefinition> _shops = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QuestDefinition> _quests = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DialogueDefinition> _dialogues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EquipmentSlotDefinition> _equipmentSlots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EquipmentDefinition> _equipment = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StatusEffectDefinition> _statusEffects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExperienceCurveDefinition> _experienceCurves = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StatDefinition> _stats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DamageTypeDefinition> _damageTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeEffectivenessChartDefinition> _typeCharts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LootTableDefinition> _lootTables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EncounterTableDefinition> _encounterTables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InteractableDefinition> _interactables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EvolutionDefinition> _evolutions = new(StringComparer.Ordinal);

    public InMemoryGameDefinitionCatalog AddItem(ItemDefinition itemDefinition)
    {
        _items[itemDefinition.Id] = itemDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddCurrency(CurrencyDefinition currencyDefinition)
    {
        _currencies[currencyDefinition.Id] = currencyDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddRecipe(RecipeDefinition recipeDefinition)
    {
        _recipes[recipeDefinition.Id] = recipeDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddShop(ShopDefinition shopDefinition)
    {
        _shops[shopDefinition.Id] = shopDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddQuest(QuestDefinition questDefinition)
    {
        _quests[questDefinition.Id] = questDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddDialogue(DialogueDefinition dialogueDefinition)
    {
        _dialogues[dialogueDefinition.Id] = dialogueDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddEquipmentSlot(EquipmentSlotDefinition slotDefinition)
    {
        _equipmentSlots[slotDefinition.Id] = slotDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddEquipment(EquipmentDefinition equipmentDefinition)
    {
        _equipment[equipmentDefinition.ItemId] = equipmentDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddStatusEffect(StatusEffectDefinition statusDefinition)
    {
        _statusEffects[statusDefinition.Id] = statusDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddExperienceCurve(ExperienceCurveDefinition curveDefinition)
    {
        _experienceCurves[curveDefinition.Id] = curveDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddStat(StatDefinition statDefinition)
    {
        _stats[statDefinition.Id] = statDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddDamageType(DamageTypeDefinition damageTypeDefinition)
    {
        _damageTypes[damageTypeDefinition.Id] = damageTypeDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddTypeEffectivenessChart(TypeEffectivenessChartDefinition chartDefinition)
    {
        _typeCharts[chartDefinition.Id] = chartDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddLootTable(LootTableDefinition lootTableDefinition)
    {
        _lootTables[lootTableDefinition.Id] = lootTableDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddEncounterTable(EncounterTableDefinition encounterTableDefinition)
    {
        _encounterTables[encounterTableDefinition.Id] = encounterTableDefinition;
        return this;
    }

    public InMemoryGameDefinitionCatalog AddInteractable(InteractableDefinition interactableDefinition)
    {
        _interactables[interactableDefinition.Id] = interactableDefinition;
        return this;
    }

    public bool TryGetItem(string itemId, out ItemDefinition itemDefinition)
    {
        return _items.TryGetValue(itemId, out itemDefinition!);
    }

    public bool TryGetCurrency(string currencyId, out CurrencyDefinition currencyDefinition)
    {
        return _currencies.TryGetValue(currencyId, out currencyDefinition!);
    }

    public bool TryGetRecipe(string recipeId, out RecipeDefinition recipeDefinition)
    {
        return _recipes.TryGetValue(recipeId, out recipeDefinition!);
    }

    public bool TryGetShop(string shopId, out ShopDefinition shopDefinition)
    {
        return _shops.TryGetValue(shopId, out shopDefinition!);
    }

    public bool TryGetQuest(string questId, out QuestDefinition questDefinition)
    {
        return _quests.TryGetValue(questId, out questDefinition!);
    }

    public bool TryGetDialogue(string dialogueId, out DialogueDefinition dialogueDefinition)
    {
        return _dialogues.TryGetValue(dialogueId, out dialogueDefinition!);
    }

    public bool TryGetEquipmentSlot(string slotId, out EquipmentSlotDefinition slotDefinition)
    {
        return _equipmentSlots.TryGetValue(slotId, out slotDefinition!);
    }

    public bool TryGetEquipment(string itemId, out EquipmentDefinition equipmentDefinition)
    {
        return _equipment.TryGetValue(itemId, out equipmentDefinition!);
    }

    public bool TryGetStatusEffect(string statusId, out StatusEffectDefinition statusDefinition)
    {
        return _statusEffects.TryGetValue(statusId, out statusDefinition!);
    }

    public bool TryGetExperienceCurve(string curveId, out ExperienceCurveDefinition curveDefinition)
    {
        return _experienceCurves.TryGetValue(curveId, out curveDefinition!);
    }

    public bool TryGetStat(string statId, out StatDefinition statDefinition)
    {
        return _stats.TryGetValue(statId, out statDefinition!);
    }

    public bool TryGetDamageType(string damageTypeId, out DamageTypeDefinition damageTypeDefinition)
    {
        return _damageTypes.TryGetValue(damageTypeId, out damageTypeDefinition!);
    }

    public bool TryGetTypeEffectivenessChart(string chartId, out TypeEffectivenessChartDefinition chartDefinition)
    {
        return _typeCharts.TryGetValue(chartId, out chartDefinition!);
    }

    public bool TryGetLootTable(string lootTableId, out LootTableDefinition lootTableDefinition)
    {
        return _lootTables.TryGetValue(lootTableId, out lootTableDefinition!);
    }

    public bool TryGetEncounterTable(string encounterTableId, out EncounterTableDefinition encounterTableDefinition)
    {
        return _encounterTables.TryGetValue(encounterTableId, out encounterTableDefinition!);
    }

    public bool TryGetInteractable(string interactableId, out InteractableDefinition interactableDefinition)
    {
        return _interactables.TryGetValue(interactableId, out interactableDefinition!);
    }
}
