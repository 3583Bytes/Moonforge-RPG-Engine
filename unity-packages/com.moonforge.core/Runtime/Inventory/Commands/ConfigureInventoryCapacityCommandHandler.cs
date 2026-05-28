using System;
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

            try
            {
                gameState.InventoryBag.SetCapacity(command.CapacitySlots);
                return DomainResult.Success();
            }
            catch (Exception ex)
            {
                return DomainResult.Fail(new DomainError(DomainErrorCode.ValidationFailed, ex.Message));
            }
        }
    }
}
