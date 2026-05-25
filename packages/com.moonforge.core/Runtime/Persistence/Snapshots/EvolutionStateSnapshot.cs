using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots;

public sealed class EvolutionStateSnapshot
{
    public List<ActorEvolutionEligibilitySnapshot> Actors { get; set; } = new();
}

public sealed class ActorEvolutionEligibilitySnapshot
{
    public string ActorId { get; set; } = string.Empty;

    public List<string> EvolutionIds { get; set; } = new();
}
