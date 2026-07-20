# Interactables

Interactables are world objects placed on the exploration grid that respond to a "use" verb:
chests, doors, levers, signs, pickups, fountains. They sit alongside actors on the map but
carry their own state and a list of declarative effects that run when used.

## Two layers: definition + instance

- **`InteractableDefinition`** (data, registered in the catalog) — describes a kind of
  object: what effects fire on use, whether it blocks movement / line-of-sight, how many
  uses it has, whether it starts locked, what key item unlocks it.
- **`InteractableInstance`** (mutable state, lives in `GameState.InteractablesState`) — one
  placed object: position, current status, uses remaining, runtime lock state.

The split lets you spawn multiple chests from the same definition without duplicating data,
and lets the runtime modify per-instance state independently of the catalog.

## Defining

```csharp
catalog.AddInteractable(new InteractableDefinition(
    "interactable.chest.iron",
    effects: [new InteractableEffectDefinition(InteractableEffectKind.GrantLootTable, "loot.chest.iron")], // on use, rolls + grants this loot table
    maxUses: 1,                          // single use, then status becomes Consumed (-1 = unlimited)
    startsLocked: true,                  // instance spawns Locked
    requiredKeyItemId: "item.iron_key",  // key item needed to unlock
    consumeKeyOnUnlock: true,            // remove the key from inventory once it unlocks
    blocksMovement: true));              // occupies its tile for pathing
```

## Effect kinds (v1)

| Kind | `TargetId` | Other fields | Effect |
|---|---|---|---|
| `GrantLootTable` | loot table id | — | Rolls and atomically grants the loot table |
| `SetWorldBool` | world variable key | `BoolValue` | Sets the variable |
| `SetWorldInt` | world variable key | `IntValue` | Sets the variable |
| `ChangeInteractableStatus` | target instance id | `IntValue` = `(int)InteractableStatus` | Flips another interactable's status |
| `UnlockInteractable` | target instance id | — | Marks target as unlocked |
| `LockInteractable` | target instance id | — | Marks target as locked |
| `EmitInteractionSignal` | signal key | — | Publishes `InteractionSignalEvent` |

Effects run in declaration order. Any failure aborts the interaction and rolls back state
via the dispatcher's atomic guarantee — no partial application.

## Placing and removing

```csharp
dispatcher.Register(new PlaceInteractableCommandHandler());
dispatcher.Dispatch(gameState, new PlaceInteractableCommand(
    instanceId: "town.chest.cache",          // unique id for this placed instance
    definitionId: "interactable.chest.iron", // which catalog definition to spawn from
    position: new GridPosition(12, 4)), context);
```

`RemoveInteractableCommand` deletes one instance.

## Interacting

```csharp
dispatcher.Register(new InteractWithCommandHandler());
dispatcher.Dispatch(gameState, new InteractWithCommand(
    actorId: "party.hero",            // actor must be adjacent (Manhattan distance ≤ 1)
    instanceId: "town.chest.cache"), context);
```

Resolution order:
1. Validate the instance and definition exist.
2. Reject `Consumed` and `Broken` instances.
3. Verify the actor is **adjacent** (Manhattan distance ≤ 1, including same tile).
4. If locked: try to unlock with the actor's key. If the actor lacks the key, emit
   `InteractableLockedEvent` and return Success without applying effects — UI listens for
   the event to prompt the player. Successful unlock consumes the key (if configured),
   transitions to `Unlocked`, and emits `InteractableStatusChangedEvent`.
5. Run effects in order.
6. Decrement `UsesRemaining`. When it hits 0 the status becomes `Consumed`; otherwise a
   `Default` status auto-transitions to `Opened`.
7. Emit `InteractableInteractedEvent` and any status-change / consumed events.

## Lock model

- **Failure mode**: locked-without-key returns `Success` with `InteractableLockedEvent` —
  no state change occurs but the event tells callers what happened. Hard errors (e.g.,
  inventory consume failing) propagate as failures and roll back.
- **Unlock-by-effect**: another interactable can call `UnlockInteractable` to open a target
  without any key, useful for lever-opens-vault scenarios.

## Lever → door chain (compound interaction)

```csharp
catalog.AddInteractable(new InteractableDefinition(
    "interactable.door.iron",
    maxUses: -1,                         // door can be re-checked indefinitely
    blocksMovement: true));

catalog.AddInteractable(new InteractableDefinition(
    "interactable.lever.vault",
    effects:                             // effects run in declaration order; any failure rolls back the whole interaction
    [
        // 1. Unlock the door instance "door.vault" without needing a key.
        new InteractableEffectDefinition(InteractableEffectKind.UnlockInteractable, "door.vault"),
        // 2. Force the door's status to Opened (IntValue carries the (int)InteractableStatus enum).
        new InteractableEffectDefinition(InteractableEffectKind.ChangeInteractableStatus,
            "door.vault",
            intValue: (int)InteractableStatus.Opened),
        // 3. Publish InteractionSignalEvent("vault.opened") for game code to react to.
        new InteractableEffectDefinition(InteractableEffectKind.EmitInteractionSignal, "vault.opened")
    ],
    maxUses: 1));
```

## Querying

- `GetInteractableAtQuery(GridPosition)` — find the instance at a tile (one per tile in v1).
- `ListInteractablesQuery` — enumerate all (for rendering, save inspection).

## Determinism & persistence

Random behavior (e.g. loot table rolls) flows through the same `IRandomSource` the rest of
the engine uses, so identical seeds reproduce identical outcomes. Instance state is captured
in `InteractablesStateSnapshot` and round-trips through the standard JSON serializer
(schema version bumped to **3**; older saves load with an empty interactables state).

## Known limitations

- **One interactable per tile** in v1 (no multi-stack). Multi-tile interactables (a 2×1
  door) are a separate concern.
- **No entry conditions** on effects yet — every effect on a triggered interactable fires.
  Game code can branch by calling `EmitInteractionSignal` and reacting to it externally.
- **No effects for HP / MP restore, time advance, or starting dialogue.** Use
  `EmitInteractionSignal` until these effect kinds land.
