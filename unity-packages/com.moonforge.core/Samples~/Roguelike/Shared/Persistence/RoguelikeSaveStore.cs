using System;
using System.Collections.Generic;
using System.IO;
using Moonforge.Core.Exploration.Persistence;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Moonforge.Sample.Roguelike.Persistence
{

    internal sealed class RoguelikeSaveStore
    {
        private static readonly JsonSerializerSettings WrapperJsonSettings = CreateWrapperSettings();

        private readonly string _savePath;
        private readonly JsonGameStateSerializer _engineSerializer;

        public RoguelikeSaveStore(string? savePath = null)
        {
            _savePath = savePath ?? BuildDefaultPath();
            _engineSerializer = new JsonGameStateSerializer(
                migrations: new ISaveMigration[]
                {
                    new LegacyV2ToV3SaveMigration(),
                    new LegacyV3ToV4SaveMigration(),
                    new LegacyV4ToV5SaveMigration(),
                    new LegacyV5ToV6SaveMigration(),
                    new LegacyV6ToV7SaveMigration(),
                    new LegacyV7ToV8SaveMigration()
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
                RoguelikeSaveFile? loaded = JsonConvert.DeserializeObject<RoguelikeSaveFile>(json, WrapperJsonSettings);
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

                string json = JsonConvert.SerializeObject(saveFile, WrapperJsonSettings);
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

        private static JsonSerializerSettings CreateWrapperSettings()
        {
            JsonSerializerSettings settings = new()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
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

    /// <summary>
    /// Demonstrates an <see cref="ISaveMigration"/>: bumps any v2 engine snapshot to v3 on load.
    /// Real migrations parse the payload, transform fields whose shape changed, and rewrite
    /// <c>schemaVersion</c> to the next number.
    /// </summary>
    internal sealed class LegacyV2ToV3SaveMigration : ISaveMigration
    {
        public int FromVersion => 2;

        public string Migrate(string payload)
        {
            return payload.Replace("\"schemaVersion\":2", "\"schemaVersion\":3");
        }
    }

    /// <summary>
    /// v3 → v4 bumped the schema when the <c>Party</c> state was added. The new <c>party</c> field
    /// is missing from v3 payloads but defaults to an empty roster on deserialization, so the
    /// migration is a pure version bump.
    /// </summary>
    internal sealed class LegacyV3ToV4SaveMigration : ISaveMigration
    {
        public int FromVersion => 3;

        public string Migrate(string payload)
        {
            return payload.Replace("\"schemaVersion\":3", "\"schemaVersion\":4");
        }
    }

    /// <summary>
    /// v4 → v5 bumped the schema when the <c>Evolution</c> state was added. The new
    /// <c>evolution</c> field defaults to empty on deserialization, so this migration is a pure
    /// version bump.
    /// </summary>
    internal sealed class LegacyV4ToV5SaveMigration : ISaveMigration
    {
        public int FromVersion => 4;

        public string Migrate(string payload)
        {
            return payload.Replace("\"schemaVersion\":4", "\"schemaVersion\":5");
        }
    }

    /// <summary>
    /// v5 → v6 bumped the schema when the <c>Bestiary</c> state was added. The new
    /// <c>bestiary</c> field defaults to empty on deserialization, so this migration is a pure
    /// version bump.
    /// </summary>
    internal sealed class LegacyV5ToV6SaveMigration : ISaveMigration
    {
        public int FromVersion => 5;

        public string Migrate(string payload)
        {
            return payload.Replace("\"schemaVersion\":5", "\"schemaVersion\":6");
        }
    }

    /// <summary>
    /// v6 → v7 bumped the schema when the <c>ActorSkillPp</c> state was added. The new
    /// <c>actorSkillPp</c> field defaults to empty on deserialization, so this migration is a
    /// pure version bump.
    /// </summary>
    internal sealed class LegacyV6ToV7SaveMigration : ISaveMigration
    {
        public int FromVersion => 6;

        public string Migrate(string payload)
        {
            return payload.Replace("\"schemaVersion\":6", "\"schemaVersion\":7");
        }
    }

    /// <summary>
    /// v7 → v8 bumped the schema when the optional <c>rng</c> stream-position state was added.
    /// The field is absent from v7 payloads and deserializes to null, in which case the session
    /// falls back to re-seeding from the run seed, so this migration is a pure version bump.
    /// </summary>
    internal sealed class LegacyV7ToV8SaveMigration : ISaveMigration
    {
        public int FromVersion => 7;

        public string Migrate(string payload)
        {
            return payload.Replace("\"schemaVersion\":7", "\"schemaVersion\":8");
        }
    }
}
