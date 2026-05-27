using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Party.Events
{

    public sealed class PartyMemberActiveChangedEvent : DomainEvent
    {
        public PartyMemberActiveChangedEvent(string actorId, bool isActive)
            : base(nameof(PartyMemberActiveChangedEvent))
        {
            ActorId = actorId;
            IsActive = isActive;
        }

        public string ActorId { get; }

        public bool IsActive { get; }
    }
}
