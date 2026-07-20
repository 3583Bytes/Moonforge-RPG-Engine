# Party

Roster state for multi-character / multi-monster games. Tracks which actors are in the
player's party, how many are "active" (on the field) vs. on the bench, and synchronises
with combat when an actor is swapped in or captured.

The engine doesn't store the actors' stats here — `ActorStatsState` and `ProgressionState`
own those. `PartyState` is **only** the membership list and active/reserve flags.

## Configuration

A fresh `PartyState` defaults to `MaxActive = 1, MaxRoster = 1` so games that never call
the party API behave exactly like the pre-Party single-hero engine. To opt in:

```csharp
using Moonforge.Core.Party.Commands;

// maxActive = on-field slots; maxRoster = total roster cap (active + bench).
// FF-style: 4 active, up to 12 in roster.
dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 4, maxRoster: 12), context);

// Pokemon-style: 1 active, up to 6 in roster.
dispatcher.Dispatch(gameState, new ConfigurePartyCommand(maxActive: 1, maxRoster: 6), context);
```

## Add / remove members

```csharp
// Active = goes on the field immediately (counts against MaxActive).
dispatcher.Dispatch(gameState, new AddPartyMemberCommand("party.hero", active: true), context);

// Reserve = sits on the bench.
dispatcher.Dispatch(gameState, new AddPartyMemberCommand("party.cleric", active: false), context);

// Remove (e.g. release, retire, story-driven loss).
dispatcher.Dispatch(gameState, new RemovePartyMemberCommand("party.cleric"), context);
```

Validation rules:

- Adding fails if the actor is already in the party.
- Adding active fails if all `MaxActive` slots are taken.
- Adding any fails if the roster is at `MaxRoster`.
- Removing a missing actor fails with `NotFound`.

## Switching active and reserve

Out of battle, move a roster member between active and bench:

```csharp
dispatcher.Dispatch(gameState, new SetPartyMemberActiveCommand("party.cleric", active: true), context);
```

`MaxActive` is enforced — promoting fails when all active slots are full. Bench someone
first.

## Querying the roster

```csharp
GetPartyMembersQueryHandler handler = new();

// Everyone.
IReadOnlyList<PartyMember> all = handler.Query(gameState, new GetPartyMembersQuery());

// Active only — the "who's on the field" list.
IReadOnlyList<PartyMember> active = handler.Query(gameState, new GetPartyMembersQuery(activeOnly: true));
```

Each `PartyMember` carries `ActorId` and `IsActive`. The list preserves insertion order so
display ordering is stable across sessions.

## Mid-battle swap

The Combat module owns `SwapBattleActorCommand`; the Party module owns the reactor that
syncs `IsActive` flags when a swap fires. The split keeps Combat decoupled from Party —
a swap event fires whether or not the actors are tracked in `PartyState`.

```csharp
using Moonforge.Core.Combat.Commands;

// During a battle, swap the current-turn party actor for a bench actor.
BattleActorDefinition incoming = BuildActorDefinitionFor("party.cleric");
dispatcher.Dispatch(gameState, new SwapBattleActorCommand(
    outActorId: "party.hero",
    inActor: incoming), context);
```

Engine rules:

- Outgoing actor must be the current-turn actor (swap consumes their turn — Pokemon shape).
- Outgoing actor must be on the Party faction.
- Incoming actor must be on the Party faction and not already in the battle.
- All incoming actor skills must be in the battle's skill registry.

The built-in `PartyActiveSyncReactor` watches `BattleActorSwappedEvent` and flips the
`IsActive` flag on each side of the swap that's in the party (silently no-ops for actors
that aren't, so wild-vs-wild scripted swaps don't pollute state).

## Capture interplay

When `AttemptCaptureCommand` succeeds, the `BattleActorCapturedEvent` fires and the
`PartyCaptureReactor` adds the captured actor to the roster as a reserve member. If the
party is full at that moment the reactor returns failure, **rolling back the entire
capture** (RNG, removal from battle, everything) — check roster room before letting the
player try.

```csharp
// Guard up front: a full roster would make PartyCaptureReactor roll back the capture.
if (gameState.PartyState.Members.Count >= gameState.PartyState.MaxRoster)
{
    // Tell the player: "Your roster is full — release a monster first."
    return;
}

dispatcher.Dispatch(gameState, new AttemptCaptureCommand(
    actorId: "party.hero",            // the actor making the capture attempt
    targetActorId: "wild.pidgey.001", // the wild actor being captured
    bonusPercent: 150 /* ball-quality multiplier; 100 = no bonus */), context);
// → on success: BattleActorCapturedEvent fires, PartyCaptureReactor adds it as a reserve member
```

See [Combat → Capture](combat.md) for the capture math and full flow.

## Events

| Event | Fires when |
|---|---|
| `PartyConfiguredEvent` | `ConfigurePartyCommand` sets new caps |
| `PartyMemberAddedEvent` | `AddPartyMemberCommand` succeeds |
| `PartyMemberRemovedEvent` | `RemovePartyMemberCommand` succeeds |
| `PartyMemberActiveChangedEvent` | `SetPartyMemberActiveCommand` flips a flag (no-op self-set doesn't fire) |
| `BattleActorSwappedEvent` *(Combat)* | A mid-battle swap resolved — `PartyActiveSyncReactor` watches this |
| `BattleActorCapturedEvent` *(Combat)* | A capture succeeded — `PartyCaptureReactor` watches this |

## Persistence

`PartyState` round-trips through `PartyStateSnapshot` in the engine's JSON save format.
Schema bumped to v4 when this module landed; games loading older saves get an empty
party by default (the `Party` field is absent → deserializer uses `new()`).

## See also

- [Combat](combat.md) — battle commands, mid-battle swap, capture
- [Evolution](evolution.md) — auto-fires on level-up for actors in the party (or anywhere)
- [Bestiary](bestiary.md) — tracks the species a captured actor brings into the party
