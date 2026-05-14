using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots;

public sealed class ProgressionStateSnapshot
{
    public List<ActorProgressionSnapshot> Actors { get; set; } = new();
}

public sealed class ActorProgressionSnapshot
{
    public string ActorId { get; set; } = string.Empty;

    public string CurveId { get; set; } = string.Empty;

    public int Level { get; set; }

    public long Xp { get; set; }
}
