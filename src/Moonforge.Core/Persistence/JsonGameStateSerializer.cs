using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Moonforge.Core.Persistence.Snapshots;

namespace Moonforge.Core.Persistence;

public sealed class JsonGameStateSerializer : IGameStateSerializer
{
    private readonly JsonSerializerOptions _options;
    private readonly SaveMigrationPipeline? _migrationPipeline;

    public JsonGameStateSerializer(IEnumerable<ISaveMigration>? migrations = null, JsonSerializerOptions? options = null)
    {
        _options = options ?? CreateDefaultOptions();
        _migrationPipeline = migrations is null ? null : new SaveMigrationPipeline(migrations);
    }

    public string Serialize(GameStateSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot, _options);
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

        GameStateSnapshot? snapshot = JsonSerializer.Deserialize<GameStateSnapshot>(migrated, _options);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Save payload deserialized to null.");
        }

        return snapshot;
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
