# Persistence

`GameState` is converted to a `GameStateSnapshot` (a flat DTO mirror) for serialization,
written as JSON, and round-tripped through a migration pipeline on load so old saves
upgrade to the current schema without breaking. Definitions are **not** saved — they're
re-supplied at boot from the host game's content layer.

## The two halves

```
GameState (mutable runtime state)
   ↕  GameStateSnapshotMapper.Capture / Apply
GameStateSnapshot (DTO with public fields)
   ↕  JsonGameStateSerializer
JSON string
```

The DTOs live in `Persistence/Snapshots/` and use `[JsonInclude]`-friendly POCO shapes —
plain auto-properties, no constructors with private setters, all collections public
`List<T>`. This keeps `System.Text.Json` happy without source generation or contracts.

## Saving

```csharp
using Moonforge.Core.Persistence;
using Moonforge.Core.Persistence.Snapshots;

GameStateSnapshot snapshot = GameStateSnapshotMapper.Capture(gameState); // 1. State → DTO
string json = new JsonGameStateSerializer().Serialize(snapshot);         // 2. DTO → JSON
File.WriteAllText(savePath, json);                                       // 3. Persist
```

`Capture` reads every sub-state on `GameState`. The active battle is **intentionally
excluded** — saves should be taken between battles. If you call `Capture` mid-battle, the
battle reference is simply absent in the snapshot.

## Loading

```csharp
string json = File.ReadAllText(savePath);
// Deserialize runs the migration pipeline first, then parses the (now-current) JSON.
GameStateSnapshot snapshot = new JsonGameStateSerializer().Deserialize(json);
GameStateSnapshotMapper.Apply(gameState, snapshot);   // overwrites every sub-state on gameState
```

`Apply` overwrites every sub-state on the target `GameState` from the snapshot. Pass a
fresh `GameState` if you want a clean load — `Apply` does not reset fields it doesn't see.

## Schema versions

`GameStateSnapshotMapper.CurrentSchemaVersion` is the version the engine writes today.
Currently `7`. Saves carry their schema version in the top-level `schemaVersion` field.

When you change a persisted shape (rename a field, restructure a collection, add a
required field), bump `CurrentSchemaVersion` **and** add an `ISaveMigration` to upgrade
old saves to the new shape.

## Migrations

An `ISaveMigration` is a single-step transformation from version N to N+1, operating on
the raw JSON string:

```csharp
public sealed class V2ToV3Migration : ISaveMigration
{
    public int FromVersion => 2;   // applies to v2 payloads only — pipeline picks by this

    public string Migrate(string payload)
    {
        // Real migrations parse the JSON, transform fields, and rewrite schemaVersion.
        // Example: rename a field from oldName → newName everywhere it appears in the save.
        string transformed = payload.Replace("\"oldName\":", "\"newName\":");

        // Always bump the schemaVersion stamp so the pipeline knows this step is done
        // and can decide whether further migrations (v3→v4, ...) still need to run.
        return transformed.Replace("\"schemaVersion\":2", "\"schemaVersion\":3");
    }
}
```

Register migrations when constructing the serializer:

```csharp
// The pipeline orders steps by FromVersion, so registration order doesn't matter.
var serializer = new JsonGameStateSerializer(
    migrations: new ISaveMigration[]
    {
        new V1ToV2Migration(),
        new V2ToV3Migration()
    });

// Loading a v1 save auto-runs V1ToV2Migration → V2ToV3Migration → deserialize.
GameStateSnapshot snapshot = serializer.Deserialize(legacyJson);
```

The pipeline applies migrations in order until the payload reaches
`CurrentSchemaVersion`. Migrations are pure string transformations — they don't
deserialize into typed DTOs, so old removed fields can still be read.

### Migration rules

- **One step per `FromVersion`.** Don't register two migrations from the same source
  version — the pipeline picks one nondeterministically.
- **Always rewrite `schemaVersion`** at the end of the migration. If you forget, the
  pipeline runs the same migration again and infinite-loops (the pipeline does have a
  safety check that breaks if the version doesn't advance, but you'll lose throughput).
- **Don't make migrations dependent on the catalog.** They operate on raw save JSON, not
  on game content. If you need to know "did this item id used to map to a different one,"
  put that logic in the migration itself, not in the live catalog.

## Wrapping the engine snapshot in your own save file

A real game's save usually carries more than just engine state — meta-progression,
preferences, run seed, current scene. The clean pattern is to embed the engine
snapshot as a JSON string inside your wrapper:

```csharp
internal sealed record GameSaveFile(
    int FileSchemaVersion,
    string EngineStateJson,        // produced by JsonGameStateSerializer.Serialize
    Dictionary<string, object> HostMetadata);
```

Save:

```csharp
GameStateSnapshot engine = GameStateSnapshotMapper.Capture(gameState);
string engineJson = new JsonGameStateSerializer(migrations).Serialize(engine);

GameSaveFile file = new(
    FileSchemaVersion: 1,
    EngineStateJson: engineJson,
    HostMetadata: /* whatever */);

File.WriteAllText(path, JsonSerializer.Serialize(file));
```

Load:

```csharp
GameSaveFile file = JsonSerializer.Deserialize<GameSaveFile>(File.ReadAllText(path))!;

// JsonGameStateSerializer.Deserialize runs the migration pipeline.
GameStateSnapshot engine = new JsonGameStateSerializer(migrations).Deserialize(file.EngineStateJson);
GameStateSnapshotMapper.Apply(gameState, engine);
```

This way the engine's migration pipeline runs on the engine-state string, and your host
metadata can have its own independent schema version.

The roguelike sample (`samples/Moonforge.Sample.Roguelike.Console/Persistence/RoguelikeSaveStore.cs`)
is the reference implementation of this pattern.

## What gets saved

Every sub-state on `GameState`:

| State | Snapshot |
|---|---|
| `CurrencyWallet` | `CurrencyWalletSnapshot` — balances + caps |
| `InventoryBag` | `InventoryBagSnapshot` — capacity, every stack |
| `QuestState` | `QuestStateSnapshot` — per-quest status and objective progress |
| `DialogueState` | `DialogueStateSnapshot` — per-dialogue node/visited/chosen |
| `ShopState` | `ShopStateSnapshot` — stock per `(shop, item)` and last-restock times |
| `WorldState` | `WorldStateSnapshot` — every variable (typed) |
| `ExplorationState` | `ExplorationStateSnapshot` — map tiles + actor positions |
| `EquipmentState` | `EquipmentStateSnapshot` — slot occupants |
| `ProgressionState` | `ProgressionStateSnapshot` — per-actor curve/level/xp |
| `ActorStatsState` | `ActorStatsStateSnapshot` — bases + modifiers per actor |
| `InteractablesState` | `InteractablesStateSnapshot` — every placed instance |

Plus the top-level fields: `SchemaVersion`, `ContentVersion`, `SimulationMinutes`.

## What's *not* saved

- **`ActiveBattle`** — see above.
- **Definitions / `IGameDefinitionCatalog`** — re-supplied at boot.
- **Buffered events** — drained after each command, no need to persist.
- **The `Pcg32RandomSource` cursor** — not persisted in v1. If your game depends on
  resuming an exact RNG stream mid-run, store the seed yourself in a world variable and
  re-seed on load.

## See also

- [Architecture](architecture.md) — why the runtime/definition split exists
- [Cookbook](cookbook.md) — full save/load examples
- [Troubleshooting](troubleshooting.md) — "my save is missing a field" / "migration didn't run"
