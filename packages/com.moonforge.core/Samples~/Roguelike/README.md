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

The current build runs end-to-end but every scene is a placeholder that just
displays the scene name and the phase it will land in — you should see a
"Moonforge Roguelike — MainMenu" header and a body explaining the placeholder
state when you press Play.

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

### 4. Download and import a sprite tileset

The sample renders the world with real 2D sprites. The recommended free, CC0
tileset is Kenney's **1-Bit Pack** (or any equivalent monochrome roguelike
tileset):

- https://kenney.nl/assets/1-bit-pack

After downloading, place individual PNGs into `Art/Resources/Sprites/` inside
the imported sample folder, naming them per the table below. Unity auto-imports
them and writes `.meta` files. Sprites must be imported as **Sprite (2D and UI)**
with **Pixels Per Unit** set to match the cell size you want
(`RoguelikeBootstrap._cellSize` defaults to `1`, so set Pixels Per Unit to the
sprite's pixel width, e.g. `16` for a 16×16 tile).

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

Missing sprites won't break the sample — `UnitySpriteCatalog` logs a warning
for each missing kind and skips drawing them. You can start by dropping just
`hero.png`, `dungeon_floor.png`, and `dungeon_wall.png` to verify the pipeline
works, then fill in the rest later.

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
