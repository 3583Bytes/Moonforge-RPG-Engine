using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Bestiary.Queries
{

    public sealed class GetBestiaryEntryQuery : IQuery<BestiaryEntry?>
    {
        public GetBestiaryEntryQuery(string speciesId)
        {
            SpeciesId = speciesId;
        }

        public string SpeciesId { get; }
    }
}
