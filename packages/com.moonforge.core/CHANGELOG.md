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
- Roguelike sample scaffolding under `Samples~/Roguelike/` — phase 1 ships an
  empty-scene bootstrap MonoBehaviour, input adapter, sprite catalog, and
  placeholder scene controllers. The sample is not yet listed in
  `package.json`'s `samples` array (still being ported scene-by-scene).
