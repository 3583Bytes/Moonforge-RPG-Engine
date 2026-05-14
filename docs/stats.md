# Stats

Moonforge exposes per-actor stat blocks with two layers:

- **Primary stats** are stored directly on `StatBlock.Base` (e.g. `str`, `vit`, `int`, `agi`).
- **Derived stats** are computed by formula at read time (e.g. `MaxHp = vit * 10 + level * 5`).

Both flow through the same modifier pipeline, so equipment, status effects, and ad-hoc buffs
stack consistently.

## Pipeline

```
base    = derived ? formula(primaries + level + extras) : Base[stat] ?? defaultBase
flat    = base + Σ Flat
add%    = flat × (1 + Σ AddPercent)
mult%   = afterAdd × ∏ (1 + MultPercent)
override = if any → highest Priority wins (tiebreak: SourceId asc)
final   = round → clamp to [Min, Max] if registered
```

Modifiers are sorted by `(Bucket, SourceKind, SourceId)` before each pass, so the same set
of modifiers produces the same value regardless of insertion order.

## Stat keys

Stat IDs are strings. Built-in constants live in `Moonforge.Core.Stats.StandardStats`:

```csharp
StandardStats.Strength      // "str"
StandardStats.Vitality      // "vit"
StandardStats.Attack        // "atk"
StandardStats.MaxHp         // "maxhp"
// ...
```

Games are free to use other keys — `"luck"`, `"corruption"`, `"sanity"` — without recompiling
the engine.

## Registering a stat (optional)

Registration is only required to declare clamps, a non-zero default, or a derivation formula:

```csharp
var defs = new InMemoryGameDefinitionCatalog()
    .AddStat(new StatDefinition(StandardStats.Vitality, defaultBase: 5))
    .AddStat(new StatDefinition(StandardStats.MaxHp, derivedFromFormula: "vit * 10 + level * 5"))
    .AddStat(new StatDefinition("crit", min: 0, max: 100));
```

Stats can also be used unregistered — they just behave as plain stored values with no clamps.

## Setting and reading

```csharp
// Imperative:
StatBlock block = gameState.ActorStatsState.GetOrCreate("hero");
block.SetBase(StandardStats.Strength, 14);
int str = block.Get(StandardStats.Strength, definitions, formulas);

// Through the command/query pipeline:
dispatcher.Dispatch(gameState, new SetStatBaseCommand("hero", StandardStats.Strength, 14), context);
int strength = new GetStatQueryHandler(definitions, formulas)
    .Query(gameState, new GetStatQuery("hero", StandardStats.Strength));
```

## Adding a buff

```csharp
dispatcher.Dispatch(gameState, new ApplyStatModifierCommand(
    "hero",
    new StatModifier(
        StandardStats.Attack,
        StatModifierBucket.AddPercent,
        0.25,            // +25%
        sourceKind: "buff",
        sourceId: "warcry")),
    context);
```

Remove it later by source:

```csharp
dispatcher.Dispatch(gameState, new RemoveStatModifiersCommand("hero", "buff", "warcry"), context);
```

## Integrations

- **Equipment.** `EquipItemCommand` translates `EquipmentDefinition.StatBonuses` into Flat
  modifiers with `SourceKind="equipment"`. Unequipping removes them.
- **Status effects.** `ApplyStatusEffectCommand` mirrors `StatusEffectDefinition.StatModifiers`
  into Flat modifiers with `SourceKind="status"`. Expiration or `RemoveStatusEffectCommand`
  withdraws them.
- **Combat.** `BattleRuntime` reads `atk`, `def`, `matk`, `mdef`, `maxhp` through the actor's
  `StatBlock` when one exists; actors without a block fall back to scalar `BattleActorState`
  fields, so older code continues to work.
- **Progression.** When evaluating a derived stat, the actor's `Level` is automatically
  exposed as the `level` variable to the formula evaluator.

## Derived stats need a real formula evaluator

The engine ships with `NoOpFormulaEvaluator`. To use `DerivedFromFormula`, provide an
`IFormulaEvaluator` that can parse the expressions you author. The sample demonstrates a
hardcoded evaluator; production games typically embed a small expression parser (e.g.
`NCalc`, `Flee`, or a custom one).

## Default actor ID

`EquipItemCommand` and `UnequipItemCommand` accept an optional `actorId` parameter, defaulting
to `"player"`. This matches the engine's current single-protagonist `EquipmentState` shape
while keeping the stat block actor-keyed from day one. When party/roster support lands, the
defaults can be removed without source-incompatible API changes.

## Damage types & resistances

Damage types are string-keyed metadata that tell the combat runtime how to resolve a damage
amount. Constants live in `Moonforge.Core.Combat.StandardDamageTypes` (`Physical`, `Magical`,
`Fire`, `Ice`, `Lightning`, `Holy`, `Dark`, `Poison`, `True`).

```csharp
var defs = new InMemoryGameDefinitionCatalog()
    .AddDamageType(new DamageTypeDefinition(
        StandardDamageTypes.Physical,
        attackStatId: "atk",
        flatDefenseStatId: "def"))   // physical keeps Atk - Def
    .AddDamageType(new DamageTypeDefinition(
        StandardDamageTypes.Fire,
        attackStatId: "matk"));      // fire: percent resistance only
```

Resistance is just a stat (`res.<type>`). Use the helper:

```csharp
block.SetBase(StandardStats.Resistance(StandardDamageTypes.Fire), 50);
// or:
block.SetBase(StandardStats.ResistanceFire, 50);
```

### Resolution

For each damage-dealing skill:

```
atk         = effective stat at typeDef.AttackStatId
rawAttack   = atk + skill.Power
afterFlat   = rawAttack − target.GetStat(typeDef.FlatDefenseStatId ?? skip)
resistance  = target.GetStat(typeDef.ResistanceStatId, signed)
if resistance ≥ 100:   damage = 0    // immunity, hard cap
else:                  damage = max(1, round(afterFlat × (100 − resistance) / 100))
```

Negative resistance amplifies (`-50` → 1.5× damage). Resistance above 100 caps at immunity.
Resistance modifiers stack through the same pipeline as any other stat — equipment, status
effects, and ad-hoc buffs all contribute.

### Routing skills to damage types

- **By default**: `BattleSkillEffectType.PhysicalDamage` looks up id `"physical"` in the
  catalog; `MagicalDamage` looks up `"magical"`. Existing skills auto-upgrade as soon as
  designers register those types.
- **Per-skill override**: pass `damageTypeId` to `BattleSkillDefinition` to opt a specific
  skill into a different type (e.g., a magical-flavor skill that does fire damage):
  ```csharp
  new BattleSkillDefinition(
      "skill.firebolt",
      BattleSkillEffectType.MagicalDamage,
      power: 12,
      damageTypeId: StandardDamageTypes.Fire);
  ```
- **No registration**: when a damage type isn't registered in the catalog, the runtime falls
  back to the pre-stat-system math (`Atk − Def` / `Matk − Mdef`). Resistance is ignored.

## Known limitations

- Turn-order initiative (read at battle start in `BattleRuntime.CreateBattle`) and AI threat
  sorting (`HighestThreatEnemy` target picker) currently read scalar `BattleActorState`
  fields, not the `StatBlock`. Stat-block-driven turn order will follow when party + actor
  registry land.
- Mid-battle equipment changes are not currently invalidated automatically. Re-equip after
  battle to refresh the block.
