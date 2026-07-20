# Dialogue

A node-graph dialogue system. Each `DialogueDefinition` declares a graph of nodes; each
node has text and a list of choices; choices can be gated by conditions and can fire
side effects on selection.

## Concepts

- **Node** — a unit of NPC speech. Has a `textKey` (looked up by your localization layer)
  and a list of `DialogueChoiceDefinition`s.
- **Choice** — a player option. Can be conditional (only visible if some world state holds),
  can navigate to another node, and can fire effects (set world variables, emit signals).
- **Effects** — actions the engine runs when a choice is selected.
- **Conditions** — predicates the engine evaluates to decide whether a choice is offered.
- **State** — per-dialogue: which node you're on, which nodes you've visited, which choices
  you've taken, whether it's completed.

## Defining a dialogue

```csharp
using Moonforge.Core.Data.Definitions;

definitions.AddDialogue(new DialogueDefinition(
    id: "dialogue.guard",
    startNodeId: "start",   // node the dialogue opens on
    nodes:
    [
        new DialogueNodeDefinition(
            id: "start",
            textKey: "dialogue.guard.start.text",
            choices:
            [
                new DialogueChoiceDefinition(
                    id: "ask_dungeon",
                    textKey: "dialogue.guard.ask_dungeon",
                    nextNodeId: "info_dungeon",   // navigates here when chosen
                    // Effects fire when this choice is selected.
                    effects:
                    [
                        // Sets a world flag the "ask_secret" choice below gates on.
                        new DialogueEffectDefinition(
                            DialogueEffectType.SetWorldBool,
                            key: "flag.guard.briefed",
                            boolValue: true),
                        // Emits a Talk signal so Talk quest objectives can auto-advance.
                        new DialogueEffectDefinition(
                            DialogueEffectType.EmitTalkSignal,
                            key: "npc.guard")
                    ]),

                new DialogueChoiceDefinition(
                    id: "ask_secret",
                    textKey: "dialogue.guard.ask_secret",
                    nextNodeId: "info_secret",
                    // Only offered once flag.guard.briefed is true (set by ask_dungeon).
                    conditions:
                    [
                        new DialogueConditionDefinition(
                            DialogueConditionType.WorldBoolEquals,
                            key: "flag.guard.briefed",
                            boolValue: true)
                    ]),

                new DialogueChoiceDefinition(
                    id: "bye",
                    textKey: "dialogue.guard.bye")
                    // no nextNodeId -> selecting this completes the dialogue
            ]),

        new DialogueNodeDefinition(
            id: "info_dungeon",
            textKey: "dialogue.guard.info.dungeon",
            choices: [new DialogueChoiceDefinition(id: "back", textKey: "common.back", nextNodeId: "start")]),

        new DialogueNodeDefinition(
            id: "info_secret",
            textKey: "dialogue.guard.info.secret",
            choices: [new DialogueChoiceDefinition(id: "back", textKey: "common.back", nextNodeId: "start")])
    ]));
```

`textKey` strings are opaque — your UI layer is responsible for mapping them to actual
text in the player's language.

## Starting and stepping

```csharp
using Moonforge.Core.Dialogue.Commands;
using Moonforge.Core.Dialogue.Queries;

// Opens the dialogue at its startNodeId ("start") and emits DialogueStartedEvent.
dispatcher.Dispatch(gameState, new StartDialogueCommand("dialogue.guard"), context);

// Render: ask the engine which choices the player should see (conditions already applied).
IReadOnlyList<string> visibleChoiceIds = new GetAvailableDialogueChoicesQueryHandler(definitions)
    .Query(gameState, new GetAvailableDialogueChoicesQuery("dialogue.guard"));

// "ask_secret" is filtered out if flag.guard.briefed is false.

// Player picks one: runs its effects, then advances to nextNodeId (or completes).
dispatcher.Dispatch(gameState, new ChooseDialogueChoiceCommand(
    dialogueId: "dialogue.guard",
    choiceId: "ask_dungeon"), context);
```

## Effect types

| Effect | `Key` | Other fields | What happens |
|---|---|---|---|
| `SetWorldBool` | world variable key | `BoolValue` | `gameState.WorldState[key] = bool` |
| `SetWorldInt` | world variable key | `IntValue` | `gameState.WorldState[key] = int` |
| `AddWorldInt` | world variable key | `IntValue` | `gameState.WorldState[key] += int` |
| `EmitTalkSignal` | actor id (e.g. `npc.guard`) | — | Publishes `QuestSignalType.Talk` for that actor |
| `EmitVisitSignal` | location id (e.g. `town.fountain`) | — | Publishes `QuestSignalType.Visit` |

`EmitTalkSignal` and `EmitVisitSignal` are how dialogue advances `Talk` / `Visit` quest
objectives without quests knowing about dialogue.

## Condition types

| Condition | Reads | Notes |
|---|---|---|
| `WorldBoolEquals` | `WorldState[key]` as bool | Matches `BoolValue` |
| `WorldIntAtLeast` | `WorldState[key]` as int | `>= IntValue` |
| `WorldIntAtMost` | `WorldState[key]` as int | `<= IntValue` |
| `QuestStatusIs` | `QuestState[key].Status` | Matches `QuestStatus` |

Multiple conditions on the same choice are AND-ed. For OR logic, split into two choices
with overlapping target nodes.

## State queries

```csharp
string? currentNodeId = new GetDialogueCurrentNodeQueryHandler()
    .Query(gameState, new GetDialogueCurrentNodeQuery("dialogue.guard"));

bool finished = new IsDialogueCompletedQueryHandler()
    .Query(gameState, new IsDialogueCompletedQuery("dialogue.guard"));
```

`DialogueInstanceState` (accessible directly via `gameState.DialogueState`) also exposes
`VisitedNodes` and `ChosenChoices` so you can model "you've already heard this" branches.

## Events

| Event | Use it for |
|---|---|
| `DialogueStartedEvent` | Open dialogue panel |
| `DialogueNodeEnteredEvent` | Display the new node text |
| `DialogueChoiceSelectedEvent` | Animate the player's selection |
| `DialogueCompletedEvent` | Close the dialogue panel |

## Patterns

### Locking content behind a previous conversation

The "ask_secret" choice in the example above is gated on `flag.guard.briefed`, which the
"ask_dungeon" choice sets. The player must talk to the guard about the dungeon before the
secret option appears.

### Branching by quest state

```csharp
new DialogueChoiceDefinition(
    id: "claim_reward",
    textKey: "dialogue.guard.claim_reward",
    nextNodeId: "node_thanks",
    // Only offered once quest.escort is Completed.
    conditions:
    [
        new DialogueConditionDefinition(
            DialogueConditionType.QuestStatusIs,
            key: "quest.escort",
            questStatus: QuestStatus.Completed)
    ]);
```

### Repeatable dialogues

`DialogueState` does not block re-entering a dialogue after completion — `StartDialogueCommand`
resets the instance to the start node. To make a dialogue one-shot, gate the entry point on
a world flag your dialogue itself sets.

## See also

- [Quests](quests.md) — Talk/Visit objectives auto-advance via dialogue signals
- [World](world.md) — the world variable system dialogue effects/conditions read and write
