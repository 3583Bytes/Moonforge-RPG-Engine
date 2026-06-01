# Equipment

Equipment slots a player can fill — weapon, armor, accessory, whatever the game defines.
Equipping an item translates the item's stat bonuses into Flat modifiers on the actor's
stat block; unequipping removes them. The system is intentionally simple: it doesn't
know about durability, sockets, or enchantments — those are content concerns the host
game can layer on top.

## Slots

A `EquipmentSlotDefinition` registers a slot id and a display name:

```csharp
using Moonforge.Core.Data.Definitions;

definitions
    // EquipmentSlotDefinition(slotId, displayName) — displayName is for UI only.
    .AddEquipmentSlot(new EquipmentSlotDefinition("slot.weapon",    "Weapon"))
    .AddEquipmentSlot(new EquipmentSlotDefinition("slot.armor",     "Armor"))
    .AddEquipmentSlot(new EquipmentSlotDefinition("slot.accessory", "Accessory"));
```

The engine doesn't restrict how many slots you have — define as many as your design
needs. Two-handed weapons, paired rings, mount slots: all just additional slot ids.

## Equipment definitions

An `EquipmentDefinition` ties an item id to a slot and a stat-bonus dictionary:

```csharp
using Moonforge.Core.Equipment;   // StandardEquipmentStats

definitions
    // The item must exist as an ItemDefinition before it can be equipped.
    .AddItem(new ItemDefinition("item.gear.bronze_blade", stackLimit: 1))
    .AddEquipment(new EquipmentDefinition(
        itemId: "item.gear.bronze_blade",
        slotId: "slot.weapon",
        // Each entry becomes one Flat StatModifier on the wearer while equipped.
        statBonuses: new Dictionary<string, int>
        {
            [StandardEquipmentStats.Attack] = 4,     // +4 atk
            [StandardEquipmentStats.Initiative] = 1  // +1 initiative
        },
        displayName: "Bronze Blade"));
```

`StandardEquipmentStats` is just a constants class: `Attack` = `"atk"`, `Defense` = `"def"`,
`MagicAttack` = `"matk"`, `MagicDefense` = `"mdef"`, `Initiative` = `"initiative"`. The
keys are stat ids — you can write to any stat you want (including custom ones like
`"crit"` or `"luck"`).

## Equipping

```csharp
using Moonforge.Core.Equipment.Commands;
using Moonforge.Core.Equipment.Queries;

// Consumes one of the item from the bag and applies its bonuses to actorId's stat block.
dispatcher.Dispatch(gameState, new EquipItemCommand(
    itemId: "item.gear.bronze_blade",
    actorId: "party.hero"), context);

// Or, with the default actorId ("player"):
dispatcher.Dispatch(gameState, new EquipItemCommand("item.gear.bronze_blade"), context);
```

The handler:

1. Looks up the item's `EquipmentDefinition` to find the target slot.
2. If the slot is already filled, unequips the previous item first (which removes its
   modifiers atomically).
3. Removes the item from inventory.
4. Records the equip in `EquipmentState`.
5. Writes Flat modifiers (one per stat-bonus entry) into the actor's `StatBlock` with
   `SourceKind = "equipment"`, `SourceId = "<slot>:<itemId>"`.

`UnequipItemCommand` reverses this: removes the modifiers, returns the item to inventory.

```csharp
// Removes the slot's modifiers and returns the item to the bag (fails if the bag is full).
dispatcher.Dispatch(gameState, new UnequipItemCommand("slot.weapon", actorId: "party.hero"), context);
```

## Reading equipped state

```csharp
string? equippedWeapon = new GetEquippedItemQueryHandler()
    .Query(gameState, new GetEquippedItemQuery("slot.weapon"));
// equippedWeapon == "item.gear.bronze_blade" or null

IReadOnlyDictionary<string, int> totalBonuses = new GetEquipmentBonusesQueryHandler(definitions)
    .Query(gameState, new GetEquipmentBonusesQuery());
// totalBonuses["atk"] == 4 (sum across all equipped slots)
```

## Granted skills

Equipment can also grant skills while equipped — a magic wand that teaches `skill.bolt`,
a thief's dagger that adds `skill.backstab`. Declare them on the definition:

