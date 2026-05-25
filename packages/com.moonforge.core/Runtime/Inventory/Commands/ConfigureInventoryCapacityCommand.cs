using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Inventory.Commands;

public sealed class ConfigureInventoryCapacityCommand : ICommand
{
    public ConfigureInventoryCapacityCommand(int capacitySlots)
    {
        CapacitySlots = capacitySlots;
    }

    public int CapacitySlots { get; }
}
