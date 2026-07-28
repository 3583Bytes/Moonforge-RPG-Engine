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

- **Menus, dialogue, summaries** → mouse/touch *or* keyboard. Every clickable option shows its hotkey in square brackets (e.g. `[N] New run`, `[1] Knight`, `[Enter] Continue`). Click the button or press the highlighted key — both do the same thing. Every closable panel also has an **X** button in its top-right corner (mouse equivalent of `Esc` / return); the root main menu omits it.
- **Town / Dungeon** → on-screen D-pad (▲ ◀ ▶ ▼ bottom-right) *or* WASD/arrow keys. An action bar on the bottom-left lists the per-scene shortcuts (`E` interact / stairs, `J` journal, `I` gear, `B` buy potion, `S` sell herb, `T` town portal, `M` main menu).
- **Battle** → action bar buttons *or* keyboard. Attack (`A`), the class's abilities by name (`1`/`2`/`3` — e.g. a Ranger's Aimed Shot / Volley / First Aid), Potion (`P`), Retreat (`Q`).
- **Landmark interaction menus** (when you press `E` on a town landmark) → menu opens with numbered options. Press `1`/`2`/`3` or click the matching button.

### Full key reference

| Scene             | Keys                                                                           |
|-------------------|--------------------------------------------------------------------------------|
| Main Menu         | `N` new, `C` continue, `D` delete, `Q` quit                                    |
| Class Select      | `1` Knight, `2` Ranger, `3` Arcanist, `Esc` back                               |
| Town              | `WASD` move, `E` interact, `1`/`2`/`3` menu choice, `J` journal, `I` gear, `B` buy potion, `M` menu |
| Dungeon           | `WASD` move, `E` stairs, `J` journal, `I` gear, `T` town portal, `M` menu      |
| Battle            | `A` attack, `1`/`2`/`3` class abilities, `P` potion, `Q` retreat               |
| Battle Summary    | `1`/`2`/`3` boss reward (if offered), `Enter` continue                         |
| Contract Notice   | `Enter` continue, `Esc` dismiss                                                |
| Contract Journal  | `A` abandon active contract, `Enter` return                                    |
| Gear Inventory    | `1`–`6` toggle slot, `U` unequip all, `Enter` return                           |
| Meta Shrine       | `1`–`4` unlock perk, `Enter` return                                            |
| Boss Reward Chest | `1`/`2`/`3` choose reward                                                      |
| Dialogue          | `1`–`5` choose option, `Esc` step away                                         |

## Art

