# Evolution

Level-up and item-driven evolution triggers. The engine detects when an actor's evolution
conditions are met and emits an `EvolutionTriggeredEvent`; the **game** applies the
outcome (stat changes, type changes, skill changes, display-name swap, species rebind).

This split is intentional. The engine doesn't have a built-in "monster species" concept;
content shapes vary too much per game to make a one-size-fits-all evolution outcome.
Detection and emission are the genuinely reusable infrastructure — actually mutating the
actor is content-specific glue you keep in your game code.

## Define an evolution

```csharp
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Evolution;

definitions.AddEvolution(new EvolutionDefinition(
    id: "evolution.caterpie_to_metapod",
    trigger: EvolutionTrigger.LevelUp,        // auto-fires on level-up (vs. Manual)
    requiredLevel: 7,                          // fires once the actor reaches level 7+
    displayName: "Caterpie → Metapod",
    evolvedSpeciesId: "species.metapod"));     // optional game-meaningful tag, passed through verbatim
```

`EvolvedSpeciesId` is a string the engine carries through to the event verbatim — the
engine never interprets it. Use it to route your game's reactor to the right species
template.

`EvolutionTrigger`:

- **`LevelUp`** — auto-fires when the actor crosses `RequiredLevel` via a
  `LevelUpEvent`. Set `RequiredLevel` to 2 or higher (1 is the starting level, so it would
  never fire).
- **`Manual`** — fires only when the game dispatches `TriggerEvolutionCommand`. Use this
  for item-driven evolutions ("Eevee + Fire Stone → Flareon"), trade evolutions, scripted
  story events.

## Register per-actor eligibility

An actor only auto-evolves through definitions the game explicitly assigns to it. This is
how a Caterpie's actor instance knows to watch `caterpie-to-metapod` but not
`charmander-to-charmeleon`.

```csharp
using Moonforge.Core.Evolution.Commands;

dispatcher.Dispatch(gameState, new ConfigureActorEvolutionsCommand(
    actorId: "party.starter.001",
    // This actor is now eligible only for these evolutions; replaces any prior list.
    evolutionIds: new[] { "evolution.caterpie_to_metapod" }), context);
```

Replaces any prior list. Pass an empty array to clear all eligibility.

The natural place to call this is right after `AddPartyMemberCommand` or
`BattleActorCapturedEvent` — anywhere a new actor enters long-term play.

## Auto-trigger on level-up

When `GrantExperienceCommand` causes a `LevelUpEvent`, the built-in
`LevelUpEvolutionReactor` checks the actor's eligibility list and fires
`EvolutionTriggeredEvent` for each registered evolution whose `RequiredLevel ≤ ToLevel`.
Multi-level jumps fire each qualifying evolution exactly once (the reactor uses the
event's `FromLevel`/`ToLevel` window to avoid double-firing).

## Manual trigger

For item-driven evolutions, dispatch directly:

```csharp
dispatcher.Dispatch(gameState, new TriggerEvolutionCommand(
    actorId: "party.eevee.001",                 // must already be registered for this evolution
    evolutionId: "evolution.eevee_fire_stone"), context);
// → fires EvolutionTriggeredEvent; your game's reactor applies the outcome
```

The handler still validates that the actor is registered for that evolution and (for
`LevelUp` triggers) that the level requirement is met.

## React to the event in your game

```csharp
using Moonforge.Core.Evolution.Events;

foreach (DomainEvent ev in _sink.DrainNewEvents())
{
    if (ev is EvolutionTriggeredEvent evo)
    {
        // 1. Look up the new species in your content data.
        // 2. Update your per-actor display name / type / skill list.
        // 3. Optionally apply new base stats via ActorStatsState.SetBase or
        //    ApplyStatModifierCommand.
        // 4. Rescale current HP proportionally to the new MaxHp.
        ApplyEvolutionInGameContent(evo.ActorId, evo.EvolvedSpeciesId);
    }
}
```

`samples/Moonforge.Sample.MonsterCatcher.Console/GameLoop/MonsterCatcherGame.cs` has a
complete worked version: `ApplyEvolution(actorId, newSpeciesId)` re-derives moves from the
new species' starting kit, scales HP proportionally, and updates the per-actor display
name dictionary.

## Failure modes

`TriggerEvolutionCommand` returns errors for:

| Error | Why |
|---|---|
| `ValidationFailed` | Empty actor id or evolution id |
| `UnsupportedOperation` | Actor isn't registered for that evolution |
| `NotFound` | Evolution id isn't in the catalog, or LevelUp trigger and actor has no progression |
| `Conflict` | LevelUp trigger and actor's level is below `RequiredLevel` |

## Events

| Event | Fires when |
|---|---|
| `EvolutionTriggeredEvent` | A level-up or manual trigger lands |

The event carries `ActorId`, `EvolutionId`, `Trigger` (LevelUp/Manual), and the
optional `EvolvedSpeciesId` tag.

## Persistence

`EvolutionState` round-trips through `EvolutionStateSnapshot` (per-actor lists of eligible
evolution ids). Schema bumped to v5 when this module landed.

## See also

- [Progression](progression.md) — the `LevelUpEvent` that drives auto-evolution
- [Party](party.md) — typical lifecycle calls `ConfigureActorEvolutionsCommand` when
  a member joins
- [Combat](combat.md) — capturing a wild monster is a common moment to register that
  actor's evolution path
