# Troubleshooting

Common pitfalls, gotchas, and how to diagnose them.

## "My command returns failure but I can't tell why"

`DomainResult.Error` carries both a `Code` (machine-readable) and a `Message` (human).
Always inspect both:

```csharp
DomainResult r = dispatcher.Dispatch(gameState, command, context);
if (!r.IsSuccess)
{
    // On failure, Error is non-null: Code is the machine-readable DomainErrorCode,
    // Message is the human-readable explanation.
    Console.WriteLine($"{r.Error!.Code}: {r.Error!.Message}");
}
```

`DomainErrorCode.InternalError` means the handler threw an exception — the message
contains the exception's message. Add structured logging in your host before treating it
as user-facing copy.

---

## "My handler/reactor isn't being called"

Did you register it on the dispatcher?

```csharp
CommandDispatcher dispatcher = DefaultCommandDispatcher.Create();
// ↑ this wires every built-in handler and reactor.

dispatcher.Register(new MyCustomCommandHandler());   // add a handler for your own ICommand
dispatcher.RegisterReactor(new MyCustomReactor());   // add a cross-module reactor
```

If you build a dispatcher manually (`new CommandDispatcher()` without using
`DefaultCommandDispatcher`), you have to register every handler you want available.
Look at `DefaultCommandDispatcher.RegisterBuiltIns` for the canonical list.

A reactor that swallows events without doing anything will return success silently —
add a log line or a breakpoint inside `React` to confirm it's firing.

---

## "Derived stats return zero"

The default `NoOpFormulaEvaluator` returns 0 for every expression. Wire a real evaluator
into your `CommandContext`:

```csharp
CommandContext context = new(
    randomSource,
    clock,
    formulaEvaluator: new MyExpressionEvaluator(),   // ← here
    eventSink,
    definitions);
```

The engine ships `ExpressionFormulaEvaluator` in `Moonforge.Core.Runtime.Formulas` — a
small recursive-descent parser supporting `+ - * / ( )` and identifiers. Drop it in if
you don't need anything fancier.

Also: derived stats need the stat to be **registered** with a `DerivedFromFormula`. An
unregistered stat is a stored value with no formula — the evaluator is never consulted.

---

## "Events I expected don't appear in the sink"

Events buffered inside a command only flush to the host's sink **after the command and
all its reactors succeed**. Common causes for missing events:

- **The command failed.** Buffered events are discarded on rollback. Check the
  `DomainResult`.
- **A reactor returned failure.** Same outcome — even the originating command's events
  are discarded.
- **You're reading from a different sink.** The `CommandContext.EventSink` you passed in
  is the one the engine writes to. Make sure your UI loop drains *that* sink.
- **You drained too early.** `DrainNewEvents()` returns events since the last drain. If
  another part of your code already drained them, they're gone.

To see all events flowing, attach a debug observer:

```csharp
// A decorator sink: logs every event, then forwards it to the real sink so
// nothing downstream changes. Wrap your host's sink with this to trace the flow.
public sealed class LoggingSink : IDomainEventSink
{
    private readonly IDomainEventSink _inner;
    public LoggingSink(IDomainEventSink inner) => _inner = inner;
    public void Publish(DomainEvent ev)
    {
        Console.WriteLine($"[{ev.Name}] {ev}");
        _inner.Publish(ev);   // forward unchanged
    }
}
```

---

## "Quest objectives don't auto-complete"

Three things to check:

1. **`QuestObjectiveTrackingReactor` is registered.** `DefaultCommandDispatcher.Create()`
   registers it automatically; a manual dispatcher must too.
2. **`QuestDefinition.AutoTrack` is true.** When false, the reactor ignores the quest and
   you must emit signals manually via `EmitQuestSignalCommand`.
3. **The signal `targetId` matches.** A `Kill` objective with `targetId = "enemy.wolf"`
   only advances on a `QuestSignalEvent(Kill, "enemy.wolf", n)`. If your enemy IDs include
   battle suffixes (`enemy.wolf.battle.1.2`), match the *template* id in the objective or
   emit a normalized signal.

For composites: each child must satisfy its `requiredCount` independently. A
`CompositeAnd` reports `Completed` only when **every** child is at or above its required
count.

---

## "My save file won't load after I changed a field"

You changed a persisted shape without bumping the schema and writing a migration.
Two paths:

