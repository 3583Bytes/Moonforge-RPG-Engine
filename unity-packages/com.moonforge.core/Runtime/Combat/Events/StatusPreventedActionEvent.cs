using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Combat.Events
{

    public sealed class StatusPreventedActionEvent : DomainEvent
    {
        public StatusPreventedActionEvent(string actorId, string statusId)
            : base(nameof(StatusPreventedActionEvent))
        {
            ActorId = actorId;
            StatusId = statusId;
        }

        public string ActorId { get; }

        public string StatusId { get; }
    }
}
