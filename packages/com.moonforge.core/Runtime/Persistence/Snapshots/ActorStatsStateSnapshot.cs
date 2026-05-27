using System.Collections.Generic;
using Moonforge.Core.Stats;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class ActorStatsStateSnapshot
    {
        public List<ActorStatsSnapshot> Actors { get; set; } = new();
    }

    public sealed class ActorStatsSnapshot
    {
        public string ActorId { get; set; } = string.Empty;

        public List<StatBaseSnapshot> Base { get; set; } = new();

        public List<StatModifierSnapshot> Modifiers { get; set; } = new();
    }

    public sealed class StatBaseSnapshot
    {
        public string StatId { get; set; } = string.Empty;

        public int Value { get; set; }
    }

    public sealed class StatModifierSnapshot
    {
        public string StatId { get; set; } = string.Empty;

        public StatModifierBucket Bucket { get; set; }

        public double Value { get; set; }

        public string SourceKind { get; set; } = string.Empty;

        public string SourceId { get; set; } = string.Empty;

        public int Priority { get; set; }
    }
}