- **The change is backwards-compatible** (added a new field with a default, or removed a
  field that's now ignored). Old saves load fine. No migration needed.
- **The change is incompatible** (renamed a field, restructured a collection, changed a
  type). Old saves silently lose data on load.

Bump `GameStateSnapshotMapper.CurrentSchemaVersion` and add an `ISaveMigration` *before*
shipping a release that changes persisted shapes. See [persistence.md](persistence.md).

In dev, you can usually just delete the save and start fresh.

---

## "Shop never restocks"

The simulation clock has to advance. The engine doesn't tick time on its own.

```csharp
// Advance the simulated clock yourself — the engine never ticks time on its own.
gameState.SimulationMinutes += 30;
```

Restocks fire **lazily** — they're checked when a `BuyFromShopCommand` or
`SellToShopCommand` runs. So bumping `SimulationMinutes` and then immediately checking
`GetShopStockQuery` won't show the refill; it shows up after the next purchase attempt.

If your design has no notion of time (pure roguelike), set
`restockIntervalMinutes: 0` and the shop is effectively always full.

---

## "RNG outputs differ between runs with the same seed"

Determinism breaks when something other than `IRandomSource` introduces variation. Audit
your handlers and reactors for:

- **`System.Random`** anywhere. Always use the `IRandomSource` from `CommandContext`.
- **`DateTime.Now`, `Stopwatch`, `Environment.TickCount`** — replace with `IGameClock`.
- **`Guid.NewGuid()`** — derive ids from a counter or seeded RNG.
- **Unordered iteration that affects output.** `Dictionary` enumeration is
  implementation-defined; sort the keys if the order matters.
- **Hash-based ordering.** `HashSet<T>` enumeration order can vary by runtime version.
- **Thread races.** The engine is single-threaded by design — don't introduce parallelism
  in handlers.

Even one of these breaks all reproducibility (replay, save resilience, deterministic
multiplayer). The fastest path to diagnosis: bisect — comment out modules and seed-based
tests until the deviation disappears.

---

## "I get `Conflict: Another battle is currently active`"

`StartBattleCommand` rejects when `GameState.ActiveBattle` is non-null and in `Active`
status. Either:

- **The previous battle ended in `Victory`/`Defeat` but you didn't clear it.** The engine
  doesn't auto-clear `ActiveBattle`; your scene-transition code should set it to `null`
  after rendering the summary.
- **You crashed mid-battle and reloaded.** Saves don't persist `ActiveBattle`, but if you
  have your own wrapper that does, set it to null on load.

---

## "My migration runs in an infinite loop"

The migration pipeline keeps applying migrations until the payload reaches
`CurrentSchemaVersion`. If your migration forgets to rewrite `schemaVersion`, the
pipeline detects the version didn't advance and breaks out — but you'll have wasted
cycles. Always finish a migration with:

```csharp
// Bump schemaVersion so the pipeline advances; forgetting this stalls it at FromVersion.
return payload.Replace($"\"schemaVersion\":{FromVersion}", $"\"schemaVersion\":{FromVersion + 1}");
```

(Or the equivalent JSON parse → mutate → serialize round trip.)

---

## "Equipment bonuses don't apply"

`EquipItemCommand` writes `StatModifier`s into the actor's stat block with
`SourceKind = "equipment"`. If `block.Get("atk", …)` doesn't reflect the bonus, check:

- **Is the right actor id being read?** The default `actorId` for `EquipItemCommand` is
  `"player"`; the sample uses `"party.hero"`. Misalignment means modifiers land on
  one actor and you query the other.
- **Was the item registered?** `EquipItemCommand` reads the `EquipmentDefinition` for
  the slot mapping and bonus list. An unregistered item won't equip (returns `NotFound`).
- **Is the formula evaluator running?** Derived stats need a real
  `IFormulaEvaluator` (see above).

For a sanity check, dump the actor's modifier list:

```csharp
// GetOrCreate returns the actor's stat block (creating an empty one if absent).
StatBlock block = gameState.ActorStatsState.GetOrCreate("party.hero");
foreach (StatModifier m in block.Modifiers)
{
    // Equipment bonuses show up with SourceKind == "equipment".
    Console.WriteLine($"{m.StatId} {m.Bucket} {m.Value} ({m.SourceKind}|{m.SourceId})");
}
```

---

## "Status effects don't deal damage / aren't ticking"

Status effects tick at the start of the affected actor's turn — not on every
`BattleTurnAdvancedEvent`. If a non-affected actor is currently up, no tick fires.
This is by design (turn-based combat).

`tickHpDelta` is what causes the HP swing; `statModifiers` doesn't tick. If you defined
a status with `statModifiers` and expect HP changes, you want `tickHpDelta` instead.

`preventsAction: true` doesn't reduce HP — it just skips the actor's turn, firing
`StatusPreventedActionEvent`.

---

## "I get warnings about CRLF / LF"

Cosmetic — Windows git's `autocrlf` setting converts line endings on checkin/checkout.
Not a functional problem. If they're noisy, add a `.gitattributes` declaring text files:

```
* text=auto
*.cs text eol=lf
*.csproj text eol=lf
```

---

## "`dotnet pack` fails about a missing nuget-readme.md"

`src/Moonforge.Core/Moonforge.Core.csproj` references `docs/nuget-readme.md` as the
package readme. Make sure the file exists at the expected relative path. If you removed
it, drop the `<None Include=... PackageReadmeFile />` block from the csproj or restore
the file.

---

## Still stuck?

- Open an issue with the engine version (or commit SHA), a minimal repro, and the
  expected vs. observed behavior.
- For security issues, see [SECURITY.md](../SECURITY.md).
