# Cookbook

Task-shaped recipes. Each one is a complete, copy-pasteable example with the bare minimum
of context.

Conventions: examples assume the standard `using`s from the
[docs index](README.md#conventions) and a constructed `gameState`, `context`, `definitions`,
and `dispatcher`.

---

## Set up an actor with stats and equipment

```csharp
// 1. Register the stats you want clamped or derived.
definitions
    .AddStat(new StatDefinition(StandardStats.Vitality, defaultBase: 5))
    .AddStat(new StatDefinition(StandardStats.MaxHp,
        derivedFromFormula: "vit * 5 + level * 4",
        min: 1))
    .AddEquipmentSlot(new EquipmentSlotDefinition("slot.weapon", "Weapon"))
    .AddItem(new ItemDefinition("item.bronze_blade", maxStack: 1))
    .AddEquipment(new EquipmentDefinition(
        itemId: "item.bronze_blade",
        slotId: "slot.weapon",
        statBonuses: new Dictionary<string, int> { ["atk"] = 4 },
        displayName: "Bronze Blade"));

// 2. Configure progression so 'level' resolves in formulas.
dispatcher.Dispatch(gameState, new ConfigureActorProgressionCommand(
    actorId: "party.hero",
    curveId: "curve.hero",
    level: 3,
    xp: 0), context);

// 3. Seed primary stats.
dispatcher.Dispatch(gameState, new SetStatBaseCommand("party.hero", StandardStats.Vitality, 12), context);
dispatcher.Dispatch(gameState, new SetStatBaseCommand("party.hero", StandardStats.Attack,   10), context);

// 4. Put a weapon in the bag and equip it.
dispatcher.Dispatch(gameState, new ConfigureInventoryCapacityCommand(20), context);
dispatcher.Dispatch(gameState, new AddInventoryItemCommand("item.bronze_blade", 1), context);
dispatcher.Dispatch(gameState, new EquipItemCommand("item.bronze_blade", "party.hero"), context);

// 5. Read derived final stats.
GetStatQueryHandler statQuery = new(definitions, context.FormulaEvaluator);
int maxHp = statQuery.Query(gameState, new GetStatQuery("party.hero", StandardStats.MaxHp));
int atk   = statQuery.Query(gameState, new GetStatQuery("party.hero", StandardStats.Attack));
// maxHp = vit(12) * 5 + level(3) * 4 = 72
// atk   = base(10) + equipment(4) = 14
```

Notes:

- Use a real `IFormulaEvaluator` (not `NoOpFormulaEvaluator`) when you have derived stats.
  The sample's `ExpressionFormulaEvaluator` is a small recursive-descent parser you can
  copy.
- `level` is auto-exposed in formulas by `GetStatQueryHandler`.

---

## Run a complete battle

```csharp
// Register damage types up front so resistance applies.
definitions
    .AddDamageType(new DamageTypeDefinition(
        StandardDamageTypes.Physical, "atk", "def", StandardStats.ResistancePhysical))
    .AddDamageType(new DamageTypeDefinition(
        StandardDamageTypes.Magical, "matk", "mdef", StandardStats.ResistanceMagical))
    .AddDamageType(new DamageTypeDefinition(
        StandardDamageTypes.Fire, "matk", null, StandardStats.ResistanceFire))
    .AddStatusEffect(new StatusEffectDefinition(
        id: "status.burn",
        durationTurns: 3,
        tickHpDelta: -2,
        displayName: "Burn"));

dispatcher.Dispatch(gameState, new StartBattleCommand(
    battleId: "battle.01",
    actors:
    [
        new BattleActorDefinition(
            "party.hero", "Hero", CombatFaction.Party,
            maxHp: 40, atk: 10, def: 5, matk: 8, mdef: 4, initiative: 18,
            skillIds: ["skill.firebolt", "skill.strike"],
            playerControlled: true),
        new BattleActorDefinition(
            "enemy.dummy", "Training Dummy", CombatFaction.Enemy,
            maxHp: 30, atk: 4, def: 2, matk: 0, mdef: 2, initiative: 5,
            skillIds: ["skill.strike"],
            playerControlled: false,
            aiPolicy: new BattleAiPolicyDefinition(
                rules: [],
                fallbackSkillId: "skill.strike",
                fallbackTargetPolicy: BattleAiTargetPolicy.LowestHpEnemy))
    ],
    skills:
    [
        new BattleSkillDefinition("skill.strike",   BattleSkillEffectType.PhysicalDamage, 6),
        new BattleSkillDefinition("skill.firebolt", BattleSkillEffectType.MagicalDamage, 10,
            displayName: "Fire Bolt",
            damageTypeId: StandardDamageTypes.Fire,
            appliesStatuses:
            [
                new StatusApplicationDefinition("status.burn", StatusApplicationTarget.Target, chancePercent: 50)
            ])
    ],
    seed: 42), context);

// Turn loop.
while (new GetActiveBattleStatusQueryHandler().Query(gameState, new GetActiveBattleStatusQuery()) == BattleStatus.Active)
{
    string? actorId = new GetCurrentBattleTurnActorQueryHandler().Query(gameState, new GetCurrentBattleTurnActorQuery());
    if (actorId == "party.hero")
    {
        dispatcher.Dispatch(gameState, new UseBattleSkillCommand("party.hero", "skill.firebolt", "enemy.dummy"), context);
    }
    else
    {
        dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(actorId!), context);
    }
}
```

---

## Make an enemy fire-immune

```csharp
gameState.ActorStatsState.GetOrCreate("enemy.fire_drake")
    .SetBase(StandardStats.ResistanceFire, 100);
```

Or via the canonical command:

```csharp
dispatcher.Dispatch(gameState, new SetStatBaseCommand(
    actorId: "enemy.fire_drake",
    statId: StandardStats.ResistanceFire,
    value: 100), context);
```

Any skill with `damageTypeId: "fire"` deals 0 damage to this actor — the runtime
publishes `BattleActionResolvedEvent` with `Amount = 0`, which UI can render as "Immune!".

---

## Save and load (with sample-style wrapper)

```csharp
public sealed record GameSaveFile(
    int FileSchemaVersion,
    string EngineStateJson,
    string PlayerName,
    int CurrentScene);

JsonGameStateSerializer engineSerializer = new(new ISaveMigration[]
{
    // Register your migrations here, in order.
});

// Save:
GameStateSnapshot engineSnapshot = GameStateSnapshotMapper.Capture(gameState);
GameSaveFile file = new(
    FileSchemaVersion: 1,
    EngineStateJson: engineSerializer.Serialize(engineSnapshot),
    PlayerName: "Adam",
    CurrentScene: 3);

File.WriteAllText("save.json", JsonSerializer.Serialize(file));

// Load:
GameSaveFile loaded = JsonSerializer.Deserialize<GameSaveFile>(File.ReadAllText("save.json"))!;
GameStateSnapshot restoredSnapshot = engineSerializer.Deserialize(loaded.EngineStateJson);
gameState = new GameState();
GameStateSnapshotMapper.Apply(gameState, restoredSnapshot);
```

The engine's migration pipeline auto-upgrades old `EngineStateJson` before deserialization.

---

## Branching dialogue with quest gating

```csharp
definitions.AddDialogue(new DialogueDefinition(
    id: "dialogue.merchant",
    startNodeId: "greet",
    nodes:
    [
        new DialogueNodeDefinition("greet", "merchant.greet", choices:
        [
            new DialogueChoiceDefinition(id: "buy", textKey: "merchant.choice.buy",   nextNodeId: "shop"),

            // Only visible if the player has the artifact quest active.
            new DialogueChoiceDefinition(
                id: "ask_artifact",
                textKey: "merchant.choice.ask_artifact",
                nextNodeId: "tell_artifact",
                conditions:
                [
                    new DialogueConditionDefinition(
                        DialogueConditionType.QuestStatusIs,
                        key: "quest.artifact",
                        questStatus: QuestStatus.Active)
                ]),

            new DialogueChoiceDefinition(id: "bye", textKey: "merchant.choice.bye")
        ]),
        // ... other nodes
    ]));

dispatcher.Dispatch(gameState, new StartDialogueCommand("dialogue.merchant"), context);

IReadOnlyList<string> visible = new GetAvailableDialogueChoicesQueryHandler(definitions)
    .Query(gameState, new GetAvailableDialogueChoicesQuery("dialogue.merchant"));
// "ask_artifact" is filtered out if quest.artifact isn't Active.
```

---

## Loot drops on enemy kill

```csharp
definitions.AddLootTable(new LootTableDefinition(
    "loot.bandit",
    LootRollMode.RollEach,
    [
        LootEntryDefinition.Currency("gold", chancePercent: 100, minQuantity: 5, maxQuantity: 15),
        LootEntryDefinition.Item("item.potion", chancePercent: 25),
        LootEntryDefinition.Item("item.rare_gem", chancePercent: 3,
            conditions:
            [
                new LootConditionDefinition(LootConditionType.WorldBoolEquals,
                    key: "flag.lucky_charm_active",
                    boolValue: true)
            ])
    ]));

dispatcher.Dispatch(gameState, new RollAndGrantLootCommand("loot.bandit"), context);
// Or attach via StartBattleCommand.rewardLootTableId for automatic grant on victory.
```

---

## Reactor: auto-grant XP on enemy defeat

```csharp
public sealed class GrantXpOnKillReactor : IDomainEventReactor
{
    public DomainResult React(GameState gs, DomainEvent ev, CommandContext ctx)
    {
        if (ev is not BattleActionResolvedEvent action || action.WasHeal || action.Amount <= 0)
            return DomainResult.Success();

        if (gs.ActiveBattle is null
            || !gs.ActiveBattle.TryGetActor(action.TargetActorId, out BattleActorState target)
            || target.Hp > 0)
            return DomainResult.Success();

        // Find the killer's faction and grant XP to all party actors.
        foreach (BattleActorState a in gs.ActiveBattle.Actors.Values)
        {
            if (a.Faction != CombatFaction.Party) continue;
            gs.ProgressionState.AddXp(a.ActorId, target.XpReward);
        }

        return DomainResult.Success();
    }
}

dispatcher.RegisterReactor(new GrantXpOnKillReactor());
```

---

## Lever opens a vault door

```csharp
definitions
    .AddInteractable(new InteractableDefinition(
        id: "interactable.door.vault",
        startsLocked: true,
        maxUses: -1,
        blocksMovement: true))
    .AddInteractable(new InteractableDefinition(
        id: "interactable.lever.vault",
        effects:
        [
            new InteractableEffectDefinition(
                kind: InteractableEffectKind.UnlockInteractable,
                targetId: "vault.door.01"),
            new InteractableEffectDefinition(
                kind: InteractableEffectKind.EmitInteractionSignal,
                targetId: "signal.vault.opened")
        ],
        maxUses: 1));

// Place both during world setup.
dispatcher.Dispatch(gameState, new PlaceInteractableCommand("vault.door.01",   "interactable.door.vault",   new GridPosition(10, 5)), context);
dispatcher.Dispatch(gameState, new PlaceInteractableCommand("vault.lever.01",  "interactable.lever.vault",  new GridPosition(15, 5)), context);

// When the player pulls the lever (adjacent only), the door auto-unlocks.
dispatcher.Dispatch(gameState, new InteractWithCommand("party.hero", "vault.lever.01"), context);
```

---

## Advance simulation time (for shop restocks)

```csharp
// Whenever a meaningful chunk of in-game time passes — entering a dungeon floor, sleeping
// at an inn, finishing a major scene:
gameState.SimulationMinutes += 30;

// The next BuyFromShopCommand or SellToShopCommand checks the elapsed time against each
// shop's restockIntervalMinutes and refills stock if needed (firing ShopRestockedEvent).
```

---

## Conditional loot via world flag

```csharp
definitions.AddLootTable(new LootTableDefinition(
    "loot.boss.rematch",
    LootRollMode.PickOne,
    [
        // Normal drop.
        LootEntryDefinition.Item("item.boss_trophy", weight: 60),

        // Only drops if the player has the "second chance" buff active.
        LootEntryDefinition.Item("item.legendary_drop", weight: 5,
            conditions:
            [
                new LootConditionDefinition(LootConditionType.WorldBoolEquals,
                    key: "flag.second_chance_active",
                    boolValue: true)
            ])
    ]));
```

When the condition is false, that entry is invisible — `PickOne` re-weights against the
remaining eligible entries.

---

## Nested loot tables

```csharp
definitions
    .AddLootTable(new LootTableDefinition(
        "loot.rare_gear",
        LootRollMode.PickOne,
        [
            LootEntryDefinition.Item("item.gear.epic_blade",  weight: 1),
            LootEntryDefinition.Item("item.gear.epic_robe",   weight: 1),
            LootEntryDefinition.Item("item.gear.epic_amulet", weight: 1)
        ]))
    .AddLootTable(new LootTableDefinition(
        "loot.boss",
        LootRollMode.RollEach,
        [
            LootEntryDefinition.Currency("gold", chancePercent: 100, minQuantity: 100, maxQuantity: 200),
            LootEntryDefinition.NestedTable("loot.rare_gear", chancePercent: 25)
        ]));
```

Boss drops always give gold; 25% of the time it also rolls one piece of epic gear from
the nested table. Recursion is capped at depth 8 and cycles terminate silently.

---

## Find an interactable near the player

```csharp
GridPosition heroPos = /* from GetExplorationActorPositionQuery */;

InteractableInstance? nearby = new GetInteractableAtQueryHandler()
    .Query(gameState, new GetInteractableAtQuery(heroPos));

if (nearby is not null && nearby.Status != InteractableStatus.Consumed)
{
    dispatcher.Dispatch(gameState, new InteractWithCommand("party.hero", nearby.InstanceId), context);
}
```

---

## See also

- [Troubleshooting](troubleshooting.md) — when these recipes don't behave as expected
- Per-module guides via the [docs index](README.md)
