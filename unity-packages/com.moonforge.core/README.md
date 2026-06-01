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

- Unity **2022.3 LTS** or newer.
- The Package Manager auto-resolves three transitive UPM dependencies:
  - `com.unity.nuget.newtonsoft-json` (Persistence module's JSON save/load).
  - `com.unity.textmeshpro` (used by the Roguelike sample's HUD).
  - `com.unity.2d.tilemap` (used by the Roguelike sample's floor rendering).

## Installation

Setup has **two steps**: first install the package itself, then go back into
the Package Manager and import the Roguelike sample from the package's
Samples list.

### Step 1 — Install the Moonforge Core package

Pick whichever fits your workflow.

**Option A: Git URL (recommended).** In your Unity project, open
**Window → Package Manager → + → Install package from git URL...** and paste:

```
https://github.com/3583Bytes/moonforge-rpg-engine.git?path=unity-packages/com.moonforge.core
```

To pin to a specific release, append `#<tag>`:

```
https://github.com/3583Bytes/moonforge-rpg-engine.git?path=unity-packages/com.moonforge.core#v1.0.2
```

**Option B: Local path (for development).** Clone the repo and reference the
package by path in your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.moonforge.core": "file:../../moonforge-rpg-engine/unity-packages/com.moonforge.core"
  }
}
```

Either way, Unity resolves the three transitive UPM dependencies
(`com.unity.nuget.newtonsoft-json`, `com.unity.textmeshpro`,
`com.unity.2d.tilemap`) automatically.

After this step, the package shows up in your Unity Console as installed but
**no sample is in your `Assets/` folder yet**. The Roguelike sample lives in
the package's hidden `Samples~/` folder and has to be imported explicitly —
that's Step 2.

### Step 2 — Import the Roguelike sample

The package ships a full Unity sample: a deterministic class-based roguelike
that exercises every engine module (combat, quests, dialogue, equipment,
shops, dungeon generation, save/load, meta-progression).

**Go back to the Package Manager you used in Step 1** — it stays open by
default after installing — and follow these clicks:

1. In the Package Manager's left-hand package list, select **Moonforge Core**
   (it's now present because Step 1 installed it).
2. In the right-hand detail pane, scroll past **Description**, **Dependencies**,
   and **Version History** until you find the **Samples** section near the
   bottom.
3. Next to **Roguelike**, click the **Import** button.
4. Unity copies the sample into
   `Assets/Samples/Moonforge Core/<version>/Roguelike/`. You'll see it appear
   in your Project window.

If you don't see a **Samples** section in the right pane, make sure
**Moonforge Core** is still selected on the left and that the right pane is
scrolled all the way down — the section is below Version History.

### Step 3 — One-time Unity setup

These two clicks only need to happen once per project:

- **TMP Essentials**: on first launch Unity will prompt you with
  **Window → TextMeshPro → Import TMP Essential Resources**. Click **Import**.
  Required for the HUD fonts to render.
- **Input mode**: the sample uses Unity's legacy `Input.GetKeyDown`. If your
  project is set to the new Input System Package only, no keyboard input
  works. Fix: **Edit → Project Settings → Player → Active Input Handling**
  → **Both** (or **Input Manager (Old)**). Mouse / touch input works
  regardless.

### Step 4 — Create the scene and play

The Roguelike bootstrap assembles its scene at runtime — you only need an
empty scene with one component on one GameObject:

1. **File → New Scene** (Basic Built-in) → save anywhere in your project
   (for example, `Assets/Samples/Moonforge Core/<version>/Roguelike/Scenes/Roguelike.unity`).
2. In the Hierarchy, right-click → **Create Empty**. Name it
   `Roguelike Bootstrap`.
3. With it selected, **Add Component → Roguelike Bootstrap**.
4. Press **Play**.

The Main Menu opens. Press `N` for a new run, `1` / `2` / `3` to pick a class,
then `W` / `A` / `S` / `D` to move and `E` to interact with town landmarks or
use dungeon stairs.

Full controls, troubleshooting, and customization options are in the sample's
own README at
`Assets/Samples/Moonforge Core/<version>/Roguelike/README.md`.

## Usage in your own code

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
