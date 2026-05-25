# Samples

This folder uses Unity's `Samples~` convention. The trailing `~` hides it from
Unity's asset pipeline so the contents are **not** compiled when the package is
installed — they're only copied into the consumer's project on demand via the
**Window → Package Manager → Moonforge Core → Samples → Import** UI.

Each sample is a self-contained subfolder with its own `.asmdef` so it can be
imported and built independently:

```
Samples~/
  Roguelike/
    Roguelike.asmdef
    Scenes/
    Scripts/
    README.md
  MonsterCatcher/
    ...
```

Once a sample has a working scene + scripts, declare it in the package manifest
so the Package Manager surfaces it as an importable item:

```jsonc
// packages/com.moonforge.core/package.json
"samples": [
  {
    "displayName": "Roguelike",
    "description": "Port of the Moonforge.Sample.Console roguelike to Unity.",
    "path": "Samples~/Roguelike"
  }
]
```

Until a sample is declared in `package.json`, Unity will not show it in the
importer even though its files live here. Adding the entry is the gate that
flips a sample from "in progress" to "shipped".

## Samples in this folder

- **Roguelike** — In-progress 1:1 Unity port of `samples/Moonforge.Sample.Console`.
  Phase 1 (asmdef, bootstrap MonoBehaviour, input adapter, sprite catalog,
  placeholder scene controllers) has landed; subsequent phases port one scene
  at a time. See [`Roguelike/README.md`](Roguelike/README.md) for the phase
  table and setup walkthrough.

## Planned samples

- **MonsterCatcher** — Unity port of `samples/Moonforge.Sample.MonsterCatcher.Console`.
  Overworld tilemap, gym battles, party UI.
- **Minimal** — Tiny "hello, engine" scene that dispatches a single command and
  logs the resulting `GameState` — mirrors `samples/Moonforge.Sample.Minimal`.
