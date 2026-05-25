using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Exploration.Commands;

public sealed class UpsertExplorationActorCommand : ICommand
{
    public UpsertExplorationActorCommand(string actorId, int x, int y, bool blocksMovement = true)
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
