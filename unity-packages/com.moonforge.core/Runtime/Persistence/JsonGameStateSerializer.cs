using System;
using System.Collections.Generic;
using Moonforge.Core.Persistence.Snapshots;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Moonforge.Core.Persistence
{

    public sealed class JsonGameStateSerializer : IGameStateSerializer
    {
        private readonly JsonSerializerSettings _settings;
        private readonly SaveMigrationPipeline? _migrationPipeline;

        public JsonGameStateSerializer(IEnumerable<ISaveMigration>? migrations = null, JsonSerializerSettings? settings = null)
        {
            _settings = settings ?? CreateDefaultSettings();
            _migrationPipeline = migrations is null ? null : new SaveMigrationPipeline(migrations);
        }

        public string Serialize(GameStateSnapshot snapshot)
        {
            return JsonConvert.SerializeObject(snapshot, _settings);
        }

        public GameStateSnapshot Deserialize(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentException("Save payload is empty.", nameof(payload));
            }

            string migrated = _migrationPipeline is null
                ? payload
                : _migrationPipeline.ApplyToLatest(payload, GameStateSnapshotMapper.CurrentSchemaVersion);

            GameStateSnapshot? snapshot = JsonConvert.DeserializeObject<GameStateSnapshot>(migrated, _settings);
            if (snapshot is null)
            {
                throw new InvalidOperationException("Save payload deserialized to null.");
            }

            return snapshot;
        }

        private static JsonSerializerSettings CreateDefaultSettings()
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
}
