using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Core.Quests;
using Moonforge.Sample.ConsoleApp.Persistence;

namespace Moonforge.Sample.Console.Tests;

public sealed class RoguelikeSaveStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsRunAndMetaData()
    {
        string savePath = Path.Combine(Path.GetTempPath(), $"rpgengine-sample-save-{Guid.NewGuid():N}.json");
        try
        {
            RoguelikeSaveStore store = new(savePath);

            GameStateSnapshot engineState = new()
            {
                SchemaVersion = 1,
                ContentVersion = "v1.0.0-data.0",
                SimulationMinutes = 60
            };
            engineState.InventoryBag.CapacitySlots = 20;
            engineState.InventoryBag.Stacks.Add(new InventoryStackSnapshot
            {
                ItemId = "item.potion.medium",
                Quantity = 2,
                StackLimit = 10
            });
            engineState.InventoryBag.Stacks.Add(new InventoryStackSnapshot
            {
                ItemId = "item.herb",
                Quantity = 4,
                StackLimit = 20
            });
            engineState.CurrencyWallet.Balances.Add(new CurrencyBalanceSnapshot
            {
                CurrencyId = "currency.gold",
                Amount = 87
            });
            engineState.CurrencyWallet.Balances.Add(new CurrencyBalanceSnapshot
            {
                CurrencyId = "currency.token",
                Amount = 5
            });
            engineState.Quest.Quests.Add(new QuestInstanceSnapshot
            {
                QuestId = "quest.contract.hunt.warrens",
                Status = QuestStatus.Active,
                ObjectiveProgress = new Dictionary<string, int> { ["obj.kill.warrens"] = 2 }
            });
            engineState.Equipment.Slots.Add(new EquipmentSlotSnapshot
            {
                SlotId = "slot.weapon",
                ItemId = "item.gear.bronze_blade"
            });

            string engineJson = store.SerializeEngineSnapshot(engineState);

            RoguelikeSaveFile input = new(
                SchemaVersion: 1,
                UnlockedMetaUnlockIds: ["FieldRations", "LuckyFinds"],
                Run: new RoguelikeRunSaveData(
                    RunSeed: 12345,
                    CurrentDungeonFloor: 3,
                    BattleSequence: 9,
                    SelectedClass: "Knight",
                    ActiveContractQuestId: "quest.contract.hunt.warrens",
                    ContractsReadyForTurnIn: ["quest.contract.remedy"],
                    ClearedBossFloors: [3],
                    HeroX: 7,
                    HeroY: 11,
                    ResumeScene: "Dungeon",
                    LastMessage: "Autosave checkpoint.",
                    PendingBossRewardFloor: 3,
                    DungeonFloors: new Dictionary<int, DungeonFloorSaveData>
                    {
                        [3] = new DungeonFloorSaveData(
                            3,
                            3,
                            [1, 1, 1, 1, 3, 1, 1, 1, 1],
                            1,
                            1,
                            2,
                            2)
                    },
                    EngineStateJson: engineJson));

            bool saved = store.TrySave(input, out string? saveError);
            Assert.True(saved, saveError);
            Assert.True(store.Exists());

            bool loaded = store.TryLoad(out RoguelikeSaveFile? output, out string? loadError);
            Assert.True(loaded, loadError);
            Assert.NotNull(output);
            Assert.Equal(1, output!.SchemaVersion);
            Assert.Equal(2, output.UnlockedMetaUnlockIds.Count);
            Assert.NotNull(output.Run);
            Assert.Equal(3, output.Run!.CurrentDungeonFloor);
            Assert.Equal("Knight", output.Run.SelectedClass);

            GameStateSnapshot rehydrated = store.DeserializeEngineSnapshot(output.Run.EngineStateJson);
            Assert.Equal(20, rehydrated.InventoryBag.CapacitySlots);
            Assert.Equal(2, rehydrated.InventoryBag.Stacks.Count);
            Assert.Equal(2, rehydrated.CurrencyWallet.Balances.Count);
            Assert.Single(rehydrated.Quest.Quests);
            Assert.Single(rehydrated.Equipment.Slots);
            Assert.Single(output.Run.DungeonFloors);
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [Fact]
    public void Delete_RemovesExistingSaveFile()
    {
        string savePath = Path.Combine(Path.GetTempPath(), $"rpgengine-sample-save-{Guid.NewGuid():N}.json");
        RoguelikeSaveStore store = new(savePath);
        try
        {
            RoguelikeSaveFile input = new(
                SchemaVersion: 1,
                UnlockedMetaUnlockIds: [],
                Run: null);
            bool saved = store.TrySave(input, out string? saveError);
            Assert.True(saved, saveError);
            Assert.True(store.Exists());

            bool deleted = store.TryDelete(out string? deleteError);
            Assert.True(deleted, deleteError);
            Assert.False(store.Exists());
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [Fact]
    public void LegacyV2Snapshot_IsMigratedToCurrentSchemaOnLoad()
    {
        RoguelikeSaveStore store = new(Path.Combine(Path.GetTempPath(), $"rpgengine-migration-{Guid.NewGuid():N}.json"));

        // Simulate a payload that was serialized when the engine was at schema v2. The
        // registered LegacyV2ToV3SaveMigration must bump it to v3 before deserialization.
        const string legacyV2Json = "{\"schemaVersion\":2,\"contentVersion\":\"v1\",\"simulationMinutes\":0," +
                                    "\"currencyWallet\":{\"balances\":[],\"maxes\":[]}," +
                                    "\"inventoryBag\":{\"capacitySlots\":0,\"stacks\":[]}," +
                                    "\"quest\":{\"quests\":[]},\"dialogue\":{\"dialogues\":[]}," +
                                    "\"shop\":{\"stocks\":[],\"restocks\":[]}," +
                                    "\"world\":{\"variables\":[]}," +
                                    "\"exploration\":{\"map\":{\"mapId\":null,\"width\":0,\"height\":0,\"tiles\":[]},\"actors\":[]}," +
                                    "\"equipment\":{\"slots\":[]},\"progression\":{\"actors\":[]}," +
                                    "\"actorStats\":{\"actors\":[]},\"interactables\":{\"instances\":[]}}";

        GameStateSnapshot migrated = store.DeserializeEngineSnapshot(legacyV2Json);
        Assert.Equal(GameStateSnapshotMapper.CurrentSchemaVersion, migrated.SchemaVersion);
    }
}
