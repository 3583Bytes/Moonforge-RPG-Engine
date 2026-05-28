using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Loot.Commands
{

    /// <summary>
    /// Rolls a loot table and deposits the results into the inventory bag and currency wallet
    /// atomically. If any individual deposit fails, the whole operation is rolled back by the
    /// command dispatcher and no drops are applied.
    /// </summary>
    public sealed class RollAndGrantLootCommand : ICommand
    {
        public RollAndGrantLootCommand(string tableId)
        {
            TableId = tableId;
        }

        public string TableId { get; }
    }
}
