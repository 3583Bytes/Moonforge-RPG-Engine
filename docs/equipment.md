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
    .AddItem(new ItemDefinition("item.gear.bronze_blade", maxStack: 1))
    .AddEquipment(new EquipmentDefinition(
        itemId: "item.gear.bronze_blade",
        slotId: "slot.weapon",
        statBonuses: new Dictionary<string, int>
        {
            [StandardEquipmentStats.Attack] = 4,
            [StandardEquipmentStats.Initiative] = 1
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
   `SourceKind = "equipment"`, `SourceId = "<slot>|<itemId>"`.

`UnequipItemCommand` reverses this: removes the modifiers, returns the item to inventory.

```csharp
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
foreach ((string slot, string itemId) in gameState.EquipmentState.EquippedItems)
{
    block.RemoveModifiersBySource("equipment", EquipmentStatSource.Id(slot, itemId));
}
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
