using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class EquipmentStateSnapshot
    {
        public List<EquipmentSlotSnapshot> Slots { get; set; } = new();
    }

    public sealed class EquipmentSlotSnapshot
    {
        public string SlotId { get; set; } = string.Empty;

        public string ItemId { get; set; } = string.Empty;
    }
}
