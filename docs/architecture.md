# Architecture

Moonforge is built around three load-bearing ideas:

1. **One aggregate**, `GameState`, owns all mutable gameplay state.
2. **All mutation flows through commands**; all reads flow through queries.
3. **Cross-module integration happens via events + reactors**, never direct calls.

The rest of this document explains what each of those means in practice and why it matters.

## The aggregate: `GameState`

`src/Moonforge.Core/GameState.cs` is the single owner of game state. Every module hangs its
mutable state off `GameState` as a property:

```csharp
public sealed class GameState
{
    public CurrencyWallet CurrencyWallet { get; } = new();
    public InventoryBag   InventoryBag   { get; } = new();
    public QuestState     QuestState     { get; } = new();
    public DialogueState  DialogueState  { get; } = new();
    public BattleState?   ActiveBattle   { get; set; }
    public ShopState      ShopState      { get; } = new();
    public WorldState     WorldState     { get; } = new();
    public ExplorationState   ExplorationState   { get; } = new();
    public EquipmentState     EquipmentState     { get; } = new();
    public ProgressionState   ProgressionState   { get; } = new();
    public ActorStatsState    ActorStatsState    { get; } = new();
    public InteractablesState InteractablesState { get; } = new();
    // ... plus SchemaVersion, ContentVersion, SimulationMinutes
}
```

Two methods make the transactional pipeline possible:

- **`Clone()`** — produces a deep copy of every sub-state. Used to snapshot before a command.
- **`RestoreFrom(snapshot)`** — copies sub-states back from a snapshot. Used to roll back.

## The command pipeline

`CommandDispatcher.Dispatch<TCommand>` runs every command through the same five-step
transaction (`src/Moonforge.Core/Runtime/Commands/CommandDispatcher.cs`):

```
1. snapshot   = gameState.Clone()
2. eventSink' = new BufferedDomainEventSink()         (events hidden during the txn)
3. handler.Handle(gameState, command, ctx')           (may throw)
4. for each buffered event:
       for each reactor:
           reactor.React(gameState, event, ctx')      (any failure → rollback)
5. on success: flush buffered events to the *original* sink
   on failure or exception: gameState.RestoreFrom(snapshot)
```

This guarantees four properties:

