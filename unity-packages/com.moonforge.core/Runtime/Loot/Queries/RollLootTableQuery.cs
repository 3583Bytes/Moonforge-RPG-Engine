using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Loot.Queries
{

    /// <summary>
    /// Rolls a loot table and returns the result without mutating the game state. The roll uses
    /// the caller-supplied <c>IRandomSource</c>, so callers wanting a reproducible preview can
    /// pass a seeded source separate from the game's main RNG.
    /// </summary>
    public sealed class RollLootTableQuery : IQuery<LootRollResult>
    {
        public RollLootTableQuery(string tableId)
        {
            TableId = tableId;
        }

        public string TableId { get; }
    }
}
