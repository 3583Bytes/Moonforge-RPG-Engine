using System;
using System.Collections.Generic;
using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Exploration.Commands
{

    /// <summary>
    /// Activates an already-configured exploration map, optionally carrying actors (e.g.
    /// the player) over to spawn positions on the target map. Actors that are not carried
    /// stay on their current map and are restored when it becomes active again.
    /// </summary>
    public sealed class SwitchExplorationMapCommand : ICommand
    {
        public SwitchExplorationMapCommand(string mapId, IReadOnlyList<ExplorationActorCarry>? carryActors = null)
        {
            MapId = mapId;
            CarryActors = carryActors ?? Array.Empty<ExplorationActorCarry>();
        }

        public string MapId { get; }

        public IReadOnlyList<ExplorationActorCarry> CarryActors { get; }
    }

    /// <summary>
    /// One actor to move onto the target map during a <see cref="SwitchExplorationMapCommand"/>:
    /// removed from the previous map and placed at (X, Y) on the new one.
    /// </summary>
    public sealed class ExplorationActorCarry
    {
        public ExplorationActorCarry(string actorId, int x, int y, bool blocksMovement = true)
        {
            ActorId = actorId;
            X = x;
            Y = y;
            BlocksMovement = blocksMovement;
        }

        public string ActorId { get; }

        public int X { get; }

        public int Y { get; }

        public bool BlocksMovement { get; }
    }
}
