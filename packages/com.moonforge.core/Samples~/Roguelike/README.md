# Roguelike (Unity port)

A faithful Unity port of [`samples/Moonforge.Sample.Console`](../../../../samples/Moonforge.Sample.Console),
the deterministic class-based roguelike that exercises every Moonforge module
(combat, quests, equipment, dialogue, crafting, shops, dungeon gen, save/load,
meta-progression).

The port is being delivered in phases. Each phase replaces one or more
`PlaceholderSceneController` bindings in
[`RoguelikeBootstrap.BuildSceneRegistry`](Scripts/Bootstrap/RoguelikeBootstrap.cs)
with a real controller.

| Phase | Scope                                                                                | Status |
|------:|--------------------------------------------------------------------------------------|--------|
| 1     | Folder layout, `asmdef`, bootstrap MonoBehaviour, input adapter, sprite catalog      | ✅      |
| 2     | Town, Dungeon, Battle scenes (with `TownLayout`, `DungeonGenerator`, `EncounterGenerator`, save store) | pending |
| 3     | MainMenu, ClassSelect, Journal, Gear, MetaShrine                                      | pending |
| 4     | BattleSummary, ContractNotice, BossReward, Dialogue                                  | pending |

The current build is the full roguelike running end-to-end inside Unity —
same game logic as the console sample, just rendered through a Tilemap and a
TextMeshPro HUD instead of Spectre.Console.

## Controls

The sample takes a **hybrid input** approach:

- **Menus, dialogue, summaries → mouse or keyboard.** Every clickable option
  also shows its hotkey in square brackets (e.g. `[N]  New run`,
  `[1]  Knight`, `[Enter]  Continue`). Click the button or press the
  highlighted key — both do the same thing.
- **Town and Dungeon movement → keyboard only.** Movement is one tile per
  keypress (turn-based), so click-to-pathfind would be a different feature
  rather than a free addition.
- **Battle → keyboard only.** Single-letter commands (`A` attack, `1`/`2`
  class skills, `P` potion, `Q` retreat) match the console sample's flow.

### Key reference

| Scene             | Keys                                                                           |
|-------------------|--------------------------------------------------------------------------------|
| Main Menu         | `N` new, `C` continue, `D` delete, `Q` quit                                    |
| Class Select      | `1` Knight, `2` Ranger, `3` Arcanist, `Esc` back                               |
| Town              | `WASD` move, `E` interact, `J` journal, `I` gear, `B` buy potion, `M` menu     |
| Dungeon           | `WASD` move, `E` stairs, `J` journal, `I` gear, `T` town portal, `M` menu      |
| Battle            | `A` attack, `1`/`2` class skill, `P` potion, `Q` retreat                       |
| Battle Summary    | `1`/`2`/`3` boss reward (if offered), `Enter` continue                         |
| Contract Notice   | `Enter` continue, `Esc` dismiss                                                |
| Contract Journal  | `A` abandon active contract, `Enter` return                                    |
| Gear Inventory    | `1`–`6` toggle slot, `U` unequip all, `Enter` return                           |
| Meta Shrine       | `1`–`4` unlock perk, `Enter` return                                            |
| Boss Reward Chest | `1`/`2`/`3` choose reward                                                      |
| Dialogue          | `1`–`5` choose option, `Esc` step away                                         |

### Input System note

The default input adapter uses Unity's **legacy `Input.GetKeyDown`**. If your
project is set to Unity's new Input System Package, no keys will register —
switch **Project Settings → Player → Active Input Handling** to **Both** (or
**Input Manager (Old)**). Mouse clicks work in either mode because the menu
buttons use the `EventSystem`, which the bootstrap creates automatically if
your scene doesn't already have one.

## Setup

### 1. Open Unity 2022.3 LTS

Tested against Unity **2022.3 LTS**. The package's `package.json` declares the
Unity version requirement; the Package Manager will refuse to import the sample
into an older project.

### 2. Import the package and the sample

In your project's Package Manager, install **Moonforge Core** via the Git URL
pinned in the package's top-level [README](../../README.md). Then expand the
**Samples** section of the package detail pane and click **Import** next to
**Roguelike**. Unity copies this folder into `Assets/Samples/Moonforge Core/<version>/Roguelike/`.

If you don't see the sample listed yet, check the top-level `package.json` —
samples are declared there explicitly, and the Roguelike entry will appear once
Phase 5 ships.

### 3. (One-time) Import TMP Essentials

The Package Manager auto-resolves every UPM dependency the sample needs:
`com.unity.nuget.system.text-json`, `com.unity.textmeshpro`, and
`com.unity.2d.tilemap` are all declared in Moonforge Core's `package.json`
and install automatically when you take the package.

