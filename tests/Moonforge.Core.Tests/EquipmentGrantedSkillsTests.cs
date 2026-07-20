using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Equipment.Commands;
using Moonforge.Core.Equipment.Queries;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class EquipmentGrantedSkillsTests
{
    private const string SlotWeapon = "slot.weapon";
    private const string SlotAccessory = "slot.accessory";
    private const string ItemWand = "item.gear.oak_wand";
    private const string ItemDagger = "item.gear.dagger";
    private const string ItemFocus = "item.gear.focus";
    private const string ItemPlain = "item.gear.plain";

    [Fact]
    public void Granted_Skills_Surface_While_Item_Is_Equipped()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = BuildWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemWand);

        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemWand), Context(sink, definitions)).IsSuccess);

        IReadOnlyList<string> granted = new GetEquipmentGrantedSkillsQueryHandler(definitions)
            .Query(gameState, new GetEquipmentGrantedSkillsQuery());

        Assert.Single(granted);
        Assert.Equal("skill.bolt", granted[0]);
    }

    [Fact]
    public void Unequipping_The_Item_Removes_Its_Granted_Skills()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = BuildWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemWand);
        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemWand), Context(sink, definitions)).IsSuccess);

        Assert.True(dispatcher.Dispatch(gameState, new UnequipItemCommand(SlotWeapon), Context(sink, definitions)).IsSuccess);

        IReadOnlyList<string> granted = new GetEquipmentGrantedSkillsQueryHandler(definitions)
            .Query(gameState, new GetEquipmentGrantedSkillsQuery());

        Assert.Empty(granted);
    }

    [Fact]
    public void Multiple_Equipped_Items_Union_Without_Duplicates()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = BuildWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemDagger);   // grants quick_strike + bolt
        GiveItem(gameState, dispatcher, definitions, sink, ItemFocus);    // grants bolt (overlap)
        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemDagger), Context(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemFocus), Context(sink, definitions)).IsSuccess);

        IReadOnlyList<string> granted = new GetEquipmentGrantedSkillsQueryHandler(definitions)
            .Query(gameState, new GetEquipmentGrantedSkillsQuery());

        Assert.Equal(2, granted.Count);
        Assert.Contains("skill.quick_strike", granted);
        Assert.Contains("skill.bolt", granted);
    }

    [Fact]
    public void Granted_Skill_Order_Is_Independent_Of_Equip_Order()
    {
        // Two worlds, same gear, opposite equip order. The skill list feeds battle skill
        // selection, so it must come back identical regardless of dictionary insertion order.
        (GameState first, CommandDispatcher firstDispatcher, InMemoryGameDefinitionCatalog firstDefs, InMemoryDomainEventSink firstSink) = BuildWorld();
        GiveItem(first, firstDispatcher, firstDefs, firstSink, ItemDagger);
        GiveItem(first, firstDispatcher, firstDefs, firstSink, ItemFocus);
        Assert.True(firstDispatcher.Dispatch(first, new EquipItemCommand(ItemDagger), Context(firstSink, firstDefs)).IsSuccess);
        Assert.True(firstDispatcher.Dispatch(first, new EquipItemCommand(ItemFocus), Context(firstSink, firstDefs)).IsSuccess);

        (GameState second, CommandDispatcher secondDispatcher, InMemoryGameDefinitionCatalog secondDefs, InMemoryDomainEventSink secondSink) = BuildWorld();
        GiveItem(second, secondDispatcher, secondDefs, secondSink, ItemDagger);
        GiveItem(second, secondDispatcher, secondDefs, secondSink, ItemFocus);
        Assert.True(secondDispatcher.Dispatch(second, new EquipItemCommand(ItemFocus), Context(secondSink, secondDefs)).IsSuccess);
        Assert.True(secondDispatcher.Dispatch(second, new EquipItemCommand(ItemDagger), Context(secondSink, secondDefs)).IsSuccess);

        IReadOnlyList<string> firstGranted = new GetEquipmentGrantedSkillsQueryHandler(firstDefs)
            .Query(first, new GetEquipmentGrantedSkillsQuery());
        IReadOnlyList<string> secondGranted = new GetEquipmentGrantedSkillsQueryHandler(secondDefs)
            .Query(second, new GetEquipmentGrantedSkillsQuery());

        Assert.Equal(firstGranted, secondGranted);
    }

    [Fact]
    public void Equipment_Without_Granted_Skills_Is_A_No_Op()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = BuildWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemPlain);
        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemPlain), Context(sink, definitions)).IsSuccess);

        IReadOnlyList<string> granted = new GetEquipmentGrantedSkillsQueryHandler(definitions)
            .Query(gameState, new GetEquipmentGrantedSkillsQuery());

        Assert.Empty(granted);
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) BuildWorld()
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddItem(new ItemDefinition(ItemWand, 1))
            .AddItem(new ItemDefinition(ItemDagger, 1))
            .AddItem(new ItemDefinition(ItemFocus, 1))
            .AddItem(new ItemDefinition(ItemPlain, 1))
            .AddEquipmentSlot(new EquipmentSlotDefinition(SlotWeapon))
            .AddEquipmentSlot(new EquipmentSlotDefinition(SlotAccessory))
            .AddEquipment(new EquipmentDefinition(
                ItemWand,
                SlotWeapon,
                grantedSkillIds: ["skill.bolt"]))
            .AddEquipment(new EquipmentDefinition(
                ItemDagger,
                SlotWeapon,
                grantedSkillIds: ["skill.quick_strike", "skill.bolt"]))
            .AddEquipment(new EquipmentDefinition(
                ItemFocus,
                SlotAccessory,
                grantedSkillIds: ["skill.bolt"]))
            .AddEquipment(new EquipmentDefinition(ItemPlain, SlotAccessory));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new AddInventoryItemCommandHandler());
        dispatcher.Register(new EquipItemCommandHandler());
        dispatcher.Register(new UnequipItemCommandHandler());

        return (gameState, dispatcher, definitions, sink);
    }

    private static void GiveItem(
        GameState gameState,
        CommandDispatcher dispatcher,
        InMemoryGameDefinitionCatalog definitions,
        InMemoryDomainEventSink sink,
        string itemId)
    {
        Assert.True(dispatcher.Dispatch(gameState, new AddInventoryItemCommand(itemId, 1), Context(sink, definitions)).IsSuccess);
    }

    private static CommandContext Context(InMemoryDomainEventSink sink, IGameDefinitionCatalog definitions)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 1, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
