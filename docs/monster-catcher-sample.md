# Monster Catcher sample

`samples/Moonforge.Sample.MonsterCatcher.Console` is a small Pokemon-style game built
end-to-end on the engine. Pick a starter, walk a procedurally-generated world from west
to east, fight wild monsters, clear eight gym leaders, manage your bag, and end at a
Champion battle.

This doc is a tour of how the game is wired — what modules it consumes, how the world is
generated, where the load-bearing seams are. For the per-module APIs themselves, read the
module-specific docs.

## What it exercises

The game is the reference end-to-end consumer for the monster-catcher feature stack
([party](party.md), [evolution](evolution.md), [bestiary](bestiary.md), plus the
type-chart / capture / swap / PP sections of [combat](combat.md)). It also pulls in:

- [Quests](quests.md) — the "Defeat the Eight Wardens" main quest with one Kill
  objective of required count 8. Each gym victory emits a quest signal; the engine's
  `QuestObjectiveTrackingReactor` advances the count and auto-completes the quest.
- [Shops](shops.md) + [Economy](economy.md) + [Inventory](economy.md#inventory) — every
  gym town has a shop selling three capture-ball tiers (Pokeball, Greatball, Ultraball),
  three healing tiers (Potion, Super Potion, Max Potion), and Revive. Gold drops from
  wild wins (`StartBattleCommand.rewardCurrency`) and gym wins
  (`GrantCurrencyCommand`).
- [Exploration](architecture.md) — the engine's tile-flag map is reconfigured on every
  screen transition (`ConfigureExplorationMapCommand` + `UpsertExplorationActorCommand`)
  so walkability and engine-side actor position track the player.
- [Progression](progression.md) — wild and gym mons configured with the shared monster
  experience curve; XP rewards on victory; the engine's level-up reactor triggers
  evolution at the species' `RequiredLevel`.

## Game flow

1. **Title screen** — choose one of three starters (Splashling, Emberkin, Sproutling),
   each starts at level 8 so they're robust enough for the early route.
2. **Walk east** through procedurally-generated screens. Tall grass tiles trigger wild
   encounters; the corridor row is sparsely sprinkled with grass so a player walking
   straight east hits a fight or two per screen.
3. **Gym screens** at every 5th screen — small towns with a heal pad, shop counter, and
   gym leader's mat. A guard NPC blocks east passage until the leader is defeated.
   Gym leader battles are multi-monster sub-battle chains; HP and PP persist across the
   chain.
4. **Champion's Hall** (screen 45) — gated behind eight badges. Auriel, the Reigning
   Champion, fields a five-monster team of evolved starters and rare wilds. Beating her
   ends the game with a real Victory.

A party wipe doesn't end the game (until the 5-faint ceiling): the player respawns at
the last visited heal pad, fully healed, minus 25% of their gold.

## World structure

| Screen index | Display | Biome / contents |
|---|---|---|
| 0 (`Screen 1`) | spawn | natural biome — guaranteed heal pad |
| 1–3 | wild | natural biome |
| 4 (`Screen 5`) | town | **Gym 1 — Brell, Stone Warden (Rock)** |
| 5–8 | wild | natural biome |
| 9 (`Screen 10`) | town | **Gym 2 — Naia, Tide Caller (Water)** |
| 14 | town | **Gym 3 — Mara, Whisper-Speaker (Ghost)** |
| 19 | town | **Gym 4 — Volt, Stormbringer (Electric)** |
| 24 | town | **Gym 5 — Garth, Quake Speaker (Ground)** |
| 29 | town | **Gym 6 — Cael, Sky-Rider (Flying)** |
| 34 | town | **Gym 7 — Sera, Ember Keeper (Fire)** |
| 39 | town | **Gym 8 — Vexar, Shadow Champion (mixed)** |
| 44 (`Screen 45`) | Champion's Hall | **Auriel, the Reigning Champion** |

Natural biomes (Plains, Forest, Cave, Highlands, Beach, Marsh) are rolled per-screen from
a band-weighted table — the bands shift toward gnarlier biomes as the player walks east.
Each non-town screen has a ~35% chance of a heal pad pinned to the corridor row; screen
0 always gets one.

## Procedural world generation

Lives in `WorldGen/`.

- **`BiomeKind` + `BiomeProfile`** — per-biome tile palette (wall density, water density,
  grass fill), encounter rate, and wild monster pool.
- **`WorldScreen`** — one screen's tile grid (40×14) plus per-screen mutable state
  (which heal pads have been used, whether the gym is cleared, whether the east gate is
  open). Cached in `World` so re-entering a screen returns the same instance.
- **`WorldScreenGenerator`** — deterministic per-screen gen seeded from
  `(worldSeed, screenIndex)`. Town and Champion biomes use fixed-layout generators;
  natural biomes carve a straight 3-tile-tall corridor at the middle row with biome
  noise around it.
- **`World`** — owns the screen cache and the biome schedule. Gym screens are always
  `BiomeKind.Town` at every 5th index; the Champion's Hall is `BiomeKind.Champion` at
  the world's last index.

## HP across battles

The engine sets `gameState.ActiveBattle = null` the moment a battle's status leaves
`Active` (inside the command handler, before the dispatcher returns), so reading
`ActiveBattle.Actors[id].Hp` after a battle ends always sees `null`. To survive that,
`BattleEndedEvent` carries a `FinalActorHp` snapshot of every actor still in the battle
at the moment of close — the sample copies it into `_currentHpByActor` from
`NarrateOne`'s `BattleEndedEvent` case:

```csharp
case BattleEndedEvent end:
    foreach ((string actorId, int hp) in end.FinalActorHp)
    {
        _currentHpByActor[actorId] = hp;
    }
    // ...narrate Victory / Defeat
```

Party-wipe detection reads from `_currentHpByActor`, not from the engine — that's the
load-bearing seam. When starting the next battle, the sample overrides the engine's
default-full HP with the tracked HP so cumulative damage persists across screens and gym
sub-battles:

```csharp
DispatchOrThrow(startCmd);
_gameState.ActiveBattle!.Actors[activeMon.ActorId].Hp = _currentHpByActor[activeMon.ActorId];
```

## Starter evolution at creation

`ConfigureActorProgressionCommand` sets level directly without firing `LevelUpEvent`, so
configuring a fresh actor at level 8 wouldn't trigger the evolution reactor — Splashling
would stay Splashling at level 15. The sample instead configures the actor at level 1,
then dispatches `GrantExperienceCommand` with the XP-floor for the target level. The
grant fires `LevelUpEvent` for every level crossed, which the evolution reactor
listens to:

```csharp
DispatchOrThrow(new ConfigureActorProgressionCommand(actorId, curveId, level: 1, xp: 0));
DispatchOrThrow(new ConfigureActorEvolutionsCommand(actorId, ...));  // before XP grant!
DispatchOrThrow(new GrantExperienceCommand(actorId, XpFloorForLevel(8)));
// ...drain the sink to apply evolution + learnset events
```

Evolution eligibility has to be configured *before* the XP grant so the reactor sees it
during the level-up cascade.

## File layout

```
samples/Moonforge.Sample.MonsterCatcher.Console/
├── Program.cs                          entry point (seed CLI arg)
├── GameLoop/MonsterCatcherGame.cs      main orchestrator — world tick + battle loops
├── WorldGen/
│   ├── BiomeKind.cs                    biome enum
│   ├── BiomeProfile.cs                 per-biome tile + monster registry
│   ├── WorldScreen.cs                  one screen — tiles + mutable state
│   ├── WorldScreenGenerator.cs         deterministic per-screen gen
│   └── World.cs                        screen cache + biome schedule
├── Content/
│   ├── ContentCatalog.cs               registers everything in InMemoryGameDefinitionCatalog
│   ├── TypeIds.cs                      damage type ids
│   ├── MonsterSpecies.cs               starters + wild + evolved species
│   ├── Moves.cs                        BattleSkillDefinition catalog
│   ├── EvolutionIds.cs                 evolution path ids
│   ├── BadgeIds.cs                     8 gym badges
│   ├── GymLeaders.cs                   8 themed gym leaders + rosters
│   ├── Champion.cs                     Auriel's 5-mon team
│   ├── ItemIds.cs                      ball / heal / revive ids
│   ├── Items.cs                        item definitions + use effects
│   ├── TownShop.cs                     shop catalog + starting inventory
│   └── MainQuest.cs                    "Defeat the Eight Wardens"
└── Rendering/Ui.cs                     Spectre.Console wrapper (HP bars, prompts, map)
```

## Run

```bash
dotnet run --project samples/Moonforge.Sample.MonsterCatcher.Console
dotnet run --project samples/Moonforge.Sample.MonsterCatcher.Console 4242   # seed
```

Move with arrow keys / WASD. Q quits. The HUD shows zone, biome, badge count, gold, and
quest progress.

## Test

`tests/Moonforge.Sample.MonsterCatcher.Console.Tests/MonsterCatcherSmokeTests.cs` drives
the game in headless mode (`Console.IsInputRedirected == true`) — `ChooseOption` always
picks option 0 and `PressEnter` returns immediately. The test confirms the loop
terminates with either Victory or Defeat (not Quit) and that all wired modules cooperate
end-to-end without throwing.
