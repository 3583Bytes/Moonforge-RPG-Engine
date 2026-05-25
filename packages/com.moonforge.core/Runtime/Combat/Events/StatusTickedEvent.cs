using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events;

public sealed class StatusTickedEvent : DomainEvent
{
    public StatusTickedEvent(string actorId, string statusId, int hpDelta, int remainingTurns)
        : base(nameof(StatusTickedEvent))
    {
        ActorId = actorId;
        StatusId = statusId;
        HpDelta = hpDelta;
        RemainingTurns = remainingTurns;
    }

    public string ActorId { get; }

    public string StatusId { get; }

    public int HpDelta { get; }

    public int RemainingTurns { get; }
}
