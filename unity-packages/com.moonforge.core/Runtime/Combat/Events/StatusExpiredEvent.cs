using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events
{

    public sealed class StatusExpiredEvent : DomainEvent
    {
        public StatusExpiredEvent(string actorId, string statusId)
            : base(nameof(StatusExpiredEvent))
        {
            ActorId = actorId;
            StatusId = statusId;
        }

        public string ActorId { get; }

        public string StatusId { get; }
    }
}
