using System;
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;
using Newtonsoft.Json.Linq;

namespace Moonforge.Core.Tests;

/// <summary>
/// Focused coverage for <see cref="SaveMigrationPipeline"/> — the load-bearing path that
/// upgrades old save payloads to the current schema. Complements the round-trip tests in
/// <c>PersistenceTests</c>; here we exercise multi-hop chains, gaps, malformed version fields,
/// and the loop-safety guards.
/// </summary>
public sealed class SaveMigrationTests
{
    [Fact]
    public void Multi_Hop_Chain_Applies_Every_Migration_In_Ascending_Order()
    {
        // Registered out of order on purpose — the pipeline keys migrations by FromVersion,
        // so application order must depend on the payload version, not registration order.
        RecordingMigration v0 = new(fromVersion: 0, toVersion: 1, addMinutes: 10);
        RecordingMigration v1 = new(fromVersion: 1, toVersion: 2, addMinutes: 100);
        RecordingMigration v2 = new(fromVersion: 2, toVersion: 3, addMinutes: 1000);
        SaveMigrationPipeline pipeline = new(new ISaveMigration[] { v2, v0, v1 });

        string result = pipeline.ApplyToLatest(
            "{\"schemaVersion\":0,\"simulationMinutes\":0}",
            targetSchemaVersion: 3);

        Assert.Equal(3, SchemaVersionOf(result));
        // 10 + 100 + 1000 proves each hop ran exactly once, in ascending order.
        Assert.Equal(1110, MinutesOf(result));
        Assert.Equal(1, v0.RunCount);
        Assert.Equal(1, v1.RunCount);
        Assert.Equal(1, v2.RunCount);
    }

    [Fact]
    public void Chain_Halts_At_Gap_When_Intermediate_Migration_Is_Missing()
    {
        // 0 -> 1 exists, 1 -> 2 is missing, 2 -> 3 exists. Starting at 0, the pipeline should
        // apply 0 -> 1 and then stop at the gap rather than skipping ahead to the 2 -> 3 step.
        RecordingMigration v0 = new(fromVersion: 0, toVersion: 1);
        RecordingMigration v2 = new(fromVersion: 2, toVersion: 3);
        SaveMigrationPipeline pipeline = new(new ISaveMigration[] { v0, v2 });

        string result = pipeline.ApplyToLatest("{\"schemaVersion\":0}", targetSchemaVersion: 3);

        Assert.Equal(1, SchemaVersionOf(result));
        Assert.Equal(1, v0.RunCount);
        Assert.Equal(0, v2.RunCount);
    }

    [Fact]
    public void Payload_Already_At_Target_Applies_No_Migrations()
    {
        RecordingMigration v3 = new(fromVersion: 3, toVersion: 4);
        SaveMigrationPipeline pipeline = new(new ISaveMigration[] { v3 });

        string result = pipeline.ApplyToLatest("{\"schemaVersion\":3}", targetSchemaVersion: 3);

        Assert.Equal(3, SchemaVersionOf(result));
        Assert.Equal(0, v3.RunCount);
    }

    [Fact]
    public void Payload_Newer_Than_Target_Passes_Through_Untouched()
    {
        // A save written by a newer engine (version 5) opened by an older target (3): the loop
        // condition is false immediately, so nothing runs and the payload is returned as-is.
        RecordingMigration v5 = new(fromVersion: 5, toVersion: 6);
        SaveMigrationPipeline pipeline = new(new ISaveMigration[] { v5 });

        string result = pipeline.ApplyToLatest("{\"schemaVersion\":5}", targetSchemaVersion: 3);

        Assert.Equal(5, SchemaVersionOf(result));
        Assert.Equal(0, v5.RunCount);
    }

    [Fact]
    public void Missing_SchemaVersion_Field_Is_Treated_As_Version_Zero()
    {
        RecordingMigration v0 = new(fromVersion: 0, toVersion: 1);
        SaveMigrationPipeline pipeline = new(new ISaveMigration[] { v0 });

        string result = pipeline.ApplyToLatest("{\"contentVersion\":\"legacy\"}", targetSchemaVersion: 1);

        Assert.Equal(1, SchemaVersionOf(result));
        Assert.Equal(1, v0.RunCount);
    }

