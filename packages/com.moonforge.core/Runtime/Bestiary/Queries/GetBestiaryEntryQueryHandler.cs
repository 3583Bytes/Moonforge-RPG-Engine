using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Bestiary.Queries;

public sealed class GetBestiaryEntryQueryHandler : IQueryHandler<GetBestiaryEntryQuery, BestiaryEntry?>
{
    public BestiaryEntry? Query(GameState gameState, GetBestiaryEntryQuery query)
    {
        return gameState.BestiaryState.TryGet(query.SpeciesId, out BestiaryEntry entry) ? entry : null;
    }
}
