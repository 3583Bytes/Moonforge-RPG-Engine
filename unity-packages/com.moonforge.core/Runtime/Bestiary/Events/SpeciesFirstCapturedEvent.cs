using Moonforge.Core.Runtime.Events;

namespace Moonforge.Core.Bestiary.Events
{

    public sealed class SpeciesFirstCapturedEvent : DomainEvent
    {
        public SpeciesFirstCapturedEvent(string speciesId, long atMinutes)
            : base(nameof(SpeciesFirstCapturedEvent))
        {
            SpeciesId = speciesId;
            AtMinutes = atMinutes;
        }

        public string SpeciesId { get; }

        public long AtMinutes { get; }
    }
}
