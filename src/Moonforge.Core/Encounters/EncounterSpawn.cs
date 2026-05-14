namespace Moonforge.Core.Encounters;

public sealed class EncounterSpawn
{
    public EncounterSpawn(string actorId, int count)
    {
        ActorId = actorId;
        Count = count;
    }

    public string ActorId { get; }

    public int Count { get; }
}
