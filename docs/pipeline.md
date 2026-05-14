# The command/query/reactor pipeline

Most of Moonforge's design choices fall out of one rule: **mutate state only through
commands, read state only through queries, and integrate modules only through events +
reactors.** This document explains when to write each, and the conventions to follow.

## At a glance

| You want to… | Write a… |
|---|---|
| Change `GameState` (grant gold, equip an item, advance a quest) | `ICommand` + `ICommandHandler<T>` |
| Read derived state (current HP, available dialogue choices, shop stock) | `IQuery<TResult>` + `IQueryHandler<T,TResult>` |
| Have module A react to module B without coupling them | `IDomainEventReactor` |
| Notify the host (UI, audio, telemetry) | `DomainEvent` published through `IDomainEventSink` |

## Writing a command

A command is a record-like object describing what should change. It carries inputs only —
no logic, no references to runtime services.

```csharp
public sealed class HealActorCommand : ICommand
{
    public HealActorCommand(string actorId, int amount)
    {
        ActorId = actorId;
        Amount = amount;
    }

    public string ActorId { get; }
    public int Amount { get; }
}
```

The handler does the work:

```csharp
public sealed class HealActorCommandHandler : ICommandHandler<HealActorCommand>
{
    public DomainResult Handle(GameState gameState, HealActorCommand command, CommandContext context)
    {
        if (command.Amount < 0)
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.ValidationFailed,
                "Heal amount must be non-negative."));
        }

        if (gameState.ActiveBattle is null
            || !gameState.ActiveBattle.TryGetActor(command.ActorId, out BattleActorState actor))
        {
            return DomainResult.Fail(new DomainError(
                DomainErrorCode.NotFound,
                $"Actor '{command.ActorId}' not in active battle."));
        }

        int before = actor.Hp;
        actor.Hp = System.Math.Min(actor.MaxHp, actor.Hp + command.Amount);
        int healed = actor.Hp - before;

        context.EventSink.Publish(new ActorHealedEvent(command.ActorId, healed));
        return DomainResult.Success();
    }
}
```

Register it on the dispatcher:

```csharp
dispatcher.Register(new HealActorCommandHandler());
```

## Writing a query

Queries are read-only. They never publish events and never mutate `GameState`.

```csharp
public sealed class GetActorHpQuery : IQuery<int>
{
    public GetActorHpQuery(string actorId) => ActorId = actorId;
    public string ActorId { get; }
}

public sealed class GetActorHpQueryHandler : IQueryHandler<GetActorHpQuery, int>
{
    public int Query(GameState gameState, GetActorHpQuery query)
    {
        if (gameState.ActiveBattle is null
            || !gameState.ActiveBattle.TryGetActor(query.ActorId, out BattleActorState actor))
        {
            return 0;
        }

        return actor.Hp;
    }
}
```

Unlike commands, queries aren't routed through the dispatcher — instantiate the handler
and call `.Query(...)` directly:

```csharp
int hp = new GetActorHpQueryHandler().Query(gameState, new GetActorHpQuery("party.hero"));
```

This keeps queries cheap (no transactional overhead) and side-effect-free by construction.

## Writing a reactor

A reactor inserts logic into the transaction whenever a relevant event is published. Use
this when **module A's events should drive module B's state**, but A shouldn't know B exists.

```csharp
public sealed class XpOnKillReactor : IDomainEventReactor
{
    public DomainResult React(GameState gameState, DomainEvent ev, CommandContext context)
    {
        if (ev is not BattleActionResolvedEvent action || action.WasHeal)
        {
            return DomainResult.Success();
        }

        // ... look up xp reward, apply it via GrantExperienceCommand or directly,
        //     publish ExperienceGrantedEvent.

        return DomainResult.Success();
    }
}

dispatcher.RegisterReactor(new XpOnKillReactor());
```

Reactors run inside the originating command's transaction. If a reactor returns failure,
the whole transaction rolls back — including the command's own state changes.

**Reactors should not dispatch commands.** Mutate `GameState` directly. Dispatching from
inside a reactor risks re-entrancy and breaks the snapshot/rollback invariants.

## Writing an event

Events are simple data carriers used for two purposes:

1. **Internal coupling** — reactors observe events to drive cross-module behavior.
2. **Host notification** — UI, audio, telemetry drain the sink to render or log.

```csharp
public sealed class ActorHealedEvent : DomainEvent
{
    public ActorHealedEvent(string actorId, int amount)
        : base(nameof(ActorHealedEvent))
    {
        ActorId = actorId;
        Amount = amount;
    }

    public string ActorId { get; }
    public int Amount { get; }
}
```

The host drains events between ticks:

```csharp
foreach (DomainEvent ev in sink.DrainNewEvents())
{
    switch (ev)
    {
        case ActorHealedEvent h:
            ui.ShowFloatingText(h.ActorId, $"+{h.Amount}");
            break;
        // ... other cases
    }
}
```

Events are only flushed to the *host's* sink after the originating command **and** all
its reactors succeed. A failed command discards both its state changes and its events.

## Error handling: `DomainResult`

Commands return `DomainResult`. Success carries no payload; failure carries a
`DomainError`:

```csharp
public sealed class DomainError
{
    public DomainErrorCode Code { get; }
    public string Message { get; }
}

public enum DomainErrorCode
{
    ValidationFailed,
    NotFound,
    Conflict,
    InsufficientResources,
    InvalidState,
    UnsupportedOperation,
    InternalError
}
```

**Don't throw for expected failures.** Insufficient gold, full inventory, target out of
range — these are domain errors, not exceptions. Throwing inside a handler still works
(the dispatcher catches and rolls back), but it loses the `DomainErrorCode` and message
clarity that callers rely on.

Throw only for *programmer* errors: null arguments, contract violations, internal
invariant failures.

## Composition: commands inside commands

A command handler can dispatch another command:

```csharp
public DomainResult Handle(GameState gameState, BuyFromShopCommand cmd, CommandContext ctx)
{
    DomainResult spend = new SpendCurrencyCommandHandler().Handle(
        gameState,
        new SpendCurrencyCommand(cmd.CurrencyId, price),
        ctx);
    if (!spend.IsSuccess) return spend;

    DomainResult add = new AddInventoryItemCommandHandler().Handle(
        gameState,
        new AddInventoryItemCommand(cmd.ItemId, cmd.Quantity),
        ctx);
    if (!add.IsSuccess) return add;

    return DomainResult.Success();
}
```

The outer dispatcher's snapshot wraps all of this — if the second sub-call fails, both
get rolled back. Sub-handlers don't need their own transaction.

## When *not* to use the pipeline

Sometimes a query handler isn't worth the ceremony. If you only need to read one scalar
from a sub-state inside a single class, a direct field read is fine:

```csharp
int gold = (int)gameState.CurrencyWallet.GetBalance("gold");
```

The query handler exists for queries you'd want to expose externally (to UI, AI, scripts)
or that perform non-trivial computation (`GetStatQuery` builds the formula evaluator
context).

Similarly, low-level helpers (e.g. `EncounterResolver.Roll(rng, table)`) are public for
host code that needs to roll outside a command's lifetime — for example, a procedural
world generator that pre-computes layouts before any `GameState` exists.

## See also

- [Architecture](architecture.md) — the broader picture this fits into.
- [Cookbook](cookbook.md) — recipes that put the pipeline through its paces.