    [Fact]
    public void Non_Integer_SchemaVersion_Is_Treated_As_Version_Zero()
    {
        RecordingMigration v0 = new(fromVersion: 0, toVersion: 1);
        SaveMigrationPipeline pipeline = new(new ISaveMigration[] { v0 });

        string result = pipeline.ApplyToLatest("{\"schemaVersion\":\"not-a-number\"}", targetSchemaVersion: 1);

        Assert.Equal(1, SchemaVersionOf(result));
        Assert.Equal(1, v0.RunCount);
    }

    [Fact]
    public void Non_Advancing_Migration_Terminates_Without_Infinite_Loop()
    {
        // A buggy migration that fails to advance the version must not spin forever — the
        // pipeline's "next <= version" guard breaks the loop after a single application.
        NonAdvancingMigration buggy = new();
        SaveMigrationPipeline pipeline = new(new ISaveMigration[] { buggy });

        string result = pipeline.ApplyToLatest("{\"schemaVersion\":0}", targetSchemaVersion: 3);

        Assert.Equal(0, SchemaVersionOf(result));
        Assert.Equal(1, buggy.RunCount);
    }

    [Fact]
    public void Duplicate_FromVersion_Registration_Throws()
    {
        RecordingMigration a = new(fromVersion: 0, toVersion: 1);
        RecordingMigration b = new(fromVersion: 0, toVersion: 2);

        Assert.Throws<ArgumentException>(() => new SaveMigrationPipeline(new ISaveMigration[] { a, b }));
    }

    [Fact]
    public void Multi_Step_Migration_Through_Serializer_Reaches_Current_Schema_And_Transforms_Data()
    {
        // End-to-end through the serializer, whose migration target is CurrentSchemaVersion.
        // Build a chain covering the last three schema steps so this stays correct as the
        // current version advances (assumes CurrentSchemaVersion >= 3, which it is).
        int current = GameStateSnapshotMapper.CurrentSchemaVersion;
        int start = current - 3;

        RecordingMigration step1 = new(fromVersion: start, toVersion: start + 1);
        RecordingMigration step2 = new(fromVersion: start + 1, toVersion: start + 2);
        RecordingMigration step3 = new(fromVersion: start + 2, toVersion: current, setContentVersion: "migrated-current");
        JsonGameStateSerializer serializer = new(migrations: new ISaveMigration[] { step1, step2, step3 });

        string oldPayload = $"{{\"schemaVersion\":{start},\"contentVersion\":\"original\"}}";
        GameStateSnapshot decoded = serializer.Deserialize(oldPayload);

        Assert.Equal(current, decoded.SchemaVersion);
        Assert.Equal("migrated-current", decoded.ContentVersion);
        Assert.Equal(1, step1.RunCount);
        Assert.Equal(1, step2.RunCount);
        Assert.Equal(1, step3.RunCount);
    }

    private static int SchemaVersionOf(string json)
    {
        return JObject.Parse(json)["schemaVersion"] is { Type: JTokenType.Integer } token
            ? token.Value<int>()
            : 0;
    }

    private static long MinutesOf(string json)
    {
        return JObject.Parse(json)["simulationMinutes"]?.Value<long>() ?? 0;
    }

    /// <summary>Test migration that bumps the version and optionally mutates payload data.</summary>
    private sealed class RecordingMigration : ISaveMigration
    {
        private readonly int _toVersion;
        private readonly long _addMinutes;
        private readonly string? _setContentVersion;

        public RecordingMigration(int fromVersion, int toVersion, long addMinutes = 0, string? setContentVersion = null)
        {
            FromVersion = fromVersion;
            _toVersion = toVersion;
            _addMinutes = addMinutes;
            _setContentVersion = setContentVersion;
        }

        public int FromVersion { get; }

        public int RunCount { get; private set; }

        public string Migrate(string payload)
        {
            RunCount++;
            JObject root = JObject.Parse(payload);
            root["schemaVersion"] = _toVersion;

            if (_addMinutes != 0)
            {
                long currentMinutes = root["simulationMinutes"]?.Value<long>() ?? 0;
                root["simulationMinutes"] = currentMinutes + _addMinutes;
            }

            if (_setContentVersion is not null)
            {
                root["contentVersion"] = _setContentVersion;
            }

            return root.ToString();
        }
    }

    /// <summary>A deliberately broken migration that never advances the schema version.</summary>
    private sealed class NonAdvancingMigration : ISaveMigration
    {
        public int FromVersion => 0;

        public int RunCount { get; private set; }

        public string Migrate(string payload)
        {
            RunCount++;
            return payload;
        }
    }
}
