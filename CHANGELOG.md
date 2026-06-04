# Changelog

All notable changes to Moonforge (the `Moonforge.Core` NuGet package and the
`com.moonforge.core` Unity package — both ship from the same source) are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - Unreleased

### ⚠️ RNG stream change

`Pcg32RandomSource.NextInt` / `NextDouble` (and their `BattleRngState` counterparts)
produce **different values than 1.0.x for the same seed** — see Fixed below. The raw
`NextUInt32` stream is unchanged. If your game stores expected outcomes derived from
1.0.x rolls, regenerate them after upgrading.

### Added

- RNG stream position can now be persisted: `Pcg32RandomSource` exposes `State` /
  `Increment` and a `Restore(state, increment)` factory; `GameStateSnapshotMapper.Capture`
  has an overload that embeds the position in the snapshot, and `RestoreRandomSource`
  rebuilds it on load. Without this, determinism silently broke across save/load — a
  loaded game re-seeded its RNG from scratch instead of resuming the stream.
- Save schema **v8**: optional `rng` field on `GameStateSnapshot`. Pre-v8 saves load
  fine (the field is null; hosts fall back to their own seeding). The roguelike sample
  demonstrates the pattern, including the v7→v8 migration.

### Changed

- The Unity Roguelike sample's `Art/` folder now ships only the individual 16×16 sprites
  the sample uses (~5 KB) — the bundled Kenney spritesheets, pack extras, and the
  `slice-kenney.ps1` helper were removed (~735 KB). Each sprite is a single PNG in
  `Art/Resources/Sprites/`, replaceable by overwriting the file; attribution and links
  to the original CC0 packs live in `Art/LICENSE_kenney.txt`.
- Handlers that compose another module's handler (shops, crafting, loot, quest rewards,
  interactables, dialogue, battle rewards, quest auto-claim) now accept that handler as an
  optional `ICommandHandler<T>` constructor parameter, and
  `DefaultCommandDispatcher.RegisterBuiltIns` wires one shared instance through every
  composition site. Replacing a built-in handler now behaves consistently on composed and
  directly dispatched paths. Parameterless construction is unchanged. (The internal
  `BattleRuntime` singleton was removed as part of this.)

### Fixed

- `DomainError` gained an optional `Exception` property. When a command handler throws,
  the dispatcher's rollback path now attaches the full exception (stack trace, inner
  exceptions) instead of keeping only `ex.Message`, and the error message includes the
  exception type name. Expected domain failures still carry no exception.
- Removed the remaining unsorted dictionary iterations from gameplay paths: equipment
  bonus/granted-skill queries, quest auto-tracking, bestiary auto-tracking, battle XP
  grants, prevented-action status reporting, and the battle-ended HP snapshot now iterate
  in ordinal key order, so results and event ordering no longer depend on dictionary
  insertion order.
- `CommandDispatcher` now caps buffered events per dispatch
  (`MaxBufferedEventsPerDispatch`, default 1024). Reactors that publish events triggering
  each other previously looped forever; the transaction now fails with `InternalError` and
  rolls back cleanly.
- `NextInt(maxExclusive)` now uses rejection sampling instead of plain modulo, removing
  the slight bias toward low results for non-power-of-two bounds (e.g. d100 rolls).
- `NextDouble()` now returns values strictly inside `[0, 1)` — previously it could
  return exactly `1.0`, which made "guaranteed" probability checks (`roll < 1.0`)
  fail roughly once per 4 billion draws (reachable via craft success rolls).
- `GameStateSnapshotMapper.Apply` now restores `GameState.SchemaVersion` from the
  snapshot; loaded states previously reported the default version.

## [1.0.2] - 2026-05-28

### Changed

- Documentation and README improvements.

## [1.0.1] - 2026-05-28

### Added

- NuGet publish step in CI (tag-driven, `--skip-duplicate`).

### Fixed

- CI build.

## [1.0.0] - 2026-05-28

### Added

- Initial release: deterministic command/query + reactor engine with 18 gameplay
  modules, JSON persistence with schema migrations, Unity package + NuGet package from
  a single source tree, and three samples (Minimal, Roguelike console + Unity,
  MonsterCatcher console).

[1.1.0]: https://github.com/3583Bytes/moonforge-rpg-engine/compare/v1.0.2...HEAD
[1.0.2]: https://github.com/3583Bytes/moonforge-rpg-engine/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/3583Bytes/moonforge-rpg-engine/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/3583Bytes/moonforge-rpg-engine/releases/tag/v1.0.0
