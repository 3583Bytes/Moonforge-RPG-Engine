# Roguelike (Unity sample)

A full Unity port of [`samples/Moonforge.Sample.Roguelike.Console`](../../../../samples/Moonforge.Sample.Roguelike.Console) — the deterministic class-based roguelike that exercises every Moonforge module (combat, quests, equipment, dialogue, crafting, shops, dungeon generation, save/load, meta-progression).

The Unity build is **the same game** as the console sample, just rendered through a `Tilemap` + `SpriteRenderer`s + a TextMeshPro UI instead of Spectre.Console. Walking around town, descending dungeons, turn-based battles with HP bars and damage numbers, town-landmark interactions, contracts, gear, the meta-shrine, boss rewards — all of it works end-to-end.

## Quick start

### 1. Open in Unity 2022.3 LTS (or newer)

The package declares `unity: 2022.3` in its manifest. The Package Manager refuses to import on older versions.

### 2. Install Moonforge Core

In your Unity project: **Window → Package Manager → + → Add package from git URL…** and use the URL from the package's top-level [README](../../README.md). Unity resolves three transitive UPM dependencies automatically:

- `com.unity.nuget.newtonsoft-json` (persistence layer uses Newtonsoft for JSON save/load)
- `com.unity.textmeshpro` (HUD + battle UI text)
- `com.unity.2d.tilemap` (floor rendering)

### 3. Import the Roguelike sample

In the Package Manager, select **Moonforge Core**, scroll down to the **Samples** section in the right pane, click **Import** next to **Roguelike**. Unity copies the sample to `Assets/Samples/Moonforge Core/<version>/Roguelike/`.

### 4. (One-time) Import TMP Essential Resources

On first launch Unity will pop a dialog. Click **Window → TextMeshPro → Import TMP Essential Resources → Import**. Required for the HUD fonts.

### 5. Set the input mode

The sample uses Unity's **legacy `Input.GetKeyDown`** for keyboard. If your project has only Unity's new Input System Package enabled, none of the keys will work. Fix: **Edit → Project Settings → Player → Active Input Handling** → **Both** (or **Input Manager (Old)**).

Mouse / touch input works regardless because the buttons use the `EventSystem`, which the bootstrap creates automatically.

### 6. Create the scene and play

The bootstrap assembles the entire scene at runtime — you just need one empty scene with one component on one GameObject:

1. **File → New Scene** → Basic (Built-in) → **Create**.
2. **File → Save As…** → `Assets/Samples/Moonforge Core/<version>/Roguelike/Scenes/Roguelike.unity`.
3. In the Hierarchy, right-click → **Create Empty**. Name it `Roguelike Bootstrap`.
4. With it selected, click **Add Component** and type `Roguelike Bootstrap`.
5. Press **Play**.

The Main Menu opens. Press `N` to start a new run, `1`/`2`/`3` to choose a class, and you're in the town.

## Controls

The sample uses a **hybrid input** approach so it works on desktop with keyboard + mouse and on mobile / touch with on-screen controls alone:

- **Menus, dialogue, summaries** → mouse/touch *or* keyboard. Every clickable option shows its hotkey in square brackets (e.g. `[N] New run`, `[1] Knight`, `[Enter] Continue`). Click the button or press the highlighted key — both do the same thing.
- **Town / Dungeon** → on-screen D-pad (▲ ◀ ▶ ▼ bottom-right) *or* WASD/arrow keys. An action bar on the bottom-left lists the per-scene shortcuts (`E` interact / stairs, `J` journal, `I` gear, `B` buy potion, `S` sell herb, `T` town portal, `M` main menu).
- **Battle** → action bar buttons *or* keyboard. Attack (`A`), Class skill 1 / 2 (`1`/`2`), Potion (`P`), Retreat (`Q`).
- **Landmark interaction menus** (when you press `E` on a town landmark) → menu opens with numbered options. Press `1`/`2`/`3` or click the matching button.

### Full key reference

| Scene             | Keys                                                                           |
|-------------------|--------------------------------------------------------------------------------|
| Main Menu         | `N` new, `C` continue, `D` delete, `Q` quit                                    |
| Class Select      | `1` Knight, `2` Ranger, `3` Arcanist, `Esc` back                               |
| Town              | `WASD` move, `E` interact, `1`/`2`/`3` menu choice, `J` journal, `I` gear, `B` buy potion, `M` menu |
| Dungeon           | `WASD` move, `E` stairs, `J` journal, `I` gear, `T` town portal, `M` menu      |
| Battle            | `A` attack, `1`/`2` class skill, `P` potion, `Q` retreat                       |
| Battle Summary    | `1`/`2`/`3` boss reward (if offered), `Enter` continue                         |
| Contract Notice   | `Enter` continue, `Esc` dismiss                                                |
| Contract Journal  | `A` abandon active contract, `Enter` return                                    |
| Gear Inventory    | `1`–`6` toggle slot, `U` unequip all, `Enter` return                           |
| Meta Shrine       | `1`–`4` unlock perk, `Enter` return                                            |
| Boss Reward Chest | `1`/`2`/`3` choose reward                                                      |
| Dialogue          | `1`–`5` choose option, `Esc` step away                                         |

