using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots;

public sealed class InventoryBagSnapshot
{
    public int CapacitySlots { get; set; } = 32;

    public List<InventoryStackSnapshot> Stacks { get; set; } = new();
}

public sealed class InventoryStackSnapshot
{
    public string ItemId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int StackLimit { get; set; }
}
