# Encounter tables

Encounter tables are the structural twin of [loot tables](loot.md): a weighted, possibly
multi-result list, but the payload is an enemy / actor id rather than an item. Use them when
you need to answer "what spawns here?" with the same deterministic, seeded randomness the
rest of the engine relies on.

## Defining a table

```csharp
var defs = new InMemoryGameDefinitionCatalog()
    .AddEncounterTable(new EncounterTableDefinition(
        "enc.warrens.normal",
        EncounterRollMode.PickOne,
        [
            new EncounterEntryDefinition("enemy.goblin", weight: 70, minCount: 1, maxCount: 3),
            new EncounterEntryDefinition("enemy.wolf",   weight: 25, minCount: 1, maxCount: 2),
            new EncounterEntryDefinition("enemy.shaman", weight: 5,  minCount: 1, maxCount: 1)
        ]));
```

## Roll modes

- **`PickOne`** — weighted random choice of one entry. The chosen entry rolls its count
  range and produces a single `EncounterSpawn`. Good for "what kind of pack appears?"
- **`RollEach`** — each entry rolls its `ChancePercent` independently. Good for layered
  spawns like "elite always present, optional support, rare champion."

## Rolling

```csharp
var handler = new RollEncounterTableQueryHandler(defs, randomSource);
EncounterRollResult result = handler.Query(gameState, new RollEncounterTableQuery("enc.warrens.normal"));
foreach (EncounterSpawn s in result.Spawns)
{
    // Game code expands actor id + count into BattleActorDefinitions:
    for (int i = 0; i < s.Count; i++)
    {
        actors.Add(BuildActorFromTemplate(s.ActorId, depth: currentFloor));
    }
}
```

Direct resolver access for game code that needs to control the seed source:

```csharp
EncounterRollResult result = EncounterResolver.Roll(rng, table);
```

## Determinism

Same seed → same spawn sequence. In `RollEach` mode, the resolver advances the RNG once per
entry regardless of whether it drops — adding an always-skip entry to a table does not shift
the RNG stream for the entries after it.

## Design notes

- **No engine-side conditions in v1.** If you need quest-state or world-flag gating, filter
  the result list in game code, or build per-context table ids (`enc.warrens.normal` vs
  `enc.warrens.post_quest`). Conditions can be added later without breaking callers.
- **The engine doesn't spawn actors.** It hands back a manifest of `(actorId, count)`. The
  host game is responsible for turning that into `BattleActorDefinition`s with the right
  stats / scaling / skills. This keeps the encounter table decoupled from your enemy
  authoring shape.