## Art

The sample ships only the individual 16×16 PNGs it actually uses, in `Art/Resources/Sprites/` — one file per sprite, each replaceable by overwriting the PNG. They were cropped from two CC0 Kenney packs (see `Art/LICENSE_kenney.txt` for attribution); the source spritesheets are not bundled:

- **Floors and most environment props** come from the [Kenney Roguelike RPG Pack](https://kenney.nl/assets/roguelike-rpg-pack). Floor tiles (`town_floor.png`, `dungeon_floor.png`) are picked from the seamless-center cells of each ground-tile group so they tile cleanly. Doors, pillars, and town markers (fountain, shrine, shop, healer, alchemist, cache, quest board) come from the same pack's prop tiles.
- **Characters and the town guard marker** come from the [Kenney 1-Bit Pack](https://kenney.nl/assets/1-bit-pack). The Roguelike RPG Pack ships environment art only — no characters — so `hero.png`, `enemy.png`, `enemy_elite.png`, `enemy_boss.png`, `npc.png`, `marker_guard.png` stay on the 1-Bit Pack. Same for `stairs_down.png` / `stairs_up.png`, which the Roguelike RPG Pack doesn't have a clean single-tile equivalent for.
- **Walls** stay on the procedural path (`UnitySpriteCatalog.ApplyKindPattern` — wood-grain town walls, dark brick dungeon walls). Every wall tile in the Roguelike RPG Pack is a top-half-only sprite designed to layer with a separate wall-face cell below, so it doesn't work as a standalone single-cell wall in this game's grid model.

If a PNG is missing or fails to import, the catalog falls back to procedural placeholders for that kind — the sample remains fully playable with zero asset setup.

### Want to swap in your own art?

- Drop your PNG into `Art/Resources/Sprites/` with the corresponding filename below. The included `AssetPostprocessor` (`Scripts/Editor/RoguelikeSpriteImporter.cs`) auto-configures the texture import settings (Sprite, Point filter, PPU 16, no compression).
- To replace the floors, just overwrite `town_floor.png` / `dungeon_floor.png`.
- To enable wall PNGs (overriding the procedural walls), drop in `town_wall.png` and `dungeon_wall.png` and add the matching `TileVisualKind.TownWall` / `TileVisualKind.DungeonWall` entries to `SpriteNames` in `UnitySpriteCatalog.cs`.

### Per-class hero sprites

The three classes share `hero.png` by default, but each class can have its own look — no code changes needed. Resolution order (first match wins):

1. **Inspector** — the Roguelike Bootstrap's *Sprite Slots → Hero By Class* list: add an entry with the class id (`Knight`, `Ranger`, `Arcanist`) and drag in a Sprite.
2. **By filename** — drop `hero_knight.png`, `hero_ranger.png`, or `hero_arcanist.png` into `Art/Resources/Sprites/` (lowercase class id).
3. **Fallback** — `hero.png`, then the procedural placeholder.

The class sprite is used everywhere the hero appears: the exploration map and the battle portrait. If you add your own class to the `PlayerClass` enum, the same convention applies (`hero_<yourclass>.png`).

### Directional hero sprites

The hero can also change sprite based on the way they're facing. The session tracks `HeroFacing` (`Down`/`Up`/`Left`/`Right`) — it updates on every move input, including blocked ones, so bumping a wall still turns the character; it resets to `Down` (facing the camera) on new run and load.

All directional art is optional and layers on top of the class system. Resolution order, first match wins:

1. `hero_<classid>_<facing>.png` — e.g. `hero_knight_left.png` (or the matching Inspector slot in *Hero By Class*)
2. The mirrored side with flip — provide only `hero_knight_right.png` and Left renders it X-flipped, so one side sprite covers both directions
3. `hero_<classid>.png` — the class default
4. `hero_<facing>.png` — classless directional, e.g. `hero_left.png`
5. The mirrored classless side with flip
6. `hero.png` → procedural placeholder

The battle portrait always uses the front-facing (`Down`) resolution. Like everything else in `Art/Resources/Sprites/`, the importer auto-configures any PNGs you drop in.

| Sprite filename         | Used for                              |
|-------------------------|---------------------------------------|
| `hero.png`              | Player character (all classes' fallback) |
| `hero_knight.png`       | Knight hero (optional per-class)      |
| `hero_ranger.png`       | Ranger hero (optional per-class)      |
| `hero_arcanist.png`     | Arcanist hero (optional per-class)    |
| `enemy.png`             | Standard enemy                        |
| `enemy_elite.png`       | Elite enemy variant                   |
| `enemy_boss.png`        | Boss-tier enemy                       |
| `npc.png`               | Generic NPC                           |
| `marker_shop.png`       | Shop landmark (gold coins, Roguelike RPG) |
| `marker_healer.png`     | Healer landmark (green cross banner, Roguelike RPG) |
| `marker_alchemist.png`  | Alchemist landmark (purple herb, Roguelike RPG) |
| `marker_guard.png`      | Town guard (1-Bit Pack)               |
| `marker_cache.png`      | Loot cache (open chest, Roguelike RPG) |
| `marker_fountain.png`   | Fountain (Roguelike RPG)              |
| `marker_questboard.png` | Quest board (scroll, Roguelike RPG)   |
| `marker_shrine.png`     | Meta-unlock shrine (gothic monument, Roguelike RPG) |
| `stairs_down.png`       | Descend to next floor (1-Bit Pack)    |
| `stairs_up.png`         | Ascend to previous floor (1-Bit Pack) |
| `town_door.png`         | Building doorway (Roguelike RPG)      |
| `dungeon_pillar.png`    | Pillar inside dungeon rooms (Roguelike RPG) |
| `town_floor.png`        | Town ground tile (Roguelike RPG Pack) |
| `dungeon_floor.png`     | Dungeon ground tile (Roguelike RPG)   |

If you want more tiles from the original packs, download them from [kenney.nl](https://kenney.nl) (links above — both CC0), crop the 16×16 cells you want, and drop them into `Art/Resources/Sprites/` under the filenames above.

## Debug overlay

The Roguelike Bootstrap component has a **Show Debug Overlay** inspector field (off by default). Toggle it on to:

- Paint **red** quads over every cell the engine considers non-walkable.
- Paint a **green** quad over the cell the engine thinks the hero is on.
- Paint **orange** quads over each marker cell.
- Show a debug text block in the right-hand HUD listing hero grid position, the tile flags of the current cell, the four neighbour cells, and every marker's coordinates and tile flags.

Useful for diagnosing any mismatch between the visible sprite and the engine's logical position.

## How the source is organized

```
Samples~/Roguelike/
├── Scripts/
│   ├── Bootstrap/        RoguelikeBootstrap.cs — single MonoBehaviour that builds
│   │                     the Camera, Grid+Tilemap, Canvas, HUD, battle panel,
│   │                     and drives RoguelikeSession each Update.
│   ├── Input/            PlayerAction enum + PlayerInputAdapter (KeyCode polling).
│   ├── Rendering/        UnitySpriteCatalog (catalog of Sprites with procedural
│   │                     fallback), TileVisualKind enum.
│   └── Editor/           AssetPostprocessor that auto-configures PNG import
│                         settings for the bundled sprite folder.
└── Shared/               IRoguelikeHost + RoguelikeSession + render models +
                          WorldGen (TownLayout, DungeonGenerator, EncounterGenerator)
                          + RoguelikeContent + Persistence/RoguelikeSaveStore.
                          Compiled by BOTH the Unity asmdef AND the console
                          sample's csproj — single source of truth for game
                          logic; only the rendering layer differs between hosts.
```

## How the source is shared with the console sample

`Shared/` is the headless game. It exposes `IRoguelikeHost` (the rendering boundary) and `RoguelikeSession` (the state machine that drives gameplay). Both samples consume the same `Shared/` source:

- **Unity**: `Roguelike.Shared.asmdef` compiles `Shared/` into a Unity assembly. `RoguelikeBootstrap` implements `IRoguelikeHost`.
- **Console**: `samples/Moonforge.Sample.Roguelike.Console/Moonforge.Sample.Roguelike.Console.csproj` includes `Shared/**/*.cs` via a `<Compile>` glob. `GameLoop/RoguelikeGame.cs` implements `IRoguelikeHost` against Spectre.Console.

Changes to `Shared/` flow to both samples automatically.

## C# language version

Unity 2022.3 LTS uses **C# 9**. Both the engine's `Runtime/` and this sample's `Shared/` are written to stay within C# 9, so:

- Use block-scoped namespaces (`namespace Foo { ... }`), not file-scoped (`namespace Foo;`).
- No collection expressions (`[1, 2, 3]`); write `new[] { 1, 2, 3 }` or `new List<T> { ... }`.
- No `init`-only properties relying on `System.Runtime.CompilerServices.IsExternalInit`. Records are fine via the shim at `Shared/IsExternalInitShim.cs`.

The console sample is .NET 8 and could use newer C# features, but anything that ends up in `Shared/` has to stay C# 9-compatible.

## Troubleshooting

**No keys do anything.** Your project is on the new Input System Package only. Set Active Input Handling to *Both* (Step 5 above).

**Walls or actors render as plain colored shapes.** That's the procedural placeholder — drop the relevant PNG into `Art/Resources/Sprites/` to override.

**Sprites look tiny and blurry.** The `AssetPostprocessor` didn't run for one or more PNGs. In the Project window, select the offending PNG → Inspector → set **Texture Type = Sprite (2D and UI)**, **Filter Mode = Point**, **Pixels Per Unit = 16**, **Compression = None** → Apply.

**Walking through a "phantom wall."** Toggle **Show Debug Overlay** on the Bootstrap component — red squares show exactly where the engine thinks walls are. If you're hitting a blocker that isn't red, file an issue with the hero's grid position and the marker list (printed in the HUD debug text).
