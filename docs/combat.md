# Combat

A turn-based battle system: actors line up by initiative, take turns using skills, apply
status effects, and battles end when one faction is wiped out.

## Lifecycle

```
StartBattleCommand
   └── BattleState created with all actors, initiative-sorted
   └── BattleStartedEvent fired

UseBattleSkillCommand    (player turn)
   └── resolves damage/heal, applies status effects
   └── BattleActionResolvedEvent, BattleTurnAdvancedEvent

ExecuteAiTurnCommand     (enemy turn — controlled by AI policy)
   └── same shape as UseBattleSkillCommand, target chosen by AiTargetPolicy

… (repeat until victory/defeat) …

BattleEndedEvent
   └── Rewards (gold/items/loot table) granted atomically
```

`GameState.ActiveBattle` is non-null while a battle is in progress. Save during a battle
is unsupported by design — take snapshots between battles.

## Starting a battle

```csharp
using Moonforge.Core.Combat;
using Moonforge.Core.Combat.Commands;
using Moonforge.Core.Economy.Commands;

dispatcher.Dispatch(gameState, new StartBattleCommand(
    battleId: "battle.tutorial.01",
    actors:
    [
        new BattleActorDefinition(
            actorId: "party.hero",
            displayName: "Hero",
            faction: CombatFaction.Party,
            maxHp: 40, atk: 12, def: 6, matk: 4, mdef: 5,
            initiative: 18,
            skillIds: ["skill.strike", "skill.heal"],
            playerControlled: true),
        new BattleActorDefinition(
            actorId: "enemy.slime.01",
            displayName: "Slime",
            faction: CombatFaction.Enemy,
            maxHp: 18, atk: 5, def: 1, matk: 2, mdef: 1,
            initiative: 8,
            skillIds: ["skill.bite"],
            playerControlled: false,
            aiPolicy: new BattleAiPolicyDefinition(
                rules: [],
                fallbackSkillId: "skill.bite",
                fallbackTargetPolicy: BattleAiTargetPolicy.LowestHpEnemy))
    ],
    skills:
    [
        new BattleSkillDefinition("skill.strike", BattleSkillEffectType.PhysicalDamage, power: 8),
        new BattleSkillDefinition("skill.heal",   BattleSkillEffectType.Heal,           power: 10),
        new BattleSkillDefinition("skill.bite",   BattleSkillEffectType.PhysicalDamage, power: 4)
    ],
    seed: 12345,
    rewardCurrency: [new CurrencyDelta("gold", 20)],
    rewardLootTableId: "loot.slime"), context);
```

`seed` is the per-battle RNG seed. `rewardCurrency` and `rewardLootTableId` are granted
atomically when the battle ends in victory.

## Taking a turn

```csharp
string currentActorId = new GetCurrentBattleTurnActorQueryHandler()
    .Query(gameState, new GetCurrentBattleTurnActorQuery());

if (currentActorId == "party.hero")
{
    dispatcher.Dispatch(gameState, new UseBattleSkillCommand(
        actorId: "party.hero",
        skillId: "skill.strike",
        targetActorId: "enemy.slime.01"), context);
}
else
{
    // Let the AI policy pick a skill and target.
    dispatcher.Dispatch(gameState, new ExecuteAiTurnCommand(currentActorId), context);
}
```

