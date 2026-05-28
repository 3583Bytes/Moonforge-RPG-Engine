using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Party.Events
{

    public sealed class PartyMemberRemovedEvent : DomainEvent
    {
        public PartyMemberRemovedEvent(string actorId)
            : base(nameof(PartyMemberRemovedEvent))
        {
            ActorId = actorId;
        }

        public string ActorId { get; }
    }
}
