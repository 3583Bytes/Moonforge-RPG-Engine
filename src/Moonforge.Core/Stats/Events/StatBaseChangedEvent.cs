using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Stats.Events;

public sealed class StatBaseChangedEvent : DomainEvent
{
    public StatBaseChangedEvent(string actorId, string statId, int previous, int next)
        : base(nameof(StatBaseChangedEvent))
    {
        ActorId = actorId;
        StatId = statId;
        Previous = previous;
        Next = next;
    }

    public string ActorId { get; }

    public string StatId { get; }

    public int Previous { get; }

    public int Next { get; }
}