Both commands publish `BattleActionResolvedEvent` (skill + target + amount) followed by
`BattleTurnAdvancedEvent` (next actor's id).

## Reading battle state

```csharp
BattleStatus? status = new GetActiveBattleStatusQueryHandler()
    .Query(gameState, new GetActiveBattleStatusQuery());

// BattleStatus: Active, Victory, Defeat
if (status == BattleStatus.Active)
{
    // ... continue
}
```

The full state is accessible directly when you need it:

```csharp
BattleState battle = gameState.ActiveBattle!;
foreach (BattleActorState actor in battle.Actors.Values)
{
    Console.WriteLine($"{actor.DisplayName}: {actor.Hp}/{actor.MaxHp}");
}
```

## Skills

A `BattleSkillDefinition` can:

- **Deal damage**: `BattleSkillEffectType.PhysicalDamage` (uses `atk`/`def` by default) or
  `MagicalDamage` (uses `matk`/`mdef`).
- **Heal**: `BattleSkillEffectType.Heal` (uses `matk` as the healing scalar).
- **Apply only**: `BattleSkillEffectType.Buff` (ally-targeted, no HP change) or
  `Debuff` (enemy-targeted, no HP change). Effect is carried entirely through
  `appliesStatuses`.
- **Hit multiple targets**: `targetMode` controls fan-out — `Single` (default, uses the
  caller-supplied target), `Self`, `AllAllies`, `AllEnemies`, `AllOthers`. For non-`Single`
  modes the command's target id is ignored.
- **Miss**: `accuracyPercent` (default 100) is rolled per target. On a miss the effect is
  skipped for that target and a `BattleActionMissedEvent` fires.
- **Randomize damage / heal**: `damageVariancePercent` (default 0) jitters the rolled
  amount by ± that percent (e.g. 15 → multiplier in `[85, 115]`). Immune targets still
  resolve to zero.
- **Critical hits**: `critChancePercent` (default 0, damage skills only) and
  `critMultiplierPercent` (default 200 = 2×) — when a crit lands, the base damage is
  multiplied before variance jitter and `BattleActionResolvedEvent.WasCritical` is true.
  Heals never crit.
- **Cooldown**: `cooldownTurns` blocks re-use for N turns after firing.
- **Cost resources**: `resourceCosts` consumes from `BattleActorState.Resources` (e.g.
  Focus / Mana).
- **Apply status effects**: `appliesStatuses` rolls per-application chances to apply a
  `StatusEffectDefinition` to the target (or self).
- **Specify a damage type**: `damageTypeId` overrides the default lookup, routing through
  the registered `DamageTypeDefinition` for resistance/immunity handling.

```csharp
new BattleSkillDefinition(
    "skill.firebolt",
    BattleSkillEffectType.MagicalDamage,
    power: 14,
    cooldownTurns: 2,
    resourceCosts: new Dictionary<string, int> { ["focus"] = 1 },
    displayName: "Fire Bolt",
    appliesStatuses:
    [
        new StatusApplicationDefinition("status.burn", StatusApplicationTarget.Target, chancePercent: 35)
    ],
    damageTypeId: StandardDamageTypes.Fire);
```

### Multi-target skills

`targetMode` controls how the engine resolves who the skill hits. For `Single` (default),
the caller passes the explicit target id with `UseBattleSkillCommand`. For every other
mode, the command's `targetActorId` is ignored and the engine fans the effect across the
matching actors in turn-order — one roll set per target.

```csharp
// Fire Nova: every enemy takes a Fire-typed magical hit, with some variance.
new BattleSkillDefinition(
    "skill.fire_nova",
    BattleSkillEffectType.MagicalDamage,
    power: 9,
    displayName: "Fire Nova",
    targetMode: BattleSkillTargetMode.AllEnemies,
    damageTypeId: StandardDamageTypes.Fire,
    damageVariancePercent: 15);

// Group heal: heals every living ally; full-HP allies are skipped silently.
new BattleSkillDefinition(
    "skill.mass_cure",
    BattleSkillEffectType.Heal,
    power: 10,
    targetMode: BattleSkillTargetMode.AllAllies);

// Self-buff: applies Bulwark to the caster.
new BattleSkillDefinition(
    "skill.bulwark",
    BattleSkillEffectType.Buff,
    power: 0,
    targetMode: BattleSkillTargetMode.Self,
    appliesStatuses:
    [
        new StatusApplicationDefinition("status.bulwark", StatusApplicationTarget.Self)
    ]);
```

Single-target validation rules still apply: trying to heal a full-HP ally with a `Single`
skill fails with `ValidationFailed`. For AoE heal the same condition silently filters the
ally out instead — the cast still succeeds on whoever's wounded.

### Accuracy, crits, and damage variance

All rolls are deterministic from the battle's seeded RNG (`BattleRngState`), so the
same battle re-runs identically. The order per target is: **accuracy → crit → variance →
status applications**. A miss skips all of the remaining rolls. An immune target
(resistance ≥ 100) resolves to 0 damage without consuming the crit or variance rolls.

The per-skill `AccuracyPercent`, `CritChancePercent`, and `CritMultiplierPercent` are
the **base** values; actor stats fold in additively at resolution time so equipment and
buffs can shift the rolls:

| Stat id | StandardStats const | Source | Effect |
|---|---|---|---|
| `acc` | `Accuracy` | attacker | `effectiveAccuracy = skill.AccuracyPercent + attacker.acc - target.eva` |
| `eva` | `Evasion` | target | (see above — subtracts from attacker's roll) |
| `crit` | `CritChance` | attacker | `effectiveCritChance = skill.CritChancePercent + attacker.crit` |
| `critdmg` | `CritDamage` | attacker | `effectiveMultiplier = skill.CritMultiplierPercent + attacker.critdmg` |

`acc` and `crit` clamp to `[0, 100]`. `critdmg` floors at 100 (no sub-1× crits). Stats
default to 0 if the actor has no stat block, so existing skills without actor stat
seeding behave exactly as before.

```csharp
// 80% chance to land; on a miss, BattleActionMissedEvent fires and the effect is
// skipped (no damage, no status applications).
new BattleSkillDefinition(
    "skill.hypnosis",
    BattleSkillEffectType.Debuff,
    power: 0,
    accuracyPercent: 80,
    appliesStatuses:
    [
        new StatusApplicationDefinition("status.sleep", chancePercent: 100)
    ]);

// ±20% damage roll — same seed reproduces the same number.
new BattleSkillDefinition(
    "skill.wild_swing",
    BattleSkillEffectType.PhysicalDamage,
    power: 12,
    damageVariancePercent: 20);

// 15% chance to crit for 2× damage. The crit roll happens after accuracy and before
// variance; the resolved event's WasCritical flag is true when it lands.
new BattleSkillDefinition(
    "skill.precise_strike",
    BattleSkillEffectType.PhysicalDamage,
    power: 9,
    critChancePercent: 15,
    critMultiplierPercent: 200);
```

Immunity (resistance ≥ 100) wins over crit and variance: an immune target still resolves
to 0 damage, never the floored 1.

## Damage types and resistances

See [stats.md](stats.md) for the full pipeline. The short version: register a
`DamageTypeDefinition` declaring which stats are read for attack, flat defense, and percent
resistance. The combat runtime looks up the skill's `DamageTypeId` (or the effect-type
default) and applies the formula:

```
damage = (atk + power - flatDefense) × (100 - resistance) / 100
       (clamped to 0 if resistance ≥ 100, else minimum 1)
```

```csharp
definitions
    .AddDamageType(new DamageTypeDefinition(
        StandardDamageTypes.Fire,
        attackStatId: "matk",
        flatDefenseStatId: null,                                  // pure-resistance type
        resistanceStatId: StandardStats.ResistanceFire));         // "res.fire"

// Make an enemy fire-immune:
gameState.ActorStatsState.GetOrCreate("enemy.demon")
    .SetBase(StandardStats.ResistanceFire, 100);
```

## Status effects

A `StatusEffectDefinition` describes an effect that applies for N turns:

```csharp
definitions.AddStatusEffect(new StatusEffectDefinition(
    id: "status.poison",
    durationTurns: 3,
    tickHpDelta: -2,                                              // -2 HP per tick
    displayName: "Poison",
    stackPolicy: StatusStackPolicy.RefreshDuration));

definitions.AddStatusEffect(new StatusEffectDefinition(
    id: "status.bulwark",
    durationTurns: 4,
    statModifiers: new Dictionary<string, int> { ["def"] = 4 },   // +4 DEF while active
    displayName: "Bulwark"));

definitions.AddStatusEffect(new StatusEffectDefinition(
    id: "status.stun",
    durationTurns: 1,
    preventsAction: true,                                         // skip your turn
    displayName: "Stun"));
```

Apply via skill side-effect or directly:

```csharp
dispatcher.Dispatch(gameState, new ApplyStatusEffectCommand(
    actorId: "enemy.slime.01",
    statusId: "status.poison",
    durationTurns: 3), context);

// Cleanse:
dispatcher.Dispatch(gameState, new RemoveStatusEffectCommand(
    actorId: "party.hero",
    statusId: "status.poison"), context);
```

Events:

- `StatusAppliedEvent` — when the effect lands.
- `StatusTickedEvent` — once per tick (start of the actor's turn).
- `StatusExpiredEvent` — when duration runs out or the effect is removed.
- `StatusPreventedActionEvent` — when `preventsAction=true` skipped a turn.

## AI policies

Enemies use a `BattleAiPolicyDefinition` to choose actions. The runtime evaluates rules
in priority order and falls back to the default if no rule matches:

```csharp
new BattleAiPolicyDefinition(
    rules:
    [
        // Heal self when below 40% HP.
        new BattleAiRuleDefinition(
            skillId: "skill.heal",
            priorityWeight: 100,
            targetPolicy: BattleAiTargetPolicy.Self,
            conditions: [new BattleAiConditionDefinition(BattleAiConditionType.SelfHpBelowPercent, 40)]),

        // Otherwise, throw bolts at the player when the player is wounded.
        new BattleAiRuleDefinition(
            skillId: "skill.bolt",
            priorityWeight: 50,
            targetPolicy: BattleAiTargetPolicy.HighestThreatEnemy,
            conditions: [new BattleAiConditionDefinition(BattleAiConditionType.AnyEnemyHpBelowPercent, 80)])
    ],
    fallbackSkillId: "skill.claw",
    fallbackTargetPolicy: BattleAiTargetPolicy.LowestHpEnemy);
```

Condition types (`BattleAiConditionType`): `SelfHpBelowPercent`, `AnyAllyHpBelowPercent`,
`AnyEnemyHpBelowPercent`. Target policies (`BattleAiTargetPolicy`): `Self`, `LowestHpAlly`,
`LowestHpEnemy`, `HighestThreatEnemy`, `RandomEnemy`.

## Resources (focus / mana / etc.)

Actors can carry resources separate from HP, refreshed each turn:

```csharp
new BattleActorDefinition(
    actorId: "party.hero",
    /* ... hp/atk/def/matk/mdef/initiative ... */
    skillIds: ["skill.firebolt"],
    playerControlled: true,
    resourceMaxes:        new Dictionary<string, int> { ["focus"] = 3 },
    startingResources:    new Dictionary<string, int> { ["focus"] = 3 },
    resourceRefreshPerTurn: new Dictionary<string, int> { ["focus"] = 1 });
```

Skills with `resourceCosts` consume on use; insufficient resources fail the command.

## Battle events for UI

| Event | Use it for |
|---|---|
| `BattleStartedEvent` | Open the battle screen, animate intro |
| `BattleTurnAdvancedEvent` | Highlight the new active actor |
| `BattleActionResolvedEvent` | Floating damage text, hit FX (`Amount = 0` means immune, `WasCritical = true` triggers crit FX) |
| `BattleActionMissedEvent` | Show "Miss!" floater; suppress damage / status FX |
| `StatusAppliedEvent` / `StatusExpiredEvent` | Show / hide status icons |
| `StatusTickedEvent` | DOT tick FX |
| `BattleEndedEvent` | Close battle screen, show rewards |

## See also

- [Stats](stats.md) — the modifier pipeline, derived stats, damage types in full
- [Loot](loot.md) — battle rewards via `rewardLootTableId`
- [Progression](progression.md) — wiring XP/level-up from kills
- [Cookbook](cookbook.md) — multi-enemy battles, themed bosses, status combos
