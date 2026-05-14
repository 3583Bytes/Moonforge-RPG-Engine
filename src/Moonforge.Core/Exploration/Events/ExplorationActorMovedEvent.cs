using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Exploration.Events;

public sealed class ExplorationActorMovedEvent : DomainEvent
{
    public ExplorationActorMovedEvent(string actorId, int fromX, int fromY, int toX, int toY)
        : base("exploration.actor.moved")
    {
        ActorId = actorId;
        FromX = fromX;
        FromY = fromY;
        ToX = toX;
        ToY = toY;
    }

    public string ActorId { get; }

    public int FromX { get; }

    public int FromY { get; }

    public int ToX { get; }

    public int ToY { get; }
}