- **Atomic.** Failed commands leave no partial state behind.
- **Isolated.** External observers (the host's event sink) never see in-progress state.
- **Composable.** Reactors participate in the same transaction the handler runs in.
- **Catastrophe-safe.** A thrown exception in a handler restores state and returns an error
  result rather than propagating.

### What goes in a command vs. a query

| | Command | Query |
|---|---|---|
| Mutates state? | Yes | No |
| Returns | `DomainResult` (success/failure + error) | Domain value |
| Publishes events? | Yes (through `eventSink`) | No |
| Examples | `AddInventoryItemCommand`, `StartBattleCommand` | `GetCurrencyBalanceQuery`, `GetStatQuery` |

## Reactors: how modules talk

A reactor (`IDomainEventReactor`) gets a chance to mutate `GameState` in response to each
event published during a command. The canonical example is
`QuestObjectiveTrackingReactor`:

```csharp
public sealed class QuestObjectiveTrackingReactor : IDomainEventReactor
{
    public DomainResult React(GameState gameState, DomainEvent ev, CommandContext ctx)
    {
        switch (ev)
        {
            case QuestSignalEvent signal:
                return ApplySignal(gameState, signal.SignalType, signal.TargetId, signal.Amount, ctx);
            case InventoryItemChangedEvent inv when inv.Delta > 0:
                return ApplySignal(gameState, QuestSignalType.Collect, inv.ItemId, inv.Delta, ctx);
            default:
                return DomainResult.Success();
        }
    }
}
```

This is how "picking up 3 herbs" completes a `Collect` quest objective without the inventory
module knowing the quest system exists. Reactors are the engine's only cross-module coupling
mechanism — handlers should never directly invoke another module's commands.

Reactors run **inside the same transaction** as the originating command. A reactor returning
failure rolls back everything, including the command's own state changes.

## The `CommandContext`

The context carries every non-`GameState` input a handler needs:

```csharp
public sealed class CommandContext
{
    public IRandomSource         RandomSource     { get; }
    public IGameClock            Clock            { get; }
    public IFormulaEvaluator     FormulaEvaluator { get; }
    public IDomainEventSink      EventSink        { get; }
    public IGameDefinitionCatalog Definitions     { get; }
}
```

Each one is an interface so the engine never depends on `System.Random`, `DateTime.Now`,
or any other ambient state. This is the **determinism contract**: handed identical inputs
(seed, clock, definitions, current `GameState`), every command produces the same
`GameState`, the same events, and the same `DomainResult`.

### Why determinism matters

- **Reproducibility.** A bug report with the seed and inputs is enough to replay the
  exact failure.
- **Save resilience.** Replaying a sequence of commands from the same starting state always
  produces the same end state.
- **Multiplayer.** Lockstep / deterministic-rollback netcodes are viable.
- **Testing.** Tests can assert on outputs without flaking.

Anything that breaks determinism breaks all of these. The rules:

- Don't use `System.Random` — use `IRandomSource`.
- Don't read `DateTime.Now` / wall-clock time — use `IGameClock`.
- Don't iterate dictionaries in a way that affects output — sort first, or use stable
  enumeration.
- Don't depend on hash codes for ordering.

## Definitions: separating content from state

`IGameDefinitionCatalog` holds the **immutable, designer-authored** data: item definitions,
quest definitions, shop catalogs, loot tables, damage types, etc.

```csharp
InMemoryGameDefinitionCatalog definitions = new InMemoryGameDefinitionCatalog()
    .AddItem(new ItemDefinition("item.potion", maxStack: 10))
    .AddQuest(new QuestDefinition(...))
    .AddDamageType(new DamageTypeDefinition(...))
    .AddLootTable(new LootTableDefinition(...));
```

This split is load-bearing for two reasons:

- **Persistence** only saves `GameState` (the *runtime* state). Definitions are re-supplied
  on boot from the host game's content, so save files stay small and content can be
  rebalanced without invalidating saves.
- **Determinism** depends on definitions being immutable during a session. The engine assumes
  no `DamageTypeDefinition` will change between turns.

`EmptyGameDefinitionCatalog.Instance` is the default — useful for tests, but most real games
construct an `InMemoryGameDefinitionCatalog`.

## Module layout

Every gameplay module follows the same shape:

```
src/Moonforge.Core/<Module>/
├── <ModuleState>.cs        ← hangs off GameState
├── Commands/
│   ├── <Command>.cs
│   └── <Command>Handler.cs
├── Events/
│   └── <Event>.cs
└── Queries/
    ├── <Query>.cs
    └── <Query>Handler.cs
```

Modules ship today: `Combat`, `Crafting`, `Dialogue`, `Economy`, `Encounters`, `Equipment`,
`Exploration`, `Interactables`, `Inventory`, `Loot`, `Persistence`, `Progression`, `Quests`,
`Shops`, `Stats`, `World`.

## Determinism + atomicity: a worked example

A `BuyFromShopCommand` runs `SpendCurrencyCommand` *and* `AddInventoryItemCommand` under
the hood. What happens if the inventory is full?

1. Snapshot taken: gold = 100, inventory = full.
2. Handler subtracts the price: gold = 80.
3. Handler tries to add the item — fails (capacity exceeded).
4. Handler returns failure result.
5. Dispatcher restores from snapshot: gold = 100 again, inventory still full.
6. The buffered `CurrencyChangedEvent` is discarded — UI never sees the partial deduction.

The player sees: "Inventory full." Their gold is intact. No half-completed purchase.

## Persistence: what's saved, what isn't

`Persistence/JsonGameStateSerializer` round-trips a `GameStateSnapshot` (a DTO mirror of
`GameState`) through JSON. The current schema version is `GameStateSnapshotMapper.CurrentSchemaVersion`.

Whenever you change a persisted shape, bump the schema version and add an `ISaveMigration`.
The migration pipeline runs migrations in order until the payload reaches the current
version.

What's **not** saved:

- The active battle (`GameState.ActiveBattle`). Saves should be taken between battles.
- Definitions (the catalog). Re-supplied at boot.
- Buffered events. Drained after each command.

See [persistence.md](persistence.md) for the full picture.

## TFM constraint

`Moonforge.Core` targets `netstandard2.1` so Unity can consume it. Tests and samples target
`net8.0` and can use modern C# APIs freely; the engine project cannot. If you write engine
code that needs `Span<byte>.Trim` or other modern APIs, check that they're available in
`netstandard2.1` first.

## See also

- [Pipeline](pipeline.md) — when to write a command, query, reactor, or just call directly.
- [Persistence](persistence.md) — snapshot mapper and migration pipeline.
- [Troubleshooting](troubleshooting.md) — common pipeline gotchas.
