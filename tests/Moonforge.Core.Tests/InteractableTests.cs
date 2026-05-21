using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Exploration;
using Moonforge.Core.Exploration.Commands;
using Moonforge.Core.Interactables;
using Moonforge.Core.Interactables.Commands;
using Moonforge.Core.Interactables.Events;
using Moonforge.Core.Inventory.Commands;
using Moonforge.Core.Loot;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Formulas;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.Runtime.Results;
using Moonforge.Core.Runtime.Time;
using Moonforge.Core.World;

namespace Moonforge.Core.Tests;

public sealed class InteractableTests
{
    private const string Hero = "party.hero";
    private const string ChestDef = "interactable.chest";
    private const string ChestInst = "chest.01";
    private const string DoorDef = "interactable.door";
    private const string DoorInst = "door.01";
    private const string LeverDef = "interactable.lever";
    private const string LeverInst = "lever.01";
    private const string KeyItemId = "item.iron_key";
    private const string GoldId = "currency.gold";
    private const string PotionId = "item.potion";
    private const string ChestLootTable = "loot.chest.iron";

    [Fact]
    public void Place_Adds_Instance_With_Definition_Defaults()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        defs.AddInteractable(new InteractableDefinition(ChestDef, maxUses: 1, startsLocked: false));

        DomainResult result = dispatcher.Dispatch(gs,
            new PlaceInteractableCommand(ChestInst, ChestDef, new GridPosition(2, 2)),
            CreateContext(sink, defs));

