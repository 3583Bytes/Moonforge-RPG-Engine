using System;

namespace Moonforge.Core.Exploration;

[Flags]
public enum ExplorationTileFlags
{
    None = 0,
    Walkable = 1 << 0,
    BlocksLineOfSight = 1 << 1,
    EncounterAllowed = 1 << 2,
    Interactable = 1 << 3
}
