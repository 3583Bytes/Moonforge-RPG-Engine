using System.Collections.Generic;
using Moonforge.Core;
using Moonforge.Core.Exploration;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Quests;
using Moonforge.Core.Runtime.Random;
using Moonforge.Core.World;

namespace Moonforge.Core.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public void Round_Trip_Preserves_All_Engine_Owned_State()
    {
        GameState original = BuildStateWithEveryModulePopulated();
        JsonGameStateSerializer serializer = new();

        GameStateSnapshot snapshot = GameStateSnapshotMapper.Capture(original);
        string json = serializer.Serialize(snapshot);
        GameStateSnapshot decoded = serializer.Deserialize(json);

        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.Equal(original.ContentVersion, rebuilt.ContentVersion);
        Assert.Equal(original.SimulationMinutes, rebuilt.SimulationMinutes);

        Assert.Equal(100, rebuilt.CurrencyWallet.GetBalance("currency.gold"));
        Assert.Equal(5, rebuilt.CurrencyWallet.GetBalance("currency.token"));
        Assert.Equal(999_999, rebuilt.CurrencyWallet.GetMax("currency.gold"));

        Assert.Equal(24, rebuilt.InventoryBag.CapacitySlots);
        Assert.Equal(3, rebuilt.InventoryBag.GetTotalQuantity("item.potion"));
        Assert.Equal(2, rebuilt.InventoryBag.GetTotalQuantity("item.gear.sword"));

        Assert.True(rebuilt.QuestState.TryGet("quest.test", out QuestInstanceState quest));
        Assert.Equal(QuestStatus.Active, quest.Status);
        Assert.Equal(2, quest.GetObjectiveProgress("obj.kill"));

        Assert.True(rebuilt.WorldState.TryGet("flag.intro_seen", out WorldVariableValue boolVar));
        Assert.True(boolVar.TryGetBool(out bool boolValue));
        Assert.True(boolValue);
        Assert.True(rebuilt.WorldState.TryGet("counter.deaths", out WorldVariableValue intVar));
        Assert.True(intVar.TryGetInt(out int intValue));
        Assert.Equal(7, intValue);

        Assert.True(rebuilt.ExplorationState.Map.IsConfigured);
        Assert.Equal(4, rebuilt.ExplorationState.Map.Width);
        Assert.Equal(3, rebuilt.ExplorationState.Map.Height);
        Assert.True(rebuilt.ExplorationState.TryGetActor("party.hero", out ExplorationActorState hero));
        Assert.Equal(1, hero.X);
        Assert.Equal(2, hero.Y);

        Assert.Equal("item.gear.sword", rebuilt.EquipmentState.GetEquippedItem("slot.weapon"));
        Assert.Equal("item.gear.vest", rebuilt.EquipmentState.GetEquippedItem("slot.armor"));

        Assert.Equal(4, rebuilt.ShopState.TryGetStock("shop.town", "item.potion"));
        Assert.Equal(120L, rebuilt.ShopState.GetLastRestockMinute("shop.town"));
    }

    [Fact]
    public void Empty_State_Round_Trips_Without_Error()
    {
        GameState original = new();
        JsonGameStateSerializer serializer = new();

        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(original));
        GameStateSnapshot decoded = serializer.Deserialize(json);
        GameState rebuilt = new();
        GameStateSnapshotMapper.Apply(rebuilt, decoded);

        Assert.Equal(32, rebuilt.InventoryBag.CapacitySlots);
        Assert.Empty(rebuilt.CurrencyWallet.Balances);
        Assert.Empty(rebuilt.QuestState.Quests);
        Assert.Empty(rebuilt.EquipmentState.EquippedItems);
    }

    [Fact]
    public void Round_Trip_Preserves_Rng_Stream_Position()
    {
        Pcg32RandomSource rng = new(seed: 42, sequence: 54);
        for (int i = 0; i < 11; i++)
        {
            rng.NextUInt32();
        }

        JsonGameStateSerializer serializer = new();
        string json = serializer.Serialize(GameStateSnapshotMapper.Capture(new GameState(), rng));
        GameStateSnapshot decoded = serializer.Deserialize(json);

        Pcg32RandomSource? restored = GameStateSnapshotMapper.RestoreRandomSource(decoded);

        Assert.NotNull(restored);
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal(rng.NextUInt32(), restored!.NextUInt32());
        }
    }

    [Fact]
    public void Capture_Without_Rng_Leaves_Snapshot_Rng_Null()
    {
        GameStateSnapshot snapshot = GameStateSnapshotMapper.Capture(new GameState());

        Assert.Null(snapshot.Rng);
        Assert.Null(GameStateSnapshotMapper.RestoreRandomSource(snapshot));
    }

    [Fact]
    public void Legacy_Payload_Without_Rng_Field_Deserializes_With_Null_Rng()
    {
        JsonGameStateSerializer serializer = new();
        string legacyJson = "{\"schemaVersion\":8,\"contentVersion\":\"v1\",\"simulationMinutes\":0}";

        GameStateSnapshot decoded = serializer.Deserialize(legacyJson);

        Assert.Null(decoded.Rng);
        Assert.Null(GameStateSnapshotMapper.RestoreRandomSource(decoded));
    }

    [Fact]
    public void Apply_Sets_SchemaVersion_From_Snapshot()
    {
        GameStateSnapshot snapshot = GameStateSnapshotMapper.Capture(new GameState());
        GameState rebuilt = new();

        GameStateSnapshotMapper.Apply(rebuilt, snapshot);

        Assert.Equal(GameStateSnapshotMapper.CurrentSchemaVersion, rebuilt.SchemaVersion);
    }

    [Fact]
    public void Migration_Pipeline_Runs_In_Order_Until_Target_Version()
    {
        TestMigration v0 = new(fromVersion: 0, replaceValue: 1);
        JsonGameStateSerializer serializer = new(migrations: [v0]);

        string legacyJson = "{\"schemaVersion\":0,\"contentVersion\":\"\",\"simulationMinutes\":0,\"currencyWallet\":{\"balances\":[],\"maxes\":[]},\"inventoryBag\":{\"capacitySlots\":32,\"stacks\":[]},\"quest\":{\"quests\":[]},\"dialogue\":{\"dialogues\":[]},\"shop\":{\"stocks\":[],\"restocks\":[]},\"world\":{\"variables\":[]},\"exploration\":{\"map\":{\"mapId\":\"\",\"width\":0,\"height\":0,\"tiles\":[]},\"actors\":[]},\"equipment\":{\"slots\":[]}}";

        GameStateSnapshot decoded = serializer.Deserialize(legacyJson);

        Assert.Equal(1, decoded.SchemaVersion);
        Assert.True(v0.Ran);
    }

    private static GameState BuildStateWithEveryModulePopulated()
    {
        GameState gameState = new()
        {
            ContentVersion = "v9.9.9",
            SimulationMinutes = 480
        };

        gameState.CurrencyWallet.ConfigureMax("currency.gold", 999_999);
        gameState.CurrencyWallet.ConfigureMax("currency.token", 999);
        gameState.CurrencyWallet.Grant("currency.gold", 100);
        gameState.CurrencyWallet.Grant("currency.token", 5);

        gameState.InventoryBag.SetCapacity(24);
        gameState.InventoryBag.TryAdd("item.potion", 3, 10, out _);
        gameState.InventoryBag.TryAdd("item.gear.sword", 2, 5, out _);

        QuestInstanceState quest = gameState.QuestState.GetOrCreate("quest.test");
        quest.Status = QuestStatus.Active;
        quest.SetObjectiveProgress("obj.kill", 2);

        gameState.WorldState.Set("flag.intro_seen", WorldVariableValue.FromBool(true));
        gameState.WorldState.Set("counter.deaths", WorldVariableValue.FromInt(7));

        gameState.ExplorationState.Map.TryConfigure(
            "town",
            width: 4,
            height: 3,
            tiles: new[]
            {
                ExplorationTileFlags.Walkable, ExplorationTileFlags.Walkable, ExplorationTileFlags.BlocksLineOfSight, ExplorationTileFlags.Walkable,
                ExplorationTileFlags.Walkable, ExplorationTileFlags.BlocksLineOfSight, ExplorationTileFlags.Walkable, ExplorationTileFlags.Walkable,
                ExplorationTileFlags.Walkable, ExplorationTileFlags.Walkable, ExplorationTileFlags.Walkable, ExplorationTileFlags.Walkable
            },
            out _);
        gameState.ExplorationState.UpsertActor("party.hero", new GridPosition(1, 2), blocksMovement: true);

        gameState.EquipmentState.SetEquipped("slot.weapon", "item.gear.sword");
        gameState.EquipmentState.SetEquipped("slot.armor", "item.gear.vest");

        gameState.ShopState.SetStock("shop.town", "item.potion", 4);
        gameState.ShopState.SetLastRestockMinute("shop.town", 120);

        return gameState;
    }

    private sealed class TestMigration : ISaveMigration
    {
        private readonly int _replaceValue;

        public TestMigration(int fromVersion, int replaceValue)
        {
            FromVersion = fromVersion;
            _replaceValue = replaceValue;
        }

        public int FromVersion { get; }

        public bool Ran { get; private set; }

        public string Migrate(string payload)
        {
            Ran = true;
            return payload.Replace("\"schemaVersion\":0", $"\"schemaVersion\":{_replaceValue}");
        }
    }
}
