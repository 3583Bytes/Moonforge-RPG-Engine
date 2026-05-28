using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Stats.Events
{

    public sealed class StatModifiersRemovedEvent : DomainEvent
    {
        public StatModifiersRemovedEvent(string actorId, string sourceKind, string sourceId, int removedCount)
            : base(nameof(StatModifiersRemovedEvent))
        {
            ActorId = actorId;
            SourceKind = sourceKind;
            SourceId = sourceId;
            RemovedCount = removedCount;
        }

        public string ActorId { get; }

        public string SourceKind { get; }

        public string SourceId { get; }

        public int RemovedCount { get; }
    }
}