The sample is skinned with **[0x72's DungeonTileset II](https://0x72.itch.io/dungeontileset-ii) (CC0)** — a cohesive 16×16 dungeon set with fully **animated** characters. The individual frame PNGs ship in `Art/Resources/DungeonTilesetII/` (one file per frame, loaded by name); the CC0 license note lives alongside in `Art/DungeonTilesetII/LICENSE.txt`. Attribution is not required (public domain) but is provided as a courtesy.

Everything is data-driven from `UnitySpriteCatalog` + the frame names, so nothing here needs code changes to re-skin.

- **Animated characters.** Every actor plays a 4-frame **idle** loop, and a 4-frame **run** loop while sliding between cells. `DungeonSpriteAnimator` (a tiny `MonoBehaviour` on each actor) swaps frames each `Update`; the bootstrap sets `Running` from the movement tween and `FlipX` from facing / travel direction (the sheets face right, so left is a horizontal flip). Characters are taller than the 16×16 grid (16×28 / 16×23 / 32×36) and are imported with a **bottom-centre pivot** so they stand on the floor cell.
- **Autotiled walls.** `GetWallSprite` picks a face from the 4-neighbour floor mask: a wall bordering a room to the south shows a brick front (`wall_mid`), side walls use the vertical edge pieces (`wall_left` / `wall_right`), and bulk/back walls show a flat top (`wall_top_mid`).
- **Varied floors.** `GetFloorSprite` seeds a floor variant from the cell position so the ground reads as a worn surface instead of one repeated tile (dungeon draws from the full, partly-cracked `floor_1…8` set biased to the clean tile; town stays on `floor_1…4`).
- **Town ground.** The 0x72 set is dungeon-only, so the town uses two large seamless textures at the `Art/Resources/` root — `Grass.tga` and `Ground.tga`. Grass is a single **tiled ground plane** across the courtyard (seamless, not one texture crammed per cell). `Ground.tga` is a dirt crossroad through the centre (`Town Road Thickness` on the Bootstrap), painted **per-cell on walkable cells only** — each cell samples a continuous slice of the texture — so the road flows up to and around buildings instead of being drawn under them and hidden. Building walls, props, and characters stay 0x72. If `Grass.tga` is missing the town falls back to per-cell 0x72 floors.
- **Camera.** `Orthographic Size` defaults to **8** (≈16 cells tall) so the 16×16 art reads large. Tune it on the Bootstrap component.

If a frame is ever missing or fails to import, the catalog falls back to the procedural placeholder for that kind — the sample stays fully playable.

### Character → tileset mapping

Actors resolve to a tileset character in `UnitySpriteCatalog.ResolveCharacterId`. The hero keys off the selected class; enemies / NPCs key off a stable hash of their actor id, so a given entity always draws as the same character but the roster looks varied:

| Actor            | Tileset character(s)                                   |
|------------------|--------------------------------------------------------|
| Hero — Knight    | `knight_m`                                             |
| Hero — Ranger    | `elf_m`                                                |
| Hero — Arcanist  | `wizzard_m`                                            |
| Enemy (standard) | `goblin` / `skelet` / `imp` / `tiny_zombie` / `masked_orc` |
| Enemy (elite)    | `orc_warrior` / `chort` / `wogol` / `orc_shaman`       |
| Enemy (boss)     | `big_demon` / `ogre` / `big_zombie`                    |
| NPC              | `dwarf_m` / `dwarf_f` / `doc` / `pumpkin_dude`         |
| Town guard       | `knight_f`                                             |

### Landmark / tile mapping

Static tiles and town markers map to named frames in `UnitySpriteCatalog.SpriteNames` (markers) or the floor/wall resolvers:

| Kind                | Frame                        |
|---------------------|------------------------------|
| Dungeon floor       | `floor_1…8` (position-seeded) |
| Town ground         | `Grass.tga` field + `Ground.tga` crossroad (tiled planes) |
| Wall                | `wall_mid` / `wall_left` / `wall_right` / `wall_top_mid` (autotiled) |
| Stairs down / up    | `floor_stairs` / `floor_ladder` |
| Pillar              | `column`                     |
| Town door           | `doors_leaf_closed`          |
| Shop                | `crate`                      |
| Healer              | `flask_big_red`              |
| Alchemist           | `flask_big_green`            |
| Loot cache          | `chest_full_open_anim_f0`    |
| Fountain            | `wall_fountain_top_1`        |
| Quest board         | `wall_banner_blue`           |
| Meta-unlock shrine  | `wall_banner_yellow`         |

Inventory **weapon** icons use a tier-scaled tileset sword (`weapon_rusty_sword` → `weapon_regular_sword` → `weapon_knight_sword` → `weapon_golden_sword` for Common→Epic), shown in full colour with the rarity conveyed by the tier-coloured name. **Armor** and **accessory** keep their procedural shield / ring silhouettes tinted per tier — the tileset ships no armor or jewellery art, and a clean silhouette reads better than a mismatched prop.

### Want to swap in your own art?

Two paths, both covering animation:

- **Overwrite the PNGs** (easiest) — drop your own art over the files in `Art/Resources/DungeonTilesetII/`, keeping the same names (`knight_m_idle_anim_f0.png`, `floor_1.png`, `wall_mid.png`, …). Character frames can be taller than 16×16 (the knights are 16×28); keep the width on the 16px grid. The included `AssetPostprocessor` (`Scripts/Editor/RoguelikeSpriteImporter.cs`) auto-configures import settings (Sprite, Point filter, PPU 16, no compression, bottom-centre pivot for `_idle_anim` / `_run_anim` / `_hit_anim` frames).
- **Repoint the names** — edit `UnitySpriteCatalog` to point a kind at a different frame: `ResolveCharacterId` (which character each actor uses), `SpriteNames` (markers/props), `DungeonFloorVariants` / `GetWallSprite` (terrain), `WeaponTierSprites` (weapon icons). Any name resolves to `Art/Resources/DungeonTilesetII/<name>.png`.

(There's no per-kind Inspector override — the art is entirely name-driven through the catalog above.)

The session tracks `HeroFacing` (`Down`/`Up`/`Left`/`Right`) — it updates on every move input, including blocked ones, so bumping a wall still turns the hero. Left/Right flip the sprite horizontally; Up/Down reuse the right-facing sheet (the tileset has no dedicated back/front walk frames).

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
│   ├── Rendering/        UnitySpriteCatalog (tileset mapping + clip loading +
│   │                     procedural fallback), DungeonSpriteAnimator (idle/run
│   │                     frame player), TileVisualKind enum.
│   └── Editor/           AssetPostprocessor that auto-configures PNG import
│                         settings for the bundled sprite folders.
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

**Walls or actors render as plain colored shapes.** That's the procedural placeholder — the tileset frames under `Art/Resources/DungeonTilesetII/` didn't import as Sprites. Check they exist and re-import the folder.

**Sprites look tiny and blurry.** The `AssetPostprocessor` didn't run for one or more PNGs. In the Project window, select the offending PNG → Inspector → set **Texture Type = Sprite (2D and UI)**, **Filter Mode = Point**, **Pixels Per Unit = 16**, **Compression = None** → Apply. Character frames should also use **Pivot = Bottom** so they stand on the floor.

**Walking through a "phantom wall."** Toggle **Show Debug Overlay** on the Bootstrap component — red squares show exactly where the engine thinks walls are. If you're hitting a blocker that isn't red, file an issue with the hero's grid position and the marker list (printed in the HUD debug text).
