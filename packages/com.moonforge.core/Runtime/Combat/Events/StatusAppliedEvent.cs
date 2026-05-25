using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events;

public sealed class StatusAppliedEvent : DomainEvent
{
    public StatusAppliedEvent(string actorId, string statusId, int durationTurns, string? sourceActorId)
        : base(nameof(StatusAppliedEvent))
    {
        ActorId = actorId;
        StatusId = statusId;
        DurationTurns = durationTurns;
        SourceActorId = sourceActorId;
    }

    public string ActorId { get; }

    public string StatusId { get; }

    public int DurationTurns { get; }

    public string? SourceActorId { get; }
}
