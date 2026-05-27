using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Equipment.Queries
{

    public sealed class GetEquippedItemQueryHandler : IQueryHandler<GetEquippedItemQuery, string?>
    {
        public string? Query(GameState gameState, GetEquippedItemQuery query)
        {
            return gameState.EquipmentState.GetEquippedItem(query.SlotId);
        }
    }
}