```csharp
definitions.AddEquipment(new EquipmentDefinition(
    itemId: "item.gear.oak_wand",
    slotId: "slot.weapon",
    statBonuses: new Dictionary<string, int> { [StandardEquipmentStats.MagicAttack] = 2 },
    displayName: "Oak Wand",
    // Skills the wearer can use while this is equipped (not auto-added to a battle actor).
    grantedSkillIds: ["skill.bolt"]));
```

The engine does **not** auto-merge these into a battle actor — host code reads them when
building the actor for `StartBattleCommand`:

```csharp
// Deduplicated union of grantedSkillIds across every currently-equipped item.
IReadOnlyList<string> granted = new GetEquipmentGrantedSkillsQueryHandler(definitions)
    .Query(gameState, new GetEquipmentGrantedSkillsQuery());

// Merge granted skills into the hero's base skill list before building the battle actor.
List<string> heroSkills = ["skill.attack", "skill.potion"];
foreach (string id in granted)
{
    if (!heroSkills.Contains(id)) heroSkills.Add(id);
}

new BattleActorDefinition(/* ... */, skillIds: heroSkills, /* ... */);
```

Make sure each granted skill id is also present in the `StartBattleCommand`'s skill list
— the actor referencing a skill that isn't in the battle's catalog will fail with
`Skill not found` when the player tries to use it.

The query returns a deduplicated union across all equipped items, so two pieces of gear
granting the same skill don't double-list.

## How equipment integrates with stats

The bonuses don't live on `EquipmentState` — they live on the actor's `StatBlock` as
modifiers with `SourceKind = "equipment"`. This means:

- Equipment-derived stats flow through the same Flat → Add% → Mult% → Override pipeline as
  status effects and ad-hoc buffs (see [stats.md](stats.md)).
- Reading `block.Get("atk", ...)` returns the final value with equipment, status, and base
  combined — you never look at equipment bonuses separately for combat math.
- Removing an item by source is cheap: `block.RemoveModifiersBySource("equipment", "<slot>|<itemId>")`.

If you load a save from before `ActorStatsState` was populated for the player, equipment
modifiers won't be present until you re-equip. A safe boot routine is to:

```csharp
StatBlock block = gameState.ActorStatsState.GetOrCreate("party.hero");

// 1. Drop any stale equipment-sourced modifiers from a prior session — keyed by
//    sourceKind "equipment" + the (slot, itemId) source id. Stat-block-derived stats
//    (level-up gains, status effects) are unaffected.
foreach ((string slot, string itemId) in gameState.EquipmentState.EquippedItems)
{
    block.RemoveModifiersBySource("equipment", EquipmentStatSource.Id(slot, itemId));
}

// 2. Re-apply each currently-equipped item's stat bonuses as fresh Flat modifiers.
foreach ((string slot, string itemId) in gameState.EquipmentState.EquippedItems)
{
    if (definitions.TryGetEquipment(itemId, out EquipmentDefinition gear))
    {
        string sourceId = EquipmentStatSource.Id(slot, itemId);
        foreach ((string statId, int bonus) in gear.StatBonuses)
        {
            block.AddModifier(new StatModifier(
                statId, StatModifierBucket.Flat, bonus,
                sourceKind: "equipment", sourceId));
        }
    }
}
```

The sample's `SeedHeroStatBlock` does exactly this.

## Events

| Event | When |
|---|---|
| `ItemEquippedEvent` | After a successful equip (includes prior item id if it replaced one) |
| `ItemUnequippedEvent` | After a successful unequip |

UI can listen for these to play swap animations or update the character sheet.

## Limitations (v1)

- **Single actor.** `EquipmentState` is keyed by slot, not by `(actorId, slot)`. The
  command API accepts an `actorId` to keep the future migration path clean, but in v1 you
  effectively get one equipment kit per game. Party support is on the roadmap.
- **No equip restrictions** by class, level, alignment, or stat. If you need them, gate
  the `EquipItemCommand` in your UI layer or add a wrapper command that checks first.

## See also

- [Stats](stats.md) — the modifier pipeline that equipment writes into
- [Economy](economy.md) — items live in the inventory bag before being equipped
