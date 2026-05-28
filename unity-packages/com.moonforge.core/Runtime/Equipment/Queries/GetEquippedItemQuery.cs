using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Equipment.Queries
{

    public sealed class GetEquippedItemQuery : IQuery<string?>
    {
        public GetEquippedItemQuery(string slotId)
        {
            SlotId = slotId;
        }

        public string SlotId { get; }
    }
}
