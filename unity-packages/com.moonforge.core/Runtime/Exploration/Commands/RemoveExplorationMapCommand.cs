using Moonforge.Core.Runtime.Commands;

namespace Moonforge.Core.Exploration.Commands
{

    /// <summary>
    /// Discards a non-active exploration map and its actors — e.g. dungeon floors the game
    /// will never revisit. Fails on the active map; switch away first.
    /// </summary>
    public sealed class RemoveExplorationMapCommand : ICommand
    {
        public RemoveExplorationMapCommand(string mapId)
        {
            MapId = mapId;
        }

        public string MapId { get; }
    }
}
