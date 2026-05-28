# Samples

This folder uses Unity's `Samples~` convention. The trailing `~` hides it from
Unity's asset pipeline so the contents are **not** compiled when the package is
installed — they're only copied into the consumer's project on demand via the
**Window → Package Manager → Moonforge Core → Samples → Import** UI.

Each sample lives in its own subfolder with its own `.asmdef`. Samples are
declared in [`package.json`](../package.json)'s `samples` array — the Package
Manager only surfaces samples that appear there.

## Samples in this folder

### Roguelike — shipped

Full 1:1 Unity port of [`samples/Moonforge.Sample.Console`](../../../samples/Moonforge.Sample.Console).
All twelve scenes (MainMenu, ClassSelect, Town, Dungeon, Battle, BattleSummary,
ContractNotice, ContractJournal, GearInventory, MetaShrine, BossReward,
Dialogue) drive the same `RoguelikeSession` the console sample uses, rendered
to a runtime-built `Tilemap` with the bundled Kenney 1-Bit Pack and a TMP HUD.
Hybrid mouse + keyboard input.

```
Roguelike/
  Roguelike.asmdef             ← Unity scripts, references Moonforge.Sample.Roguelike.Shared
  README.md                    ← setup + control reference
  Scripts/
    Bootstrap/RoguelikeBootstrap.cs    ← MonoBehaviour + IRoguelikeHost impl
    Input/PlayerInputAdapter.cs        ← KeyCode → PlayerAction
    Rendering/UnitySpriteCatalog.cs    ← loads sprites or generates placeholders
    Rendering/TileVisualKind.cs
  Shared/
    Roguelike.Shared.asmdef    ← noEngineReferences, also consumed by the console sample
    Content/                   ← id constants, dialogue, class/gear/meta-unlock data, BuildCatalog()
    Session/                   ← RoguelikeSession, IRoguelikeHost, snapshot records
    Scenes/SceneId.cs
    Input/PlayerAction.cs
    Rendering/                 ← MapRenderModel, BattleRenderModel, BattleLogEntry, MessageTone
    Persistence/               ← RoguelikeSaveStore + save migrations
    WorldGen/                  ← DungeonGenerator, EncounterGenerator, TownLayout
  Art/
    Source/                    ← Kenney 1-Bit Pack tilesheet (CC0) + LICENSE_kenney.txt
    Resources/Sprites/         ← sliced sprites (run slice-kenney.ps1 to populate)
    slice-kenney.ps1           ← crops named PNGs from the tilesheet
```

The `Shared/` folder is the **single source of truth** for the roguelike game
logic — it's compiled into `Moonforge.Sample.Roguelike.Shared.dll` for Unity
and into `Moonforge.Sample.Console.dll` for the console sample (via a
`<Compile>` glob in that sample's csproj). Adding a scene or changing logic
only touches `Shared/`; both samples pick it up automatically.

## Planned samples

- **MonsterCatcher** — Unity port of `samples/Moonforge.Sample.MonsterCatcher.Console`.
  Overworld tilemap, gym battles, party UI.
- **Minimal** — Tiny "hello, engine" scene that dispatches a single command and
  logs the resulting `GameState` — mirrors `samples/Moonforge.Sample.Minimal`.
