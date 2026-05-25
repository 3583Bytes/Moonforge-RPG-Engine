using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Exploration.Events;

public sealed class ExplorationActorPositionedEvent : DomainEvent
{
    public ExplorationActorPositionedEvent(string actorId, int x, int y, bool blocksMovement)
        : base("exploration.actor.positioned")
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