The one piece UPM can't do for you is import the **TMP Essentials Resources**
(font assets, shaders). On first launch in a fresh project Unity prompts with
**Window → TextMeshPro → Import TMP Essential Resources** — click **Import**
and that's it.

### 4. Sprites — the easy path or the manual path

The sample renders the Town and Dungeon scenes through a runtime-built
`Tilemap` plus per-entity `SpriteRenderer`s. **You don't need to do anything
on first play** — `UnitySpriteCatalog` falls back to coloured procedural
placeholders for every tile kind, so the game looks alive on first run with
zero asset setup.

If you want real pixel-art sprites, the sample ships with the **Kenney 1-Bit
Pack** (CC0 license — see `Art/Source/LICENSE_kenney.txt`) and a PowerShell
slicing helper:

```powershell
# From the imported sample folder in your Assets:
./Art/slice-kenney.ps1
```

The script crops 21 individual 16×16 PNGs out of the bundled tilesheet at
`Art/Source/kenney_1bit_colored-packed.png` and writes them to
`Art/Resources/Sprites/` with the names `UnitySpriteCatalog` looks up
(`hero.png`, `enemy.png`, `dungeon_floor.png`, etc.). Unity auto-imports each
new PNG and the catalog picks them up by name — coloured placeholders stop
being used for any kind that now has a real sprite.

**The positions I picked are best-guesses.** Some land in the right region of
the sheet (characters, enemies, doors, the alchemist flask) and some don't
(stairs, shop, cache, fountain — these crop near visually-similar but
semantically-wrong tiles). To fix a wrong sprite:

1. Open `Art/Source/kenney_1bit_colored-packed.png` in any image viewer.
2. The sheet is a 49×22 grid of 16×16 tiles, packed with no spacing. Tile
   `(col, row)` lives at pixel `(col*16, row*16)`.
3. Find the tile you want and note its `(col, row)`.
4. Edit the `$tiles` table at the top of `Art/slice-kenney.ps1`.
5. Re-run the script.

The runtime catalog also accepts hand-authored PNGs — drop any
`<name>.png` into `Art/Resources/Sprites/` and the catalog will pick it up
instead of the placeholder, regardless of whether you used the slicer.

| Sprite name             | Used for                              | Suggested Kenney tile  |
|-------------------------|---------------------------------------|------------------------|
| `dungeon_floor.png`     | Walkable dungeon tile                 | floor / cobblestone    |
| `dungeon_wall.png`      | Dungeon wall                          | brick wall             |
| `dungeon_pillar.png`    | Non-walkable pillar inside rooms      | pillar / column        |
| `stairs_down.png`       | Descend to next floor                 | stairs (down arrow)    |
| `stairs_up.png`         | Ascend to town / previous floor       | stairs (up arrow)      |
| `town_floor.png`        | Walkable town tile                    | grass / paving         |
| `town_wall.png`         | Town wall / building exterior         | wood wall              |
| `town_door.png`         | Building door                         | door                   |
| `marker_shop.png`       | Shop landmark                         | crate / barrel         |
| `marker_healer.png`     | Healer landmark                       | cross / heart icon     |
| `marker_alchemist.png`  | Alchemist landmark                    | potion bottle          |
| `marker_guard.png`      | Town guard NPC tile                   | guard / soldier        |
| `marker_cache.png`      | Loot cache interactable               | chest                  |
| `marker_fountain.png`   | Fountain interactable                 | fountain               |
| `marker_questboard.png` | Quest board                           | sign / scroll          |
| `marker_shrine.png`     | Meta-unlock shrine                    | shrine / pedestal      |
| `hero.png`              | Player character                      | knight / hero          |
| `enemy.png`             | Standard enemy                        | goblin / skeleton      |
| `enemy_elite.png`       | Elite enemy variant                   | armored goblin         |
| `enemy_boss.png`        | Boss-tier enemy                       | dragon / large boss    |
| `npc.png`               | Generic NPC (alchemist, healer, etc.) | civilian / merchant    |

