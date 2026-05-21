# World variables & conditions

A typed key/value store for arbitrary gameplay flags — quest progress markers, dialogue
state, unlocked content, region clear flags, currency-adjacent counters. Plus a small
composable predicate API for asking questions like "is this quest done **and** has the
player visited the fountain?"

## Variable types

`WorldVariableValue` is a tagged union of four kinds:

```csharp
WorldVariableValue.FromBool(true);
WorldVariableValue.FromInt(42);
WorldVariableValue.FromFloat(0.75);
WorldVariableValue.FromString("crypt");
```

Reading is type-safe (TryGet returns false on a type mismatch):

```csharp
WorldVariableValue v = /* ... */;
if (v.TryGetInt(out int n))   { /* ... */ }
if (v.TryGetBool(out bool b)) { /* ... */ }
```

## Set and read

```csharp
using Moonforge.Core.World;
using Moonforge.Core.World.Commands;
using Moonforge.Core.World.Queries;

dispatcher.Dispatch(gameState, new SetWorldVariableCommand(
    key: "flag.intro_complete",
    value: WorldVariableValue.FromBool(true)), context);

WorldVariableValue? value = new GetWorldVariableQueryHandler()
    .Query(gameState, new GetWorldVariableQuery("flag.intro_complete"));

bool ready = value is not null && value.TryGetBool(out bool b) && b;
```

`SetWorldVariableCommand` overwrites whatever was there (including the type). Reading an
unset key returns `null`.

Direct access on `WorldState` also works when you don't need command semantics:

```csharp
gameState.WorldState.Set("flag.foo", WorldVariableValue.FromInt(7));
int x = gameState.WorldState.GetInt("flag.foo");           // 7
int y = gameState.WorldState.GetInt("flag.missing");       // 0 (default)
```

## Who else writes here

The world store is integration glue — several modules read and write it:

| Source | What it writes |
|---|---|
| Dialogue `SetWorldBool` / `SetWorldInt` / `AddWorldInt` effects | Whatever the choice configures |
| Interactable `SetWorldBool` / `SetWorldInt` effects | On use of an interactable instance |
| Your game code | Anything else — region flags, story beats, counters |

And the readers:

| Reader | What it gates |
|---|---|
| Dialogue `WorldBoolEquals` / `WorldIntAtLeast` / `WorldIntAtMost` conditions | Whether a choice is visible |
| Loot `WorldBoolEquals` / `WorldIntAtLeast` conditions | Whether an entry is eligible |
| `ICondition` composites (below) | Arbitrary boolean expressions |

## Conditions: composable predicates

`ICondition` lets you express boolean rules over `GameState`. Useful for ad-hoc gating
in custom code without writing a one-off predicate every time.

```csharp
using Moonforge.Core.World.Conditions;

// AllCondition = AND across every nested condition. Use AnyCondition for OR.
ICondition entrance = new AllCondition(
    // Required: the intro flag must be set true.
    new WorldVariableEqualsCondition(
        key: "flag.intro_complete",
        expected: WorldVariableValue.FromBool(true)),
    // Required: reputation with the thieves' guild must be >= 50.
    new WorldVariableNumberCondition(
        key: "rep.thieves_guild",
        comparison: NumericComparisonOperator.GreaterThanOrEqual,   // >= 50
        value: 50));

bool canEnter = entrance.Evaluate(gameState);
```

### Building blocks

- **`WorldVariableEqualsCondition(key, expected)`** — exact value match (any kind).
- **`WorldVariableNumberCondition(key, comparison, value)`** — numeric comparison against
  an int or float variable. `comparison` is a `NumericComparisonOperator` (`Equal`,
  `NotEqual`, `LessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`).
- **`AllCondition(c1, c2, …)`** — true when every child is true. Like `&&`.
- **`AnyCondition(c1, c2, …)`** — true when any child is true. Like `||`.

Compose nested logic by mixing `AllCondition` and `AnyCondition`:

```csharp
// (flag.A == true) AND (count >= 3 OR override == true)
ICondition cond = new AllCondition(
    new WorldVariableEqualsCondition("flag.A", WorldVariableValue.FromBool(true)),
    new AnyCondition(
        new WorldVariableNumberCondition("count", NumericComparisonOperator.GreaterThanOrEqual, 3),
        new WorldVariableEqualsCondition("override", WorldVariableValue.FromBool(true))));
```

Custom conditions are a single-method interface:

```csharp
public sealed class HeroAtLevelCondition : ICondition
{
    private readonly int _level;
    public HeroAtLevelCondition(int level) => _level = level;

    public bool Evaluate(GameState gameState)
    {
        return gameState.ProgressionState.TryGet("party.hero", out ActorProgression p)
            && p.Level >= _level;
    }
}
```

`ICondition` is intentionally not surfaced through the dialogue/loot data layer in v1 —
those use enum-discriminated `DialogueConditionDefinition` / `LootConditionDefinition`
so they can be serialized. `ICondition` is for game-code-side gating: locked tile
checks, scripted-encounter triggers, content unlocks.

## Events

| Event | When |
|---|---|
| `WorldVariableChangedEvent` | After `SetWorldVariableCommand` succeeds (also fires for indirect writes from dialogue/interactable effects) |

## Persistence

`WorldState` is part of `GameStateSnapshot` — variables of all four kinds round-trip
through `JsonGameStateSerializer`. Keys you set today survive across saves.

## Patterns

### One-time triggers

```csharp
const string fired = "trigger.intro.fired";
if (!gameState.WorldState.GetBool(fired))
{
    dispatcher.Dispatch(gameState, new SetWorldVariableCommand(fired, WorldVariableValue.FromBool(true)), ctx);
    // ... fire the trigger (cinematic, dialogue, etc.)
}
```

### Counters

```csharp
// Track how many times the fountain was used:
int touches = gameState.WorldState.GetInt("town.fountain.touched");
dispatcher.Dispatch(gameState, new SetWorldVariableCommand(
    "town.fountain.touched",
    WorldVariableValue.FromInt(touches + 1)), context);
```

Dialogue's `AddWorldInt` effect does this without a read-modify-write dance — prefer it
when the increment is part of a dialogue choice.

### Naming convention

The sample uses dot-segmented keys like `flag.guard.briefed`, `dungeon.deepest_floor`,
`town.cache.looted`. This isn't enforced by the engine, but consistency makes filtering
and debug-dumping easier.

## See also

- [Dialogue](dialogue.md) — `SetWorld*` effects and `World*` conditions
- [Loot](loot.md) — `WorldBoolEquals` / `WorldIntAtLeast` conditional drops
- [Interactables](interactables.md) — `SetWorldBool` / `SetWorldInt` effect kinds
