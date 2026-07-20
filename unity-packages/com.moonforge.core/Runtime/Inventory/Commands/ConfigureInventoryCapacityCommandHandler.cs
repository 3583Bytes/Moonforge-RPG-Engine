using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Inventory.Commands
{

    public sealed class ConfigureInventoryCapacityCommandHandler : ICommandHandler<ConfigureInventoryCapacityCommand>
    {
        public DomainResult Handle(GameState gameState, ConfigureInventoryCapacityCommand command, CommandContext context)
        {
            if (command.CapacitySlots <= 0)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Capacity slots must be positive."));
            }

            if (command.CapacitySlots < gameState.InventoryBag.UsedSlots)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, "Cannot set capacity below current used slots."));
            }

            // Inputs are fully validated above; SetCapacity cannot throw for these arguments.
            gameState.InventoryBag.SetCapacity(command.CapacitySlots);
            return DomainResult.Success();
        }
    }
}
