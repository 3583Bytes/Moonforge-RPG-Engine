namespace Moonforge.Core.Progression;

public sealed class ActorProgression
{
    public ActorProgression(string actorId, string curveId, int level = 1, long xp = 0)
    {
        ActorId = actorId;
        CurveId = curveId;
        Level = level < 1 ? 1 : level;
        Xp = xp < 0 ? 0 : xp;
    }

    public string ActorId { get; }

    public string CurveId { get; set; }

    public int Level { get; set; }

    public long Xp { get; set; }

    public ActorProgression Clone()
    {
        return new ActorProgression(ActorId, CurveId, Level, Xp);
    }
}
