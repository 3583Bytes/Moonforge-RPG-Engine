# Bestiary

A "Pokédex"-style codex: per-species encounter and capture history. Auto-fills when
battles start and when captures resolve; games can also mark entries manually for
scripted reveals or starter gifts.

The bestiary is **opt-in per actor**. Actors without a `SpeciesId` are silently ignored,
so games that don't care about a codex pay zero cost — the module only does work for
species you explicitly tag.

## Tagging actors with a species

Set `SpeciesId` on `BattleActorDefinition` when you build a combatant:

```csharp
using Moonforge.Core.Combat;

new BattleActorDefinition(
    actorId: "wild.pidgey.001",
    displayName: "Pidgey",
    faction: CombatFaction.Enemy,
    maxHp: 30, atk: 5, def: 3, matk: 3, mdef: 2,
    initiative: 10,
    skillIds: new[] { "move.tackle", "move.gust" },
    speciesId: "species.pidgey",       // ← this is the bestiary key
    captureBaseRate: 50);
```

Multiple actor instances can (and should) share a `SpeciesId`. The bestiary keys on the
species, not the per-instance actor id, so `wild.pidgey.001` and `wild.pidgey.002` roll up
into the same entry.

## Auto-tracking

The built-in `BestiaryAutoTrackReactor` listens to two events:

- **`BattleStartedEvent`** — iterates every enemy actor with a non-null `SpeciesId` and
  marks them encountered. First-time species fire `SpeciesFirstEncounteredEvent`.
- **`BattleActorCapturedEvent`** — marks the captured species both *captured* and
  *encountered* (a sneak-capture without a prior battle still counts as encountered).
  First-time captures fire `SpeciesFirstCapturedEvent`.

No setup beyond `DefaultCommandDispatcher.Create()` — both reactors are wired by default.

## Manual marks

For scripted reveals (a Mew flies overhead, a starter gift, a debug "complete the dex"
command):

```csharp
using Moonforge.Core.Bestiary.Commands;

// You've seen a Mew but never caught one.
dispatcher.Dispatch(gameState, new MarkSpeciesObservedCommand(
    speciesId: "species.mew",
    encountered: true,
    captured: false), context);

// Starter gift — you now have one without a battle.
dispatcher.Dispatch(gameState, new MarkSpeciesObservedCommand(
    speciesId: "species.charmander",
    encountered: true,
    captured: true), context);
```

At least one of `Encountered` or `Captured` must be true — the command fails
`ValidationFailed` otherwise.

## Reading the bestiary

```csharp
using Moonforge.Core.Bestiary.Queries;

GetBestiaryEntryQueryHandler handler = new();
BestiaryEntry? entry = handler.Query(gameState, new GetBestiaryEntryQuery("species.pidgey"));

if (entry is not null)
{
    Console.WriteLine($"Pidgey: encountered {entry.EncounterCount}× (first at minute {entry.FirstEncounteredAtMinutes}); " +
                      $"captured {entry.CaptureCount}× (first at minute {entry.FirstCapturedAtMinutes})");
}

// Aggregate counts:
int seen   = gameState.BestiaryState.EncounteredSpeciesCount;
int caught = gameState.BestiaryState.CapturedSpeciesCount;
```

Each `BestiaryEntry` tracks:

- `EncounterCount` / `CaptureCount` — running totals (every battle that includes the
  species adds one to encounter, every capture adds one to capture).
- `FirstEncounteredAtMinutes` / `FirstCapturedAtMinutes` — `IGameClock.CurrentSimulationMinutes`
  at the moment of the first event. Null until the species has been encountered/captured
  at least once.

## Events

| Event | Fires when |
|---|---|
| `SpeciesFirstEncounteredEvent` | The first time a species is seen (battle start with a tagged enemy, or first manual `encountered: true` mark, or first capture without a prior encounter) |
| `SpeciesFirstCapturedEvent` | The first time a species is caught (or first manual `captured: true` mark) |

Subsequent encounters/captures of the same species only bump the counts — no event
fires.

## Persistence

`BestiaryState` round-trips through `BestiaryStateSnapshot`. Schema bumped to v6 when
this module landed.

## See also

- [Combat](combat.md) — set `SpeciesId` on `BattleActorDefinition`; capture flow fires
  the events the auto-track reactor watches
- [Party](party.md) — captured actors join the party and contribute their species to
  the bestiary in one transaction
