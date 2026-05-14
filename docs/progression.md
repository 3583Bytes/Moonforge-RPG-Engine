# Progression

Per-actor experience and levels. Levels are computed from XP using a designer-authored
`ExperienceCurveDefinition` — a list of XP thresholds. Granting XP can cause one or more
level-ups in a single command; each level-up fires a `LevelUpEvent` so other modules
(stats, content unlocks) can react.

## XP curves

The curve is a sorted list of thresholds. Index 0 is the XP required to reach level 2,
index 1 the XP to reach level 3, and so on. Beyond the last threshold the actor stops
levelling.

```csharp
using Moonforge.Core.Data.Definitions;

definitions.AddExperienceCurve(new ExperienceCurveDefinition(
    id: "curve.hero",
    xpThresholds: new long[] { 20, 60, 120, 200, 320, 480, 700, 1000, 1400, 1900 },
    displayName: "Hero Curve"));
```

This curve gives 11 total levels (1 → 11). XP totals are cumulative — to reach level 4
the actor needs 120 XP in total, not 60 more after reaching level 3.

## Configuring an actor

Each actor that should track XP needs progression state — typically created at run start:

```csharp
using Moonforge.Core.Progression.Commands;

dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(
    actorId: "party.hero",
    curveId: "curve.hero",
    level: 1,
    xp: 0), context);
```

Calling this on an existing actor overwrites their state. If you load a save, the
snapshot restores progression directly — you don't need to re-issue this.

## Granting XP

```csharp
dispatcher.Dispatch(gameState, new GrantExperienceCommand(
    actorId: "party.hero",
    amount: 35), context);
```

One command can cross multiple thresholds at once. For each level gained, the handler
publishes a `LevelUpEvent` with the new level number. Subscribers (UI, stat-recompute
reactors) can fan out from there.

## Reading progression

```csharp
if (gameState.ProgressionState.TryGet("party.hero", out ActorProgression progression))
{
    int level = progression.Level;
    long xp    = progression.Xp;
}
```

(There's no `GetProgressionQuery` in v1 — direct read is the idiomatic path.)

## Wiring level-ups to stat changes

A common pattern: each level-up grants +1 to all primary stats. Use a reactor:

```csharp
public sealed class StatGainOnLevelUpReactor : IDomainEventReactor
{
    public DomainResult React(GameState gameState, DomainEvent ev, CommandContext ctx)
    {
        if (ev is not LevelUpEvent levelUp) return DomainResult.Success();

        StatBlock block = gameState.ActorStatsState.GetOrCreate(levelUp.ActorId);
        block.AddModifier(new StatModifier(
            "atk", StatModifierBucket.Flat, 1,
            sourceKind: "progression",
            sourceId: $"level.{levelUp.NewLevel}"));
        // ... repeat for other stats

        return DomainResult.Success();
    }
}

dispatcher.RegisterReactor(new StatGainOnLevelUpReactor());
```

Tagging the modifier with `sourceId = $"level.{newLevel}"` makes it idempotent: even if
the reactor fires twice for the same level (shouldn't happen, but defense in depth), the
same modifier doesn't double-count because you can dedupe by source.

Alternatively, derive stats from `level` directly via a formula and skip the reactor —
see [stats.md](stats.md):

```csharp
definitions.AddStat(new StatDefinition(
    "atk",
    derivedFromFormula: "8 + level * 2"));
```

`GetStatQueryHandler` automatically exposes the actor's `Level` as the `level` variable
to the formula evaluator, so any stat formula can reference it.

## Events

| Event | When |
|---|---|
| `ExperienceGrantedEvent` | After `GrantExperienceCommand` succeeds (one per call, after all level-ups) |
| `LevelUpEvent` | Once per level gained (so a 3-level jump fires three events in order) |

## Patterns

### XP rewards on enemy defeat

`BattleActorDefinition.XpReward` is read by the engine's combat runtime, but **the engine
does not auto-grant XP on kill** in v1. The pattern is to react to `BattleActionResolvedEvent`
that drops a target to 0 HP and dispatch the grant yourself:

```csharp
public sealed class GrantXpOnKillReactor : IDomainEventReactor
{
    public DomainResult React(GameState gs, DomainEvent ev, CommandContext ctx)
    {
        if (ev is not BattleActionResolvedEvent action || action.WasHeal) return DomainResult.Success();
        if (action.Amount <= 0) return DomainResult.Success();

        if (!gs.ActiveBattle!.TryGetActor(action.TargetActorId, out BattleActorState target)
            || target.Hp > 0) return DomainResult.Success();

        // Find the active player actor and grant the reward.
        BattleActorState? killer = gs.ActiveBattle.Actors.Values
            .FirstOrDefault(a => a.Faction == CombatFaction.Party);
        if (killer is null) return DomainResult.Success();

        gs.ProgressionState.AddXp(killer.ActorId, target.XpReward);
        // (Use a real GrantExperienceCommand if you want the LevelUpEvent fan-out.)

        return DomainResult.Success();
    }
}
```

The sample console takes a simpler route: aggregates total XP earned during a battle in
the host code and dispatches one `GrantExperienceCommand` after `BattleEndedEvent` with
status `Victory`.

### Cap-level handling

When the actor's level exceeds `xpThresholds.Count`, extra XP is silently absorbed (and
`ExperienceGrantedEvent` still fires with the granted amount). To show "MAX LV" in the
UI, compare `progression.Level` against `curve.XpThresholds.Count + 1`.

## See also

- [Stats](stats.md) — derived stats reference `level`; modifier pipeline for level-up bonuses
- [Combat](combat.md) — `XpReward` on `BattleActorDefinition`
