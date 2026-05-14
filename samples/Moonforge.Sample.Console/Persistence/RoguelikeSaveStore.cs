using System.Text.Json;
using System.Text.Json.Serialization;
using Moonforge.Core.Exploration;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Moonforge.Sample.ConsoleApp.WorldGen;

namespace Moonforge.Sample.ConsoleApp.Persistence;

internal sealed class RoguelikeSaveStore
{
    private static readonly JsonSerializerOptions WrapperJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _savePath;
    private readonly JsonGameStateSerializer _engineSerializer;

    public RoguelikeSaveStore(string? savePath = null)
    {
        _savePath = savePath ?? BuildDefaultPath();
        _engineSerializer = new JsonGameStateSerializer(
            migrations: new ISaveMigration[]
            {
                new LegacyV2ToV3SaveMigration()
            });
    }

    public bool Exists()
    {
        return File.Exists(_savePath);
    }

    public bool TryLoad(out RoguelikeSaveFile? saveFile, out string? error)
    {
        saveFile = null;
        error = null;
        try
        {
            if (!File.Exists(_savePath))
            {
                return false;
            }

            string json = File.ReadAllText(_savePath);
            RoguelikeSaveFile? loaded = JsonSerializer.Deserialize<RoguelikeSaveFile>(json, WrapperJsonOptions);
            if (loaded is null)
            {
                error = "Save data was empty.";
                return false;
            }

            saveFile = loaded;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySave(RoguelikeSaveFile saveFile, out string? error)
    {
        error = null;
        try
        {
            string? directory = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(saveFile, WrapperJsonOptions);
            File.WriteAllText(_savePath, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryDelete(out string? error)
    {
        error = null;
        try
        {
            if (File.Exists(_savePath))
            {
                File.Delete(_savePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Serializes a <see cref="GameStateSnapshot"/> through <see cref="JsonGameStateSerializer"/>
    /// so the produced string carries the engine's current schema version and can be
    /// round-tripped through the migration pipeline.
    /// </summary>
    public string SerializeEngineSnapshot(GameStateSnapshot snapshot)
    {
        return _engineSerializer.Serialize(snapshot);
    }

    /// <summary>
    /// Deserializes engine state through <see cref="JsonGameStateSerializer"/>. Any registered
    /// <see cref="ISaveMigration"/> entries are applied to upgrade older payloads to the
    /// current schema version before the snapshot is returned.
    /// </summary>
    public GameStateSnapshot DeserializeEngineSnapshot(string payload)
    {
        return _engineSerializer.Deserialize(payload);
    }

    private static string BuildDefaultPath()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Moonforge",
            "SampleConsole");
        return Path.Combine(root, "savegame.json");
    }
}

internal sealed record RoguelikeSaveFile(
    int SchemaVersion,
    List<string> UnlockedMetaUnlockIds,
    RoguelikeRunSaveData? Run);

internal sealed record RoguelikeRunSaveData(
    ulong RunSeed,
    int CurrentDungeonFloor,
    int BattleSequence,
    string SelectedClass,
    string? ActiveContractQuestId,
    List<string> ContractsReadyForTurnIn,
    List<int> ClearedBossFloors,
    int HeroX,
    int HeroY,
    string ResumeScene,
    string LastMessage,
    int? PendingBossRewardFloor,
    Dictionary<int, DungeonFloorSaveData> DungeonFloors,
    string EngineStateJson);

internal sealed record DungeonFloorSaveData(
    int Width,
    int Height,
    List<int> Tiles,
    int SpawnX,
    int SpawnY,
    int StairsX,
    int StairsY,
    List<int>? PillarsXY = null);

internal static class DungeonFloorSaveMapper
{
    public static DungeonFloorSaveData ToSaveData(DungeonFloorBlueprint floor)
    {
        List<int> pillarsXY = new(floor.Pillars.Count * 2);
        for (int i = 0; i < floor.Pillars.Count; i++)
        {
            pillarsXY.Add(floor.Pillars[i].X);
            pillarsXY.Add(floor.Pillars[i].Y);
        }

        return new DungeonFloorSaveData(
            floor.Width,
            floor.Height,
            floor.Tiles.Select(x => (int)x).ToList(),
            floor.Spawn.X,
            floor.Spawn.Y,
            floor.Stairs.X,
            floor.Stairs.Y,
            pillarsXY);
    }

    public static DungeonFloorBlueprint ToBlueprint(DungeonFloorSaveData saveData)
    {
        List<ExplorationTileFlags> tiles = saveData.Tiles.Select(x => (ExplorationTileFlags)x).ToList();
        List<GridPosition> pillars = new();
        if (saveData.PillarsXY is not null)
        {
            for (int i = 0; i + 1 < saveData.PillarsXY.Count; i += 2)
            {
                pillars.Add(new GridPosition(saveData.PillarsXY[i], saveData.PillarsXY[i + 1]));
            }
        }

        return new DungeonFloorBlueprint(
            saveData.Width,
            saveData.Height,
            tiles,
            new GridPosition(saveData.SpawnX, saveData.SpawnY),
            new GridPosition(saveData.StairsX, saveData.StairsY),
            pillars);
    }
}

/// <summary>
/// Demonstrates an <see cref="ISaveMigration"/>: bumps any v2 engine snapshot to v3 on load.
/// Today the engine ships at schema v3 (see <c>GameStateSnapshotMapper.CurrentSchemaVersion</c>),
/// so this migration is a template for future use — it shows how to register a transformation
/// without changing call sites. Real migrations parse the payload, transform fields whose shape
/// changed, and rewrite <c>schemaVersion</c> to the next number.
/// </summary>
internal sealed class LegacyV2ToV3SaveMigration : ISaveMigration
{
    public int FromVersion => 2;

    public string Migrate(string payload)
    {
        return payload.Replace("\"schemaVersion\":2", "\"schemaVersion\":3");
    }
}
