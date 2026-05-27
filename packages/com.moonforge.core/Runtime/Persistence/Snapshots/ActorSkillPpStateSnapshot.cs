using System.Collections.Generic;

namespace Moonforge.Core.Persistence.Snapshots
{

    public sealed class ActorSkillPpStateSnapshot
    {
        public List<ActorSkillPpEntrySnapshot> Actors { get; set; } = new();
    }

    public sealed class ActorSkillPpEntrySnapshot
    {
        public string ActorId { get; set; } = string.Empty;

        public List<SkillPpSnapshot> Skills { get; set; } = new();
    }

    public sealed class SkillPpSnapshot
    {
        public string SkillId { get; set; } = string.Empty;

        public int Pp { get; set; }
    }
}
