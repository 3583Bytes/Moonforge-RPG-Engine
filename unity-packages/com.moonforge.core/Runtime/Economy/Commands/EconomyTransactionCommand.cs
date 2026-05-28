using System.Collections.Generic;
using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Economy.Commands
{

    public sealed class EconomyTransactionCommand : ICommand
    {
        public EconomyTransactionCommand(
            IReadOnlyList<CurrencyDelta>? currencyDeltas = null,
            IReadOnlyList<InventoryDelta>? inventoryDeltas = null)
        {
            CurrencyDeltas = currencyDeltas ?? System.Array.Empty<CurrencyDelta>();
            InventoryDeltas = inventoryDeltas ?? System.Array.Empty<InventoryDelta>();
        }

        public IReadOnlyList<CurrencyDelta> CurrencyDeltas { get; }

        public IReadOnlyList<InventoryDelta> InventoryDeltas { get; }
    }
}
