using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Equipment;
using Moonforge.Core.Equipment.Commands;
using Moonforge.Core.Equipment.Events;
using Moonforge.Core.Equipment.Queries;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;

namespace Moonforge.Core.Tests;

public sealed class EquipmentTests
{
    private const string SlotWeapon = "slot.weapon";
    private const string SlotArmor = "slot.armor";
    private const string ItemBlade = "item.gear.blade";
    private const string ItemSword = "item.gear.sword";
    private const string ItemVest = "item.gear.vest";

    [Fact]
    public void Equip_Moves_Item_From_Bag_To_Slot()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemBlade);

        DomainResult result = dispatcher.Dispatch(gameState, new EquipItemCommand(ItemBlade), CreateContext(sink, definitions));

        Assert.True(result.IsSuccess);
        Assert.Equal(ItemBlade, gameState.EquipmentState.GetEquippedItem(SlotWeapon));
        Assert.Equal(0, gameState.InventoryBag.GetTotalQuantity(ItemBlade));
        Assert.Contains(sink.Events, e => e is ItemEquippedEvent equipped && equipped.ItemId == ItemBlade && equipped.ReplacedItemId is null);
    }

    [Fact]
    public void Equip_Swaps_Previously_Equipped_Item_Back_To_Bag()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemBlade);
        GiveItem(gameState, dispatcher, definitions, sink, ItemSword);
        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemBlade), CreateContext(sink, definitions)).IsSuccess);

        DomainResult swap = dispatcher.Dispatch(gameState, new EquipItemCommand(ItemSword), CreateContext(sink, definitions));

        Assert.True(swap.IsSuccess);
        Assert.Equal(ItemSword, gameState.EquipmentState.GetEquippedItem(SlotWeapon));
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity(ItemBlade));
        Assert.Equal(0, gameState.InventoryBag.GetTotalQuantity(ItemSword));
        Assert.Contains(sink.Events, e => e is ItemEquippedEvent equipped && equipped.ItemId == ItemSword && equipped.ReplacedItemId == ItemBlade);
    }

    [Fact]
    public void Equip_Fails_When_Item_Not_In_Inventory()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();

        DomainResult result = dispatcher.Dispatch(gameState, new EquipItemCommand(ItemBlade), CreateContext(sink, definitions));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.InsufficientResources, result.Error!.Code);
        Assert.Null(gameState.EquipmentState.GetEquippedItem(SlotWeapon));
    }

    [Fact]
    public void Equip_Fails_When_Equipment_Definition_Is_Missing()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        definitions.AddItem(new ItemDefinition("item.plain.rock", 1));
        Assert.True(dispatcher.Dispatch(gameState, new AddInventoryItemCommand("item.plain.rock", 1), CreateContext(sink, definitions)).IsSuccess);

        DomainResult result = dispatcher.Dispatch(gameState, new EquipItemCommand("item.plain.rock"), CreateContext(sink, definitions));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.NotFound, result.Error!.Code);
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity("item.plain.rock"));
    }

    [Fact]
    public void Unequip_Returns_Item_To_Bag_And_Clears_Slot()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemBlade);
        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemBlade), CreateContext(sink, definitions)).IsSuccess);

        DomainResult result = dispatcher.Dispatch(gameState, new UnequipItemCommand(SlotWeapon), CreateContext(sink, definitions));

        Assert.True(result.IsSuccess);
        Assert.Null(gameState.EquipmentState.GetEquippedItem(SlotWeapon));
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity(ItemBlade));
        Assert.Contains(sink.Events, e => e is ItemUnequippedEvent unequipped && unequipped.ItemId == ItemBlade && unequipped.SlotId == SlotWeapon);
    }

    [Fact]
    public void Unequip_Fails_When_Slot_Is_Empty()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();

        DomainResult result = dispatcher.Dispatch(gameState, new UnequipItemCommand(SlotWeapon), CreateContext(sink, definitions));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.ValidationFailed, result.Error!.Code);
    }

    [Fact]
    public void Bonuses_Query_Aggregates_Across_Equipped_Slots()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemBlade);
        GiveItem(gameState, dispatcher, definitions, sink, ItemVest);
        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemBlade), CreateContext(sink, definitions)).IsSuccess);
        Assert.True(dispatcher.Dispatch(gameState, new EquipItemCommand(ItemVest), CreateContext(sink, definitions)).IsSuccess);

        GetEquipmentBonusesQueryHandler handler = new(definitions);
        IReadOnlyDictionary<string, int> bonuses = handler.Query(gameState, new GetEquipmentBonusesQuery());

        Assert.Equal(2, bonuses[StandardEquipmentStats.Attack]);
        Assert.Equal(3, bonuses[StandardEquipmentStats.Defense]);
    }

    [Fact]
    public void Equip_Failure_Rolls_Back_Atomically()
    {
        (GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink) = CreateWorld();
        GiveItem(gameState, dispatcher, definitions, sink, ItemBlade);
        int eventsBeforeFailedEquip = sink.Events.Count;

        DomainResult result = dispatcher.Dispatch(gameState, new EquipItemCommand("item.nonexistent"), CreateContext(sink, definitions));

        Assert.False(result.IsSuccess);
        Assert.Equal(1, gameState.InventoryBag.GetTotalQuantity(ItemBlade));
        Assert.Null(gameState.EquipmentState.GetEquippedItem(SlotWeapon));
        Assert.Equal(eventsBeforeFailedEquip, sink.Events.Count);
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) CreateWorld()
    {
        GameState gameState = new();
        InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
            .AddItem(new ItemDefinition(ItemBlade, 1))
            .AddItem(new ItemDefinition(ItemSword, 1))
            .AddItem(new ItemDefinition(ItemVest, 1))
            .AddEquipmentSlot(new EquipmentSlotDefinition(SlotWeapon))
            .AddEquipmentSlot(new EquipmentSlotDefinition(SlotArmor))
            .AddEquipment(new EquipmentDefinition(ItemBlade, SlotWeapon, new Dictionary<string, int>
            {
                [StandardEquipmentStats.Attack] = 2
            }))
            .AddEquipment(new EquipmentDefinition(ItemSword, SlotWeapon, new Dictionary<string, int>
            {
                [StandardEquipmentStats.Attack] = 4
            }))
            .AddEquipment(new EquipmentDefinition(ItemVest, SlotArmor, new Dictionary<string, int>
            {
                [StandardEquipmentStats.Defense] = 3
            }));

        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new AddInventoryItemCommandHandler());
        dispatcher.Register(new EquipItemCommandHandler());
        dispatcher.Register(new UnequipItemCommandHandler());

        return (gameState, dispatcher, definitions, sink);
    }

    private static void GiveItem(GameState gameState, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog definitions, InMemoryDomainEventSink sink, string itemId)
    {
        DomainResult result = dispatcher.Dispatch(gameState, new AddInventoryItemCommand(itemId, 1), CreateContext(sink, definitions));
        Assert.True(result.IsSuccess);
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog definitions)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 42, sequence: 54),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            definitions);
    }
}
