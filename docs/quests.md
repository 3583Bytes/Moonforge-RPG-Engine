# Quests

Quests have a lifecycle (`NotStarted → Active → Completed | Abandoned | Rewarded`),
one or more objectives, and an optional reward bundle. The engine ships a reactor that
auto-advances objectives in response to events from other modules, so most quests "just
work" without per-quest custom code.

## Lifecycle

```
StartQuestCommand
   └── QuestStartedEvent

(events from other modules)
   └── QuestObjectiveTrackingReactor advances objectives
       └── QuestObjectiveProgressedEvent
       └── QuestCompletedEvent (when all root objectives complete)

ClaimQuestRewardsCommand
   └── QuestRewardedEvent
   └── currency / inventory deltas applied through EconomyTransactionCommand

AbandonQuestCommand          (alternate exit)
   └── QuestAbandonedEvent
```

## Objective types

Each objective has a `QuestObjectiveType` that determines how it's tracked:

| Type | Triggered by | Example |
|---|---|---|
| `Kill` | `QuestSignalType.Kill` event with matching `targetId` | "Kill 5 wolves" — targetId = "enemy.wolf" |
| `Collect` | `InventoryItemChangedEvent` with positive `Delta` | "Collect 4 herbs" — targetId = "item.herb" |
| `Talk` | `QuestSignalType.Talk` (from dialogue `EmitTalkSignal` effect) | "Speak to the guard" — targetId = "npc.guard" |
| `Visit` | `QuestSignalType.Visit` (from dialogue / interactable signals) | "Visit the fountain" — targetId = "town.fountain" |
| `CompositeAnd` | Children all complete | "Collect 4 herbs **and** visit the healer" |
| `CompositeOr` | Any child completes | "Kill 2 crypt packs **or** 2 warrens packs" |

Composites reference their children by `id` and list them in `childObjectiveIds`. The
quest's `rootObjectiveIds` is what the reactor uses to decide whether the quest is
complete; leave it empty to default to every objective.

## Defining a quest

```csharp
using Moonforge.Core.Data.Definitions;
using Moonforge.Core.Economy.Commands;

definitions.AddQuest(new QuestDefinition(
    id: "quest.remedy",
    objectives:
    [
        new QuestObjectiveDefinition(
            id: "obj.collect.herb",
            objectiveType: QuestObjectiveType.Collect,
            targetId: "item.herb",
            requiredCount: 4,
            displayName: "Herbs gathered"),

        new QuestObjectiveDefinition(
            id: "obj.visit.healer",
            objectiveType: QuestObjectiveType.Visit,
            targetId: "town.healer",
            requiredCount: 1,
            displayName: "Healer visited"),

        new QuestObjectiveDefinition(
            id: "obj.root",
            objectiveType: QuestObjectiveType.CompositeAnd,
            childObjectiveIds: ["obj.collect.herb", "obj.visit.healer"])
    ],
    rootObjectiveIds: ["obj.root"],
    displayName: "Remedy Supply",
    description: "Gather herbs and report to the healer.",
    rewardCurrency: [new CurrencyDelta("gold", 50)],
    rewardInventory: [new InventoryDelta("item.tonic", 1)],
    autoTrack: true));
```

`autoTrack: false` lets you start a quest that the reactor *won't* advance automatically —
useful for fully-scripted scenarios where you call `EmitQuestSignalCommand` yourself.

## Starting and claiming

```csharp
using Moonforge.Core.Quests;
using Moonforge.Core.Quests.Commands;
using Moonforge.Core.Quests.Queries;

dispatcher.Dispatch(gameState, new StartQuestCommand("quest.remedy"), context);

// ...time passes, the player collects herbs and visits the healer...

QuestStatus status = new GetQuestStatusQueryHandler()
    .Query(gameState, new GetQuestStatusQuery("quest.remedy"));
// status == QuestStatus.Completed

dispatcher.Dispatch(gameState, new ClaimQuestRewardsCommand("quest.remedy"), context);
// status now == QuestStatus.Rewarded; gold and tonic granted atomically.
```

The reward grant routes through `EconomyTransactionCommand`, so if the inventory is full,
**the whole claim rolls back**. The quest stays `Completed` (not `Rewarded`) and the
player can free up space and try again.

## Reading progress

```csharp
int herbsGathered = new GetQuestObjectiveProgressQueryHandler()
    .Query(gameState, new GetQuestObjectiveProgressQuery("quest.remedy", "obj.collect.herb"));
```

Composite objectives report their resolved progress (0 or `requiredCount`) — useful for
UI badges but not for granular display. To render a quest sheet, iterate the leaf
objectives directly:

```csharp
QuestDefinition def = /* from definitions.TryGetQuest(...) */;
foreach (QuestObjectiveDefinition obj in def.Objectives)
{
    if (obj.ObjectiveType is QuestObjectiveType.CompositeAnd or QuestObjectiveType.CompositeOr)
    {
        continue;  // skip the wrapper
    }

    int progress = new GetQuestObjectiveProgressQueryHandler()
        .Query(gameState, new GetQuestObjectiveProgressQuery(def.Id, obj.Id));

    Console.WriteLine($"{obj.DisplayName ?? obj.Id}: {progress}/{obj.RequiredCount}");
}
```

## Manual signals

If you need to advance an objective from custom code — say, the player walked into a tile
that should count as a "Visit" — emit a signal:

```csharp
dispatcher.Dispatch(gameState, new EmitQuestSignalCommand(
    signalType: QuestSignalType.Visit,
    targetId: "town.fountain",
    amount: 1), context);
```

Dialogue's `EmitTalkSignal` / `EmitVisitSignal` effects and combat's kill signals do this
automatically. You only need to emit manually for custom triggers (entering a tile,
finishing a minigame, etc.).

## Abandoning

```csharp
dispatcher.Dispatch(gameState, new AbandonQuestCommand("quest.remedy"), context);
```

Abandoned quests cannot be restarted in v1. Add an `autoTrack: false` "retry"
quest definition if you need this.

## Events

| Event | When |
|---|---|
| `QuestStartedEvent` | After `StartQuestCommand` succeeds |
| `QuestObjectiveProgressedEvent` | When an objective's progress changes |
| `QuestCompletedEvent` | When all root objectives are satisfied |
| `QuestRewardedEvent` | When `ClaimQuestRewardsCommand` succeeds |
| `QuestAbandonedEvent` | When `AbandonQuestCommand` succeeds |
| `QuestSignalEvent` | Every emitted signal (kill/talk/visit) — useful for telemetry |

## See also

- [Dialogue](dialogue.md) — `EmitTalkSignal` and `EmitVisitSignal` effects
- [Economy](economy.md) — how reward currency/inventory deltas are applied
- [Loot](loot.md) — quest rewards via loot table (use `RollAndGrantLootCommand` after claim)
