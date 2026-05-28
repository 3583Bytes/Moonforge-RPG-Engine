using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Stats.Events
{

    public sealed class StatModifierAppliedEvent : DomainEvent
    {
        public StatModifierAppliedEvent(
            string actorId,
            string statId,
            StatModifierBucket bucket,
            string sourceKind,
            string sourceId,
            double value)
            : base(nameof(StatModifierAppliedEvent))
        {
            ActorId = actorId;
            StatId = statId;
            Bucket = bucket;
            SourceKind = sourceKind;
            SourceId = sourceId;
            Value = value;
        }

        public string ActorId { get; }

        public string StatId { get; }

        public StatModifierBucket Bucket { get; }

        public string SourceKind { get; }

        public string SourceId { get; }

        public double Value { get; }
    }
}
