using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Party.Events;

public sealed class PartyMemberAddedEvent : DomainEvent
{
    public PartyMemberAddedEvent(string actorId, bool isActive)
        : base(nameof(PartyMemberAddedEvent))
    {
        ActorId = actorId;
        IsActive = isActive;
    }

    public string ActorId { get; }

    public bool IsActive { get; }
}