| Sprite name             | Used for                              | Suggested Kenney tile  |
|-------------------------|---------------------------------------|------------------------|
| `dungeon_floor.png`     | Walkable dungeon tile                 | floor / cobblestone    |
| `dungeon_wall.png`      | Dungeon wall                          | brick wall             |
| `dungeon_pillar.png`    | Non-walkable pillar inside rooms      | pillar / column        |
| `stairs_down.png`       | Descend to next floor                 | stairs (down arrow)    |
| `stairs_up.png`         | Ascend to town / previous floor       | stairs (up arrow)      |
| `town_floor.png`        | Walkable town tile                    | grass / paving         |
| `town_wall.png`         | Town wall / building exterior         | wood wall              |
| `town_door.png`         | Building door                         | door                   |
| `marker_shop.png`       | Shop landmark                         | crate / barrel         |
| `marker_healer.png`     | Healer landmark                       | cross / heart icon     |
| `marker_alchemist.png`  | Alchemist landmark                    | potion bottle          |
| `marker_guard.png`      | Town guard NPC tile                   | guard / soldier        |
| `marker_cache.png`      | Loot cache interactable               | chest                  |
| `marker_fountain.png`   | Fountain interactable                 | fountain               |
| `marker_questboard.png` | Quest board                           | sign / scroll          |
| `marker_shrine.png`     | Meta-unlock shrine                    | shrine / pedestal      |
| `hero.png`              | Player character                      | knight / hero          |
| `enemy.png`             | Standard enemy                        | goblin / skeleton      |
| `enemy_elite.png`       | Elite enemy variant                   | armored goblin         |
| `enemy_boss.png`        | Boss-tier enemy                       | dragon / large boss    |
| `npc.png`               | Generic NPC (alchemist, healer, etc.) | civilian / merchant    |

Missing sprites won't break the sample — `UnitySpriteCatalog` generates a
coloured placeholder for any kind whose `<name>.png` isn't on disk, so the
game runs even with zero sprite assets.

### 5. Create the scene

The sample assembles its own Unity scene at runtime — you only need to create
an empty scene and drop one component on one GameObject:

1. **File → New Scene** (Empty (Built-in)).
2. Save as `Assets/Samples/Moonforge Core/<version>/Roguelike/Scenes/Roguelike.unity`.
3. In the Hierarchy, **Create Empty** → name it `Roguelike Bootstrap`.
4. **Add Component → Roguelike Bootstrap** on that GameObject.
5. (Optional) In the Inspector, adjust `Cell Size`, `Orthographic Size`,
   `Background Color`, and `Starting Scene` to taste.
6. **File → Save**, then press **Play**.

You should see the placeholder HUD render. Esc quits play mode.

## Architecture

The sample is structured around an `ISceneController` state machine driven by
`RoguelikeBootstrap`. Each console-sample scene maps to a Unity scene
controller; transitions go through `SceneTransition.To(SceneId)` and the
bootstrap swaps controllers atomically (Exit → Enter).

```
RoguelikeBootstrap (MonoBehaviour)
├── Awake: builds Camera + Grid+Tilemap + Canvas + HUD at runtime
├── Update: polls PlayerInputAdapter → ISceneController.Tick → handles SceneTransition
└── SceneContext: shared bag of Unity refs handed to every scene controller
```

| Layer        | Folder                       | Notes                                                              |
|--------------|------------------------------|--------------------------------------------------------------------|
| Bootstrap    | `Scripts/Bootstrap/`         | `RoguelikeBootstrap`, `SceneContext`, `ISceneController`, routing  |
| Input        | `Scripts/Input/`             | `PlayerAction` enum + `PlayerInputAdapter` (KeyCode polling)       |
| Rendering    | `Scripts/Rendering/`         | `UnitySpriteCatalog`, `TileVisualKind`                             |
| Scenes       | `Scripts/Scenes/`            | One controller per `SceneId`; placeholders today                   |
| Engine glue  | `Scripts/Engine/` (Phase 2+) | Ports of `WorldGen/`, `Persistence/`, headless run state           |
| Sprite art   | `Art/Resources/Sprites/`     | PNGs loaded via `Resources.Load<Sprite>("Sprites/<name>")`         |

## Why is the source duplicated from the console sample?

Unity packages convention says each sample under `Samples~/` is self-contained
so users can import one without inheriting unrelated code. The headless logic
shared between the console and Unity samples (`DungeonGenerator`,
`TownLayout`, `EncounterGenerator`, `RoguelikeSaveStore`, plus the run-state
plumbing currently inside `GameLoop/RoguelikeGame.cs`) is being **duplicated**
into `Scripts/Engine/` as each scene that needs it is ported. The console
sample remains the canonical reference; the Unity copies are kept in sync by
hand during port phases. If the duplication ever becomes painful enough to
matter, we can revisit by extracting a shared `Moonforge.Sample.Roguelike.Core`
library, but that's deliberately deferred.

## C# version note

Unity 2022.3 LTS uses C# 9. The console sample (which targets .NET 8) uses
C# 12 features such as collection expressions (`new[] { ... }` written as
`[...]`). When porting files into this sample, those have to be converted by
hand — usually `[a, b, c]` becomes `new[] { a, b, c }` or
`new List<T> { a, b, c }` depending on the target type.
