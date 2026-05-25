# Changelog

All notable changes to the Moonforge Core Unity package are documented here.
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-05-25

### Added
- Initial Unity Package Manager release of the Moonforge deterministic RPG engine.
- Runtime assembly (`Moonforge.Core`) covering Bestiary, Combat, Crafting, Dialogue,
  Economy, Encounters, Equipment, Evolution, Exploration, Interactables, Inventory,
  Loot, Party, Persistence, Progression, Quests, Runtime (command/query + reactor
  pipeline), Shops, Stats, and World modules.
- `com.unity.nuget.system.text-json` declared as a package dependency so the
  Persistence module's JSON save/load works out of the box on Unity 2022.3 LTS.
- `com.unity.textmeshpro` and `com.unity.2d.tilemap` declared as dependencies
  so the bundled Roguelike sample imports without requiring users to install
  them by hand. Auto-resolved by UPM on package install.
- **Roguelike sample** at `Samples~/Roguelike/`, listed in `package.json`'s
  `samples` array and importable through the Package Manager:
  - Full 1:1 port of `samples/Moonforge.Sample.Console` — same game flow,
    same scenes (MainMenu, ClassSelect, Town, Dungeon, Battle,
    BattleSummary, ContractNotice, ContractJournal, GearInventory,
    MetaShrine, BossReward, Dialogue).
  - Headless `RoguelikeSession` + `IRoguelikeHost` boundary in `Shared/` is
    consumed verbatim by both the Unity sample (via `Roguelike.Shared.asmdef`)
    and the console sample (via csproj `<Compile>` glob) — zero duplication of
    game logic between samples.
  - `RoguelikeBootstrap` MonoBehaviour creates Camera, Grid, Tilemap, Canvas,
    and EventSystem at runtime; the user only needs an empty scene with the
    component on one GameObject.
  - Town and Dungeon scenes paint onto a `Tilemap` with hero/marker
    `SpriteRenderer`s, camera follows the hero. Other scenes render via a
    TMP HUD with click-or-keyboard menu buttons.
  - **Hybrid input** (desktop + mobile-friendly): menu/modal/dialogue scenes
    use clickable buttons that mirror their keyboard hotkeys. Town and Dungeon
    show an on-screen D-pad (bottom-right) plus an action bar (bottom-left)
    with the per-scene commands. Battle shows the action bar with
    Attack / Class skill 1 / Class skill 2 / Potion / Retreat. Every
    on-screen button has a matching keyboard hotkey; either input works.
  - **Kenney 1-Bit Pack** (CC0) bundled at `Art/Source/`; an `Art/slice-kenney.ps1`
    helper crops named sprites out of the tilesheet into `Art/Resources/Sprites/`.
    `UnitySpriteCatalog` falls back to runtime-generated coloured placeholders
    for any sprite that isn't on disk, so the sample looks alive on first
    Play even with zero asset setup.
