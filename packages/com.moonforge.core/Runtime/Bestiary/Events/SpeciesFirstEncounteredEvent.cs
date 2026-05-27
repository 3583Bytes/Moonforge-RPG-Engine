using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Bestiary.Events
{

    public sealed class SpeciesFirstEncounteredEvent : DomainEvent
    {
        public SpeciesFirstEncounteredEvent(string speciesId, long atMinutes)
            : base(nameof(SpeciesFirstEncounteredEvent))
        {
            SpeciesId = speciesId;
            AtMinutes = atMinutes;
        }

        public string SpeciesId { get; }

        public long AtMinutes { get; }
    }
}
