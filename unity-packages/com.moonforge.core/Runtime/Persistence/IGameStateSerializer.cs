using Moonforge.Core.Persistence.Snapshots;

namespace Moonforge.Core.Persistence
{

    public interface IGameStateSerializer
    {
        string Serialize(GameStateSnapshot snapshot);

        GameStateSnapshot Deserialize(string payload);
    }
}
