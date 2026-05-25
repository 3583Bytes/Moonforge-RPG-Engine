# Moonforge Core for Unity

Deterministic, modular RPG engine for Unity. The same engine that ships as the
`Moonforge.Core` NuGet package — repackaged for the Unity Package Manager so
your Unity project can consume it as source.

- **Determinism is a hard guarantee.** All gameplay logic is driven by a seeded
  PCG32 RNG (`IRandomSource`) and an explicit simulation clock (`IGameClock`).
  No `System.Random`, no `DateTime.Now`. Replaying the same inputs always
  produces the same `GameState`.
- **Pure C#, no UnityEngine references.** The runtime assembly is built with
  `noEngineReferences`, so the engine compiles unchanged on .NET console apps,
  servers, and Unity. Use Unity for input, rendering, and audio; let Moonforge
  own the model.
- **Command/Query + Reactor pipeline.** Every mutation goes through an
  `ICommand` dispatched against a single `GameState` aggregate. Reads go
  through queries. Cross-module reactions are wired with `IDomainEventReactor`
  so a failure anywhere rolls the whole transaction back atomically.

## Modules

Bestiary, Combat, Crafting, Dialogue, Economy, Encounters, Equipment, Evolution,
Exploration, Interactables, Inventory, Loot, Party, Persistence, Progression,
Quests, Shops, Stats, World.

## Requirements

- Unity 2022.3 LTS or newer.
- `com.unity.nuget.system.text-json` 2.0.2 (resolved automatically as a
  package dependency — used by the Persistence module for save/load).

## Installation

### Option A — Unity Package Manager via Git URL (recommended)

In your Unity project, open **Window → Package Manager → + → Install package
from git URL...** and paste:

```
https://github.com/3583Bytes/moonforge-rpg-engine.git?path=packages/com.moonforge.core
```

To pin to a specific release, append `#<tag>`:

```
https://github.com/3583Bytes/moonforge-rpg-engine.git?path=packages/com.moonforge.core#v1.0.0
```

### Option B — Local path (for development)

Clone the repo and reference the package by path in your project's
`Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.moonforge.core": "file:../../moonforge-rpg-engine/packages/com.moonforge.core"
  }
}
```

## Usage

The runtime assembly is named `Moonforge.Core`. From any of your own asmdefs,
add a reference to it and you can dispatch commands and read queries directly:

```csharp
using Moonforge.Core;
using Moonforge.Core.Runtime.Commands;

var dispatcher = DefaultCommandDispatcher.Create();
var state = new GameState();
var context = /* IRandomSource + IGameClock + IDomainEventSink + catalog */;

dispatcher.Dispatch(new SomeCommand(...), state, context);
```

See the documentation in the main repository (`docs/`) for the full architecture
overview, per-module guides, and the MonsterCatcher reference sample.

## License

MIT. See [LICENSE.md](LICENSE.md).
