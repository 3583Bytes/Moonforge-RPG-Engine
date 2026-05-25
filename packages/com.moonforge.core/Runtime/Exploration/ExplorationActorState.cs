namespace Moonforge.Core.Exploration;

public sealed class ExplorationActorState
{
    public ExplorationActorState(string actorId, GridPosition position, bool blocksMovement)
    {
        ActorId = actorId;
        X = position.X;
        Y = position.Y;
        BlocksMovement = blocksMovement;
    }

    public string ActorId { get; }

    public int X { get; set; }

    public int Y { get; set; }

    public bool BlocksMovement { get; set; }

    public GridPosition Position => new(X, Y);

    public ExplorationActorState Clone()
    {
        return new ExplorationActorState(ActorId, new GridPosition(X, Y), BlocksMovement);
    }
}