        Assert.True(result.IsSuccess);
        Assert.True(gs.InteractablesState.TryGet(ChestInst, out InteractableInstance inst));
        Assert.Equal(InteractableStatus.Default, inst.Status);
        Assert.Equal(1, inst.UsesRemaining);
        Assert.False(inst.Locked);
        Assert.Contains(sink.Events, e => e is InteractablePlacedEvent p && p.InstanceId == ChestInst);
    }

    [Fact]
    public void Interact_Out_Of_Range_Fails()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 0, y: 0);
        defs.AddInteractable(new InteractableDefinition(ChestDef));
        Place(dispatcher, gs, defs, sink, ChestDef, ChestInst, new GridPosition(5, 5));

        DomainResult result = dispatcher.Dispatch(gs, new InteractWithCommand(Hero, ChestInst), CreateContext(sink, defs));

        Assert.False(result.IsSuccess);
        Assert.Equal(DomainErrorCode.ValidationFailed, result.Error!.Code);
    }

    [Fact]
    public void Interact_Adjacent_Succeeds_And_Marks_Consumed_When_Max_Uses_Is_One()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        defs.AddInteractable(new InteractableDefinition(ChestDef, maxUses: 1));
        Place(dispatcher, gs, defs, sink, ChestDef, ChestInst, new GridPosition(1, 2)); // 1 tile south

        DomainResult result = dispatcher.Dispatch(gs, new InteractWithCommand(Hero, ChestInst), CreateContext(sink, defs));

        Assert.True(result.IsSuccess);
        Assert.True(gs.InteractablesState.TryGet(ChestInst, out InteractableInstance inst));
        Assert.Equal(0, inst.UsesRemaining);
        Assert.Equal(InteractableStatus.Consumed, inst.Status);
        Assert.Contains(sink.Events, e => e is InteractableInteractedEvent i && i.InstanceId == ChestInst);
        Assert.Contains(sink.Events, e => e is InteractableConsumedEvent c && c.InstanceId == ChestInst);
    }

    [Fact]
    public void Interact_With_Consumed_Interactable_Fails()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        defs.AddInteractable(new InteractableDefinition(ChestDef, maxUses: 1));
        Place(dispatcher, gs, defs, sink, ChestDef, ChestInst, new GridPosition(1, 1));
        Assert.True(dispatcher.Dispatch(gs, new InteractWithCommand(Hero, ChestInst), CreateContext(sink, defs)).IsSuccess);

        DomainResult second = dispatcher.Dispatch(gs, new InteractWithCommand(Hero, ChestInst), CreateContext(sink, defs));

        Assert.False(second.IsSuccess);
        Assert.Equal(DomainErrorCode.Conflict, second.Error!.Code);
    }

    [Fact]
    public void Locked_Door_Without_Key_Emits_Locked_Event_And_Leaves_State_Unchanged()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        defs.AddItem(new ItemDefinition(KeyItemId, 1));
        defs.AddInteractable(new InteractableDefinition(
            DoorDef,
            startsLocked: true,
            requiredKeyItemId: KeyItemId));
        Place(dispatcher, gs, defs, sink, DoorDef, DoorInst, new GridPosition(2, 1));

        // Interact returns Success — the lock prevents action but is not an error. UI checks
        // InteractableLockedEvent to know whether the action actually happened.
        DomainResult result = dispatcher.Dispatch(gs, new InteractWithCommand(Hero, DoorInst), CreateContext(sink, defs));

        Assert.True(result.IsSuccess);
        Assert.Contains(sink.Events, e => e is InteractableLockedEvent l && l.RequiredKeyItemId == KeyItemId);
        Assert.True(gs.InteractablesState.TryGet(DoorInst, out InteractableInstance inst));
        Assert.True(inst.Locked);
        Assert.Equal(InteractableStatus.Default, inst.Status);
        Assert.DoesNotContain(sink.Events, e => e is InteractableInteractedEvent);
    }

    [Fact]
    public void Locked_Door_With_Key_Unlocks_Consumes_Key_And_Interacts()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        defs.AddItem(new ItemDefinition(KeyItemId, 1));
        defs.AddInteractable(new InteractableDefinition(
            DoorDef,
            startsLocked: true,
            requiredKeyItemId: KeyItemId,
            consumeKeyOnUnlock: true));
        Assert.True(dispatcher.Dispatch(gs, new AddInventoryItemCommand(KeyItemId, 1), CreateContext(sink, defs)).IsSuccess);
        Place(dispatcher, gs, defs, sink, DoorDef, DoorInst, new GridPosition(2, 1));

        DomainResult result = dispatcher.Dispatch(gs, new InteractWithCommand(Hero, DoorInst), CreateContext(sink, defs));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, gs.InventoryBag.GetTotalQuantity(KeyItemId));
        Assert.True(gs.InteractablesState.TryGet(DoorInst, out InteractableInstance inst));
        Assert.False(inst.Locked);
    }

    [Fact]
    public void Grant_Loot_Table_Effect_Deposits_Items_And_Currency()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        defs.AddItem(new ItemDefinition(PotionId, 99));
        defs.AddCurrency(new CurrencyDefinition(GoldId, 999_999));
        defs.AddLootTable(new LootTableDefinition(
            ChestLootTable,
            LootRollMode.RollEach,
            [
                LootEntryDefinition.Item(PotionId, chancePercent: 100, minQuantity: 2, maxQuantity: 2),
                LootEntryDefinition.Currency(GoldId, chancePercent: 100, minQuantity: 50, maxQuantity: 50)
            ]));
        defs.AddInteractable(new InteractableDefinition(
            ChestDef,
            effects: [new InteractableEffectDefinition(InteractableEffectKind.GrantLootTable, ChestLootTable)]));
        Place(dispatcher, gs, defs, sink, ChestDef, ChestInst, new GridPosition(1, 2));

        DomainResult result = dispatcher.Dispatch(gs, new InteractWithCommand(Hero, ChestInst), CreateContext(sink, defs));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, gs.InventoryBag.GetTotalQuantity(PotionId));
        Assert.Equal(50L, gs.CurrencyWallet.GetBalance(GoldId));
    }

    [Fact]
    public void Set_World_Bool_Effect_Updates_World_State()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 0, y: 0);
        defs.AddInteractable(new InteractableDefinition(
            "sign",
            effects: [new InteractableEffectDefinition(InteractableEffectKind.SetWorldBool, "flag.read_sign", boolValue: true)]));
        Place(dispatcher, gs, defs, sink, "sign", "sign.01", new GridPosition(0, 1));

        Assert.True(dispatcher.Dispatch(gs, new InteractWithCommand(Hero, "sign.01"), CreateContext(sink, defs)).IsSuccess);

        Assert.True(gs.WorldState.TryGet("flag.read_sign", out WorldVariableValue v));
        Assert.True(v.TryGetBool(out bool b) && b);
    }

    [Fact]
    public void Lever_Can_Change_Status_Of_Another_Interactable()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        defs.AddInteractable(new InteractableDefinition(DoorDef, maxUses: -1));
        Place(dispatcher, gs, defs, sink, DoorDef, DoorInst, new GridPosition(5, 5));
        defs.AddInteractable(new InteractableDefinition(
            LeverDef,
            effects: [new InteractableEffectDefinition(
                InteractableEffectKind.ChangeInteractableStatus,
                DoorInst,
                intValue: (int)InteractableStatus.Opened)]));
        Place(dispatcher, gs, defs, sink, LeverDef, LeverInst, new GridPosition(1, 2));

        Assert.True(dispatcher.Dispatch(gs, new InteractWithCommand(Hero, LeverInst), CreateContext(sink, defs)).IsSuccess);

        Assert.True(gs.InteractablesState.TryGet(DoorInst, out InteractableInstance door));
        Assert.Equal(InteractableStatus.Opened, door.Status);
        Assert.Contains(sink.Events, e => e is InteractableStatusChangedEvent c
            && c.InstanceId == DoorInst
            && c.Next == InteractableStatus.Opened);
    }

    [Fact]
    public void Unlimited_Uses_Stay_Interactable()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        defs.AddInteractable(new InteractableDefinition("fountain", maxUses: -1));
        Place(dispatcher, gs, defs, sink, "fountain", "fountain.01", new GridPosition(1, 2));

        for (int i = 0; i < 5; i++)
        {
            Assert.True(dispatcher.Dispatch(gs, new InteractWithCommand(Hero, "fountain.01"), CreateContext(sink, defs)).IsSuccess);
        }

        Assert.True(gs.InteractablesState.TryGet("fountain.01", out InteractableInstance inst));
        Assert.Equal(-1, inst.UsesRemaining);
        Assert.NotEqual(InteractableStatus.Consumed, inst.Status);
    }

    [Fact]
    public void Emit_Signal_Effect_Publishes_Interaction_Signal_Event()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        defs.AddInteractable(new InteractableDefinition(
            "shrine",
            effects: [new InteractableEffectDefinition(InteractableEffectKind.EmitInteractionSignal, "shrine.touched")]));
        Place(dispatcher, gs, defs, sink, "shrine", "shrine.01", new GridPosition(1, 2));

        Assert.True(dispatcher.Dispatch(gs, new InteractWithCommand(Hero, "shrine.01"), CreateContext(sink, defs)).IsSuccess);

        Assert.Contains(sink.Events, e => e is InteractionSignalEvent s
            && s.SignalKey == "shrine.touched"
            && s.SourceInstanceId == "shrine.01"
            && s.ActorId == Hero);
    }

    [Fact]
    public void Failing_Effect_Rolls_Back_All_State()
    {
        (GameState gs, CommandDispatcher dispatcher, InMemoryGameDefinitionCatalog defs, InMemoryDomainEventSink sink) = BuildWorld();
        PlaceHero(gs, dispatcher, defs, sink, x: 1, y: 1);
        // Loot table referenced by the chest doesn't exist → loot grant fails.
        defs.AddInteractable(new InteractableDefinition(
            ChestDef,
            effects:
            [
                new InteractableEffectDefinition(InteractableEffectKind.SetWorldBool, "flag.before", boolValue: true),
                new InteractableEffectDefinition(InteractableEffectKind.GrantLootTable, "loot.does_not_exist")
            ]));
        Place(dispatcher, gs, defs, sink, ChestDef, ChestInst, new GridPosition(1, 2));

        DomainResult result = dispatcher.Dispatch(gs, new InteractWithCommand(Hero, ChestInst), CreateContext(sink, defs));

        Assert.False(result.IsSuccess);
        Assert.False(gs.WorldState.TryGet("flag.before", out _));
        Assert.True(gs.InteractablesState.TryGet(ChestInst, out InteractableInstance inst));
        Assert.Equal(InteractableStatus.Default, inst.Status);
        Assert.Equal(1, inst.UsesRemaining);
    }

    [Fact]
    public void Persistence_Round_Trip_Preserves_Instances()
    {
        GameState original = new();
        original.InteractablesState.Add(new InteractableInstance(
            "chest.persisted",
            "interactable.chest",
            new GridPosition(7, 3),
            InteractableStatus.Opened,
            usesRemaining: 0,
            locked: false));

        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(original));
        GameStateSnapshot decoded = serializer.Deserialize(json);
        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.True(rebuilt.InteractablesState.TryGet("chest.persisted", out InteractableInstance rebuiltInstance));
        Assert.Equal(InteractableStatus.Opened, rebuiltInstance.Status);
        Assert.Equal(7, rebuiltInstance.Position.X);
        Assert.Equal(3, rebuiltInstance.Position.Y);
        Assert.Equal(0, rebuiltInstance.UsesRemaining);
    }

    [Fact]
    public void Schema_Bumped_To_Four()
    {
        Assert.Equal(4, GameStateSnapshotMapper.CurrentSchemaVersion);
    }

    private static (GameState, CommandDispatcher, InMemoryGameDefinitionCatalog, InMemoryDomainEventSink) BuildWorld()
    {
        GameState gs = new();
        InMemoryGameDefinitionCatalog defs = new();
        InMemoryDomainEventSink sink = new();
        CommandDispatcher dispatcher = new();
        dispatcher.Register(new PlaceInteractableCommandHandler());
        dispatcher.Register(new RemoveInteractableCommandHandler());
        dispatcher.Register(new InteractWithCommandHandler());
        dispatcher.Register(new AddInventoryItemCommandHandler());
        dispatcher.Register(new UpsertExplorationActorCommandHandler());
        return (gs, dispatcher, defs, sink);
    }

    private static void PlaceHero(
        GameState gs,
        CommandDispatcher dispatcher,
        InMemoryGameDefinitionCatalog defs,
        InMemoryDomainEventSink sink,
        int x,
        int y)
    {
        gs.ExplorationState.UpsertActor(Hero, new GridPosition(x, y), blocksMovement: true);
    }

    private static void Place(
        CommandDispatcher dispatcher,
        GameState gs,
        InMemoryGameDefinitionCatalog defs,
        InMemoryDomainEventSink sink,
        string definitionId,
        string instanceId,
        GridPosition position)
    {
        Assert.True(dispatcher.Dispatch(
            gs,
            new PlaceInteractableCommand(instanceId, definitionId, position),
            CreateContext(sink, defs)).IsSuccess);
    }

    private static CommandContext CreateContext(InMemoryDomainEventSink sink, IGameDefinitionCatalog defs)
    {
        return new CommandContext(
            new Pcg32RandomSource(seed: 7, sequence: 1),
            new SimulationClock(0),
            new NoOpFormulaEvaluator(),
            sink,
            defs);
    }
}
